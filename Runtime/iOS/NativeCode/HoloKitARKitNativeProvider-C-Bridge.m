// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#import "HoloKitARKitNativeProvider.h"

void* HoloKit_HoloKitARKitNativeProvider_init() {
    HoloKitARKitNativeProvider *provider = [[HoloKitARKitNativeProvider alloc] init];
    return (__bridge_retained void *)provider;
}

void HoloKit_HoloKitARKitNativeProvider_setARSessionPtr(void *self, UnityXRNativeSession *nativeARSessionPtr) {
    if (nativeARSessionPtr == NULL) {
        NSLog(@"[HoloKitARKitNativeProvider] nativeARSessionPtr is NULL");
        return;
    }
    
    HoloKitARKitNativeProvider *provider = (__bridge HoloKitARKitNativeProvider *)self;
    ARSession *session = (__bridge ARSession *)nativeARSessionPtr->sessionPtr;
    [provider setSession:session];
}


void HoloKit_HoloKitARKitNativeProvider_registerCallbacks(void *self,
                                                                         OnARSessionUpdatedFrame onARSessionUpdatedFrame) {
    HoloKitARKitNativeProvider *provider = (__bridge HoloKitARKitNativeProvider *)self;
    [provider setOnARSessionUpdatedFrame:onARSessionUpdatedFrame];
}

void HoloKit_HoloKitARKitNativeProvider_interceptUnityARSessionDelegate(void *self, UnityXRNativeSession *nativeARSessionPtr) {
    if (nativeARSessionPtr == NULL) {
        return;
    }
    
    HoloKitARKitNativeProvider *provider = (__bridge HoloKitARKitNativeProvider *)self;
    ARSession *session = (__bridge ARSession *)nativeARSessionPtr->sessionPtr;
    [provider setUnityARSessionDelegate:session.delegate];
    [provider setSession:session];
    if (session.delegate != provider) {
        [session setDelegate:provider];
    }
}

void HoloKit_HoloKitARKitNativeProvider_restoreUnityARSessionDelegate(void *self, UnityXRNativeSession *nativeARSessionPtr) {
    if (nativeARSessionPtr == NULL) {
        return;
    }
    
    HoloKitARKitNativeProvider *provider = (__bridge HoloKitARKitNativeProvider *)self;
    ARSession *session = (__bridge ARSession *)nativeARSessionPtr->sessionPtr;
    if (session.delegate == provider) {
        [session setDelegate:provider.unityARSessionDelegate];
    }
}

void HoloKit_HoloKitARKitNativeProvider_resetOrigin(void *self, float position[3], float rotation[4]) {
    HoloKitARKitNativeProvider *provider = (__bridge HoloKitARKitNativeProvider *)self;
    simd_float4x4 transform_matrix = [HoloKitARKitNativeProvider getSimdFloat4x4WithPosition:position rotation:rotation];
    [[provider session] setWorldOrigin:transform_matrix];
}

void HoloKit_HoloKitARKitNativeProvider_setVideoEnhancement(void *self, bool enabled) {
    HoloKitARKitNativeProvider *provider = (__bridge HoloKitARKitNativeProvider *)self;
    [provider setVideoEnhancement:enabled];
}
