// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#import <ARKit/ARKit.h>

// XRSessionExtensions.GetNativePtr
typedef struct UnityXRNativeSession
{
    int version;
    void* sessionPtr;
} UnityXRNativeSession;

@interface HoloKitARKitNativeProvider : NSObject

@property (nonatomic, strong, nullable) ARSession *session;

+ (simd_float4x4)getSimdFloat4x4WithPosition:(float [3])position rotation:(float [4])rotation;

- (void)setVideoEnhancement:(bool)enabled;

@end
