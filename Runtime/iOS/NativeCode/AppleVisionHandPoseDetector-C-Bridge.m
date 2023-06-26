#import "AppleVisionHandPoseDetector.h"

void* HoloInteractiveHoloKit_AppleVisionHandPoseDetector_initWithARSession(UnityXRNativeSession *arSessionPtr, int maximumHandCount) {
    ARSession *arSession = (__bridge ARSession *)arSessionPtr->sessionPtr;
    AppleVisionHandPoseDetector *detector = [[AppleVisionHandPoseDetector alloc] initWithARSession: arSession maximumHandCount:maximumHandCount];
    return (__bridge_retained void *)detector;
}

void HoloInteractiveHoloKit_AppleVisionHandPoseDetector_registerCallbacks(void *self,
                                                                          OnHandPose2DUpdatedCallback onHandPose2DUpdatedCallback,
                                                                          OnHandPose3DUpdatedCallback onHandPose3DUpdatedCallback) {
    AppleVisionHandPoseDetector *detector = (__bridge AppleVisionHandPoseDetector *)self;
    [detector setOnHandPose2DUpdatedCallback:onHandPose2DUpdatedCallback];
    [detector setOnHandPose3DUpdatedCallback:onHandPose3DUpdatedCallback];
}

void HoloInteractiveHoloKit_AppleVisionHandPoseDetector_processCurrentFrame2D(void *self) {
    AppleVisionHandPoseDetector *detector = (__bridge AppleVisionHandPoseDetector *)self;
    return [detector processCurrentFrame2D];
}

void HoloInteractiveHoloKit_AppleVisionHandPoseDetector_processCurrentFrame3D(void *self) {
    AppleVisionHandPoseDetector *detector = (__bridge AppleVisionHandPoseDetector *)self;
    return [detector processCurrentFrame3D];
}
