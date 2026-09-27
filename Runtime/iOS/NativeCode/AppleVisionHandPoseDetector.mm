// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
//
// Vision's 21 hand joints per hand, with LiDAR-depth world positions, as the HoloKit
// Swift SDK's HandTracker does: Vision runs on a serial queue off the main thread (a
// frame that arrives while one is in flight is skipped), each joint takes its own
// reliable depth, else the hand's median depth (remembered for 0.5 s per hand), else
// a 0.65 m plane.

#import "AppleVisionHandPoseDetector.h"
#import <QuartzCore/QuartzCore.h>
#include "HoloKitHandDepth.h"

static const int kJointCount = 21;

API_AVAILABLE(ios(14.0))
@interface AppleVisionHandPoseDetector ()

@property (nonatomic, strong) VNDetectHumanHandPoseRequest *handPoseRequest;
@property (nonatomic, strong) ARSession *arSession;
@property (nonatomic, strong) dispatch_queue_t queue;
@property (atomic, assign) BOOL busy;

@end

@implementation AppleVisionHandPoseDetector {
    // Last measured hand depth per chirality (index chirality + 1), touched on the queue only.
    float _rememberedDepth[3];
    double _rememberedTime[3];
    bool _hasRemembered[3];
}

- (instancetype)initWithARSession:(ARSession *)arSession maximumHandCount:(int)maximumHandCount {
    if (self = [super init]) {
        if (@available(iOS 14.0, *)) {
            self.handPoseRequest = [[VNDetectHumanHandPoseRequest alloc] init];
            self.handPoseRequest.maximumHandCount = maximumHandCount;
            self.arSession = arSession;
            self.queue = dispatch_queue_create("io.holokit.hands", dispatch_queue_attr_make_with_qos_class(
                DISPATCH_QUEUE_SERIAL, QOS_CLASS_USER_INITIATED, 0));
        } else {
            NSLog(@"Apple Vision framework is only available on iOS 14.0 or higher");
        }
    }
    return self;
}

- (void)processCurrentFrame2D {
    [self processCurrentFrameWithDepth:NO];
}

- (void)processCurrentFrame3D {
    [self processCurrentFrameWithDepth:YES];
}

// One ARFrame per request: Vision, depth and unprojection all use the same frame.
- (void)processCurrentFrameWithDepth:(BOOL)withDepth {
    if (@available(iOS 14.0, *)) {
        ARFrame *frame = self.arSession.currentFrame;
        if (frame == nil || frame.capturedImage == NULL || self.busy) return;
        self.busy = YES;
        dispatch_async(self.queue, ^{
            [self runVisionOnFrame:frame withDepth:withDepth];
        });
    }
}

- (void)runVisionOnFrame:(ARFrame *)frame withDepth:(BOOL)withDepth API_AVAILABLE(ios(14.0)) {
    VNImageRequestHandler *handler = [[VNImageRequestHandler alloc] initWithCVPixelBuffer:frame.capturedImage
                                                                              orientation:kCGImagePropertyOrientationUp
                                                                                  options:@{}];
    NSArray<VNHumanHandPoseObservation *> *observations = nil;
    @try {
        if ([handler performRequests:@[self.handPoseRequest] error:nil])
            observations = self.handPoseRequest.results;
    } @catch (NSException *e) {
        observations = nil;
    }
    int handCount = (int)observations.count;
    if (handCount == 0) {
        [self deliverHandCount:0 results2D:NULL results3D:NULL confidences:NULL chiralities:NULL];
        return;
    }

    float *results2D = (float *)malloc(sizeof(float) * 2 * kJointCount * handCount);
    float *confidences = (float *)malloc(sizeof(float) * kJointCount * handCount);
    int *chiralities = (int *)malloc(sizeof(int) * handCount);
    float *results3D = withDepth ? (float *)malloc(sizeof(float) * 3 * kJointCount * handCount) : NULL;

    ARDepthData *sceneDepth = frame.sceneDepth ?: frame.smoothedSceneDepth;
    holokit::HandDepthImage image;
    CVPixelBufferRef depthMap = withDepth && sceneDepth != nil ? sceneDepth.depthMap : NULL;
    CVPixelBufferRef confidenceMap = depthMap != NULL ? sceneDepth.confidenceMap : NULL;
    if (depthMap != NULL && CVPixelBufferGetPixelFormatType(depthMap) == kCVPixelFormatType_DepthFloat32 &&
        CVPixelBufferLockBaseAddress(depthMap, kCVPixelBufferLock_ReadOnly) == kCVReturnSuccess) {
        image.depth = (const float *)CVPixelBufferGetBaseAddress(depthMap);
        image.width = CVPixelBufferGetWidth(depthMap);
        image.height = CVPixelBufferGetHeight(depthMap);
        image.depthRowFloats = CVPixelBufferGetBytesPerRow(depthMap) / sizeof(float);
        if (confidenceMap != NULL && CVPixelBufferGetPixelFormatType(confidenceMap) == kCVPixelFormatType_OneComponent8 &&
            CVPixelBufferGetWidth(confidenceMap) == image.width && CVPixelBufferGetHeight(confidenceMap) == image.height &&
            CVPixelBufferLockBaseAddress(confidenceMap, kCVPixelBufferLock_ReadOnly) == kCVReturnSuccess) {
            image.confidence = (const uint8_t *)CVPixelBufferGetBaseAddress(confidenceMap);
            image.confidenceRowBytes = CVPixelBufferGetBytesPerRow(confidenceMap);
        } else {
            confidenceMap = NULL;
        }
    } else {
        depthMap = NULL;
        confidenceMap = NULL;
    }

    for (int i = 0; i < handCount; i++) {
        VNHumanHandPoseObservation *observation = observations[i];
        int chirality = 0;
        if (@available(iOS 15.0, *)) chirality = (int)observation.chirality; // -1 left, 0 unknown, 1 right
        chiralities[i] = chirality;
        VNRecognizedPoint *points[kJointCount];
        for (int j = 0; j < kJointCount; j++) {
            points[j] = [observation recognizedPointForJointName:[AppleVisionHandPoseDetector jointNameAtIndex:j] error:nil];
            results2D[(i * kJointCount + j) * 2] = points[j].x;
            results2D[(i * kJointCount + j) * 2 + 1] = points[j].y;
            confidences[i * kJointCount + j] = points[j].confidence;
        }
        if (results3D == NULL) continue;

        // The hand's depth: the median reliable depth of its confident joints, or its
        // last one for up to 0.5 s. Without it a whole hand fell back to the plane and
        // jumped 20-50 cm for a frame (Swift SDK device trace).
        float own[kJointCount];
        bool hasOwn[kJointCount];
        float votes[kJointCount];
        int voteCount = 0;
        for (int j = 0; j < kJointCount; j++) {
            hasOwn[j] = depthMap != NULL && holokit::reliableHandDepth(image, points[j].x, points[j].y, &own[j]);
            if (hasOwn[j] && points[j].confidence >= holokit::kHandDepthMinimumConfidence) votes[voteCount++] = own[j];
        }
        float measured = 0, hand = 0;
        bool hasMeasured = holokit::handDepthMedian(votes, voteCount, 3, &measured);
        int slot = chirality + 1;
        bool hasHand = holokit::currentHandDepth(hasMeasured, measured, _hasRemembered[slot], _rememberedDepth[slot],
                                                 _rememberedTime[slot], frame.timestamp, &hand);
        if (hasMeasured) {
            _hasRemembered[slot] = true;
            _rememberedDepth[slot] = measured;
            _rememberedTime[slot] = frame.timestamp;
        }
        for (int j = 0; j < kJointCount; j++) {
            float depth;
            holokit::selectJointDepth(hasOwn[j], own[j], hasHand, hand, &depth);
            simd_float3 world = [AppleVisionHandPoseDetector worldPositionWithCamera:frame.camera
                                                                               x:points[j].x y:points[j].y depth:depth];
            // ARKit's right-handed world to Unity's left-handed one.
            results3D[(i * kJointCount + j) * 3] = world.x;
            results3D[(i * kJointCount + j) * 3 + 1] = world.y;
            results3D[(i * kJointCount + j) * 3 + 2] = -world.z;
        }
    }
    if (confidenceMap != NULL) CVPixelBufferUnlockBaseAddress(confidenceMap, kCVPixelBufferLock_ReadOnly);
    if (depthMap != NULL) CVPixelBufferUnlockBaseAddress(depthMap, kCVPixelBufferLock_ReadOnly);
    [self deliverHandCount:handCount results2D:results2D results3D:results3D confidences:confidences chiralities:chiralities];
}

// Calls back on the main thread, then accepts the next frame.
- (void)deliverHandCount:(int)handCount results2D:(float *)results2D results3D:(float *)results3D
             confidences:(float *)confidences chiralities:(int *)chiralities {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (self.onHandPoseUpdatedCallback != NULL)
            self.onHandPoseUpdatedCallback((__bridge void *)self, handCount, results2D, results3D, confidences, chiralities);
        free(results2D);
        free(results3D);
        free(confidences);
        free(chiralities);
        self.busy = NO;
    });
}

// Pinhole unprojection with the frame's intrinsics (HandProjection.worldPosition):
// Vision's image point and depth to ARKit world space.
+ (simd_float3)worldPositionWithCamera:(ARCamera *)camera x:(float)x y:(float)y depth:(float)depth {
    simd_float3x3 k = camera.intrinsics;
    float pixelX = x * (float)camera.imageResolution.width;
    float pixelY = (1 - y) * (float)camera.imageResolution.height;
    simd_float4 local = simd_make_float4((pixelX - k.columns[2].x) / k.columns[0].x * depth,
                                         -(pixelY - k.columns[2].y) / k.columns[1].y * depth,
                                         -depth, 1);
    simd_float4 world = simd_mul(camera.transform, local);
    return simd_make_float3(world.x, world.y, world.z);
}

+ (VNRecognizedPointKey)jointNameAtIndex:(int)index API_AVAILABLE(ios(14.0)) {
    static VNHumanHandPoseObservationJointName names[kJointCount];
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        VNHumanHandPoseObservationJointName ordered[kJointCount] = {
            VNHumanHandPoseObservationJointNameWrist,
            VNHumanHandPoseObservationJointNameThumbCMC, VNHumanHandPoseObservationJointNameThumbMP,
            VNHumanHandPoseObservationJointNameThumbIP, VNHumanHandPoseObservationJointNameThumbTip,
            VNHumanHandPoseObservationJointNameIndexMCP, VNHumanHandPoseObservationJointNameIndexPIP,
            VNHumanHandPoseObservationJointNameIndexDIP, VNHumanHandPoseObservationJointNameIndexTip,
            VNHumanHandPoseObservationJointNameMiddleMCP, VNHumanHandPoseObservationJointNameMiddlePIP,
            VNHumanHandPoseObservationJointNameMiddleDIP, VNHumanHandPoseObservationJointNameMiddleTip,
            VNHumanHandPoseObservationJointNameRingMCP, VNHumanHandPoseObservationJointNameRingPIP,
            VNHumanHandPoseObservationJointNameRingDIP, VNHumanHandPoseObservationJointNameRingTip,
            VNHumanHandPoseObservationJointNameLittleMCP, VNHumanHandPoseObservationJointNameLittlePIP,
            VNHumanHandPoseObservationJointNameLittleDIP, VNHumanHandPoseObservationJointNameLittleTip,
        };
        for (int j = 0; j < kJointCount; j++) names[j] = ordered[j];
    });
    return names[index >= 0 && index < kJointCount ? index : 0];
}

@end
