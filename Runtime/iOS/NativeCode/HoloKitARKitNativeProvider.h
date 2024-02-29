// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
// SPDX-License-Identifier: MIT

#import <ARKit/ARKit.h>

// XRSessionExtensions.GetNativePtr
typedef struct UnityXRNativeSession
{
    int version;
    void* sessionPtr;
} UnityXRNativeSession;

typedef void (*OnARSessionUpdatedFrame)(void * _Nonnull, double, const float *);

@interface HoloKitARKitNativeProvider : NSObject

@property (nonatomic, strong, nullable) ARSession *session;
@property (nonatomic, weak, nullable) id<ARSessionDelegate> unityARSessionDelegate;
@property (nonatomic, assign, nullable) OnARSessionUpdatedFrame onARSessionUpdatedFrame;

+ (simd_float4x4)getSimdFloat4x4WithPosition:(float [3])position rotation:(float [4])rotation;

- (void)setVideoEnhancement:(bool)enabled;

@end
