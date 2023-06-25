#import "AppleVisionHandPoseDetector.h"

void* HoloInteractiveHoloKit_AppleVisionHandPoseDetector_initWithARSession(UnityXRNativeSession *arSessionPtr, int maximumHandCount) {
    ARSession *arSession = (__bridge ARSession *)arSessionPtr->sessionPtr;
    AppleVisionHandPoseDetector *detector = [[AppleVisionHandPoseDetector alloc] initWithARSession: arSession maximumHandCount:maximumHandCount];
    return (__bridge_retained void *)detector;
}

bool HoloInteractiveHoloKit_AppleVisionHandPoseDetector_processCurrentFrame(void *self) {
    AppleVisionHandPoseDetector *detector = (__bridge AppleVisionHandPoseDetector *)self;
    return [detector processCurrentFrame];
}

int HoloInteractiveHoloKit_AppleVisionHandPoseDetector_getResultCount(void *self) {
    AppleVisionHandPoseDetector *detector = (__bridge AppleVisionHandPoseDetector *)self;
    return [detector getResultCount];
}

void HoloInteractiveHoloKit_AppleVisionHandPoseDetector_getHandJointLocation(void *self, int handIndex, int jointIndex, float *x, float *y) {
    if (@available(iOS 14.0, *)) {
        AppleVisionHandPoseDetector *detector = (__bridge AppleVisionHandPoseDetector *)self;
        VNRecognizedPoint *point = [detector getHandJointWithHandIndex: handIndex jointIndex: jointIndex];
        *x = point.location.x;
        *y = point.location.y;
    } else {
        // Fallback on earlier versions
    }
}

float HoloInteractiveHoloKit_AppleVisionHandPoseDetector_getHandJointConfidence(void *self, int handIndex, int jointIndex) {
    if (@available(iOS 14.0, *)) {
        AppleVisionHandPoseDetector *detector = (__bridge AppleVisionHandPoseDetector *)self;
        VNRecognizedPoint *point = [detector getHandJointWithHandIndex: handIndex jointIndex: jointIndex];
        return point.confidence;
    } else {
        return 0;
    }
}
