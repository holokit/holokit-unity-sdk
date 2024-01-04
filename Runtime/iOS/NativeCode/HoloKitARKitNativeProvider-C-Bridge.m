// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#import "HoloKitARKitNativeProvider.h"

void* HoloInteractiveHoloKit_HoloKitARKitNativeProvider_init() {
    HoloKitARKitNativeProvider *provider = [[HoloKitARKitNativeProvider alloc] init];
    return (__bridge_retained void *)provider;
}

void HoloInteractiveHoloKit_HoloKitARKitNativeProvider_setARSessionPtr(void *self, UnityXRNativeSession *nativeARSessionPtr) {
    if (nativeARSessionPtr == NULL) {
        NSLog(@"[HoloKitARKitNativeProvider] nativeARSessionPtr is NULL");
        return;
    }
    
    HoloKitARKitNativeProvider *provider = (__bridge HoloKitARKitNativeProvider *)self;
    ARSession *session = (__bridge ARSession *)nativeARSessionPtr->sessionPtr;
    [provider setSession:session];
}

void HoloInteractiveHoloKit_HoloKitARKitNativeProvider_resetOrigin(void *self, float position[3], float rotation[4]) {
    HoloKitARKitNativeProvider *provider = (__bridge HoloKitARKitNativeProvider *)self;
    simd_float4x4 transform_matrix = [HoloKitARKitNativeProvider getSimdFloat4x4WithPosition:position rotation:rotation];
    [[provider session] setWorldOrigin:transform_matrix];
}

void HoloInteractiveHoloKit_HoloKitARKitNativeProvider_setVideoEnhancement(void *self, bool enabled) {
    HoloKitARKitNativeProvider *provider = (__bridge HoloKitARKitNativeProvider *)self;
    [provider setVideoEnhancement:enabled];
}
