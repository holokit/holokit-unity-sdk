// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
// SPDX-License-Identifier: MIT

#import "AppleVisionHandPoseDetector.h"

void* HoloKit_AppleVisionHandPoseDetector_initWithARSession(UnityXRNativeSession *arSessionPtr, int maximumHandCount) {
    ARSession *arSession = (__bridge ARSession *)arSessionPtr->sessionPtr;
    AppleVisionHandPoseDetector *detector = [[AppleVisionHandPoseDetector alloc] initWithARSession: arSession maximumHandCount:maximumHandCount];
    return (__bridge_retained void *)detector;
}

void HoloKit_AppleVisionHandPoseDetector_registerCallbacks(void *self,
                                                                          OnHandPoseUpdatedCallback onHandPoseUpdatedCallback) {
    AppleVisionHandPoseDetector *detector = (__bridge AppleVisionHandPoseDetector *)self;
    [detector setOnHandPoseUpdatedCallback:onHandPoseUpdatedCallback];
}

void HoloKit_AppleVisionHandPoseDetector_processCurrentFrame2D(void *self) {
    AppleVisionHandPoseDetector *detector = (__bridge AppleVisionHandPoseDetector *)self;
    return [detector processCurrentFrame2D];
}

void HoloKit_AppleVisionHandPoseDetector_processCurrentFrame3D(void *self) {
    AppleVisionHandPoseDetector *detector = (__bridge AppleVisionHandPoseDetector *)self;
    return [detector processCurrentFrame3D];
}
