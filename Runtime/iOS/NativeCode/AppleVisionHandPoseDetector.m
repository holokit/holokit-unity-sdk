// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#import "AppleVisionHandPoseDetector.h"

API_AVAILABLE(ios(14.0))
@interface AppleVisionHandPoseDetector ()

@property (nonatomic, strong) VNDetectHumanHandPoseRequest *handPoseRequest;
@property (nonatomic, strong) ARSession *arSession;

@end

@implementation AppleVisionHandPoseDetector

- (instancetype)initWithARSession:(ARSession *)arSession maximumHandCount:(int)maximumHandCount {
    if (self = [super init]) {
        if (@available(iOS 14.0, *)) {
            self.handPoseRequest = [[VNDetectHumanHandPoseRequest alloc] init];
            self.handPoseRequest.usesCPUOnly = false;
            self.handPoseRequest.maximumHandCount = maximumHandCount;
            self.arSession = arSession;
        } else {
            NSLog(@"Apple Vision framework is only available on iOS 14.0 or higher");
        }
    }
    return self;
}

- (BOOL)processCurrentFrame {
    if (@available(iOS 14.0, *)) {
        VNImageRequestHandler *imageRequestHandler = [[VNImageRequestHandler alloc] initWithCVPixelBuffer: self.arSession.currentFrame.capturedImage orientation:kCGImagePropertyOrientationUp options:[NSMutableDictionary dictionary]];
        @try {
            NSArray<VNRequest *> *requests = [[NSArray alloc] initWithObjects:self.handPoseRequest, nil];
            [imageRequestHandler performRequests:requests error:nil];
            return YES;
        } @catch(NSException *e) {
            return NO;
        }
    }
}

- (void)processCurrentFrame2D {
    if (@available(iOS 14.0, *)) {
        if ([self processCurrentFrame]) {
            int handCount = (int)self.handPoseRequest.results.count;
            if (handCount == 0) {
                if (self.onHandPoseUpdatedCallback != NULL) {
                    dispatch_async(dispatch_get_main_queue(), ^{
                        self.onHandPoseUpdatedCallback((__bridge void *)self, 0, NULL, NULL);
                    });
                }
                return;
            }
            
            float *results2D = malloc(sizeof(float) * 2 * 21 * handCount);
            for (int i = 0; i < handCount; i++) {
                VNHumanHandPoseObservation *observation = self.handPoseRequest.results[i];
                for (int j = 0; j < 21; j++) {
                    VNRecognizedPoint *point = [observation recognizedPointForJointName:[AppleVisionHandPoseDetector getVNHumanHandPoseObservationJointNameWithJointIndex:j] error:nil];
                    results2D[i * 2 * 21 + j * 2] = point.x;
                    results2D[i * 2 * 21 + j * 2 + 1] = point.y;
                }
            }
            
            if (self.onHandPoseUpdatedCallback != NULL) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    self.onHandPoseUpdatedCallback((__bridge void *)self, handCount, results2D, NULL);
                    free(results2D);
                });
            }
        } else {
            if (self.onHandPoseUpdatedCallback != NULL) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    self.onHandPoseUpdatedCallback((__bridge void *)self, 0, NULL, NULL);
                });
            }
        }
    } else {
        
    }
}

- (void)processCurrentFrame3D {
    if (@available(iOS 14.0, *)) {
        if (self.arSession.currentFrame.sceneDepth == nil) {
            NSLog(@"Failed to get environment depth image");
            return;
        }
        
        if ([self processCurrentFrame]) {
            int handCount = (int)self.handPoseRequest.results.count;
            if (handCount == 0) {
                if (self.onHandPoseUpdatedCallback != NULL) {
                    dispatch_async(dispatch_get_main_queue(), ^{
                        self.onHandPoseUpdatedCallback((__bridge void *)self, 0, NULL, NULL);
                    });
                }
                return;
            }

            // Get scene depth
            CVPixelBufferLockBaseAddress(self.arSession.currentFrame.sceneDepth.depthMap, 0);
            size_t depthBufferWidth = CVPixelBufferGetWidth(self.arSession.currentFrame.sceneDepth.depthMap);
            size_t depthBufferHeight = CVPixelBufferGetHeight(self.arSession.currentFrame.sceneDepth.depthMap);
            Float32 *depthBufferBaseAddress = (Float32 *)CVPixelBufferGetBaseAddress(self.arSession.currentFrame.sceneDepth.depthMap);

            float *results2D = malloc(sizeof(float) * 2 * 21 * handCount);
            float *results3D = malloc(sizeof(float) * 3 * 21 * handCount);
            for (int i = 0; i < handCount; i++) {
                VNHumanHandPoseObservation *observation = self.handPoseRequest.results[i];
                for (int j = 0; j < 21; j++) {
                    VNRecognizedPoint *point = [observation recognizedPointForJointName:[AppleVisionHandPoseDetector getVNHumanHandPoseObservationJointNameWithJointIndex:j] error:nil];
                    results2D[i * 2 * 21 + j * 2] = point.x;
                    results2D[i * 2 * 21 + j * 2 + 1] = point.y;
                    
                    // Get the depth of the point
                    int depthX = point.x * depthBufferWidth;
                    int depthY = (1 - point.y) * depthBufferHeight;
                    float depth = (float)depthBufferBaseAddress[depthY * depthBufferWidth + depthX];
                    simd_float3 unprojectedPoint = [self unprojectScreenPointWithLocationX:point.x locationY:point.y depth:depth];
                    results3D[i * 3 * 21 + j * 3] = unprojectedPoint.x;
                    results3D[i * 3 * 21 + j * 3 + 1] = unprojectedPoint.y;
                    results3D[i * 3 * 21 + j * 3 + 2] = -unprojectedPoint.z;
                }
            }
            CVPixelBufferUnlockBaseAddress(self.arSession.currentFrame.sceneDepth.depthMap, 0);

            if (self.onHandPoseUpdatedCallback != NULL) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    self.onHandPoseUpdatedCallback((__bridge void *)self, handCount, results2D, results3D);
                    free(results2D);
                    free(results3D);
                });
            }
        } else {
            if (self.onHandPoseUpdatedCallback != NULL) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    self.onHandPoseUpdatedCallback((__bridge void *)self, 0, NULL, NULL);
                });
            }
        }
    } else {
        
    }
}

+ (VNRecognizedPointKey)getVNHumanHandPoseObservationJointNameWithJointIndex:(int)jointIndex {
    if (@available(iOS 14.0, *)) {
        switch(jointIndex) {
            case 0:
                return VNHumanHandPoseObservationJointNameWrist;
            case 1:
                return VNHumanHandPoseObservationJointNameThumbCMC;
            case 2:
                return VNHumanHandPoseObservationJointNameThumbMP;
            case 3:
                return VNHumanHandPoseObservationJointNameThumbIP;
            case 4:
                return VNHumanHandPoseObservationJointNameThumbTip;
            case 5:
                return VNHumanHandPoseObservationJointNameIndexMCP;
            case 6:
                return VNHumanHandPoseObservationJointNameIndexPIP;
            case 7:
                return VNHumanHandPoseObservationJointNameIndexDIP;
            case 8:
                return VNHumanHandPoseObservationJointNameIndexTip;
            case 9:
                return VNHumanHandPoseObservationJointNameMiddleMCP;
            case 10:
                return VNHumanHandPoseObservationJointNameMiddlePIP;
            case 11:
                return VNHumanHandPoseObservationJointNameMiddleDIP;
            case 12:
                return VNHumanHandPoseObservationJointNameMiddleTip;
            case 13:
                return VNHumanHandPoseObservationJointNameRingMCP;
            case 14:
                return VNHumanHandPoseObservationJointNameRingPIP;
            case 15:
                return VNHumanHandPoseObservationJointNameRingDIP;
            case 16:
                return VNHumanHandPoseObservationJointNameRingTip;
            case 17:
                return VNHumanHandPoseObservationJointNameLittleMCP;
            case 18:
                return VNHumanHandPoseObservationJointNameLittlePIP;
            case 19:
                return VNHumanHandPoseObservationJointNameLittleDIP;
            case 20:
                return VNHumanHandPoseObservationJointNameLittleTip;
            default:
                return VNHumanHandPoseObservationJointNameWrist;
        }
    } else {
        return nil;
    }
}

- (simd_float3)unprojectScreenPointWithLocationX:(float)locationX locationY:(float)locationY depth:(float)depth {
    CGFloat screenX = (CGFloat)locationX * self.arSession.currentFrame.camera.imageResolution.width;
    CGFloat screenY = (CGFloat)(1 - locationY) * self.arSession.currentFrame.camera.imageResolution.height;
    CGPoint screenPoint = CGPointMake(screenX, screenY);
    
    simd_float4x4 translation = matrix_identity_float4x4;
    translation.columns[3].z = -depth;
    simd_float4x4 planeOrigin = simd_mul(self.arSession.currentFrame.camera.transform, translation);
    simd_float3 xAxis = simd_make_float3(1, 0, 0);
    simd_float4x4 rotation = simd_matrix4x4(simd_quaternion(0.5 * M_PI, xAxis));
    simd_float4x4 plane = simd_mul(planeOrigin, rotation);
    simd_float3 unprojectedPoint = [self.arSession.currentFrame.camera unprojectPoint:screenPoint ontoPlaneWithTransform:plane orientation:UIInterfaceOrientationLandscapeRight viewportSize:self.arSession.currentFrame.camera.imageResolution];
    return simd_make_float3(unprojectedPoint.x, unprojectedPoint.y, unprojectedPoint.z);
}

@end
