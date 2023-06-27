// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#import <Vision/Vision.h>
#import <ARKit/ARKit.h>
#import "UnityXRInterface.h"

typedef void (*OnHandPoseUpdatedCallback)(void * _Nonnull, int, float * _Nullable, float * _Nullable);

@interface AppleVisionHandPoseDetector : NSObject

@property (nonatomic, assign) OnHandPoseUpdatedCallback _Nullable onHandPoseUpdatedCallback;

- (nonnull instancetype)initWithARSession:(ARSession *_Nonnull)arSession maximumHandCount:(int)maximumHandCount;
- (void)processCurrentFrame2D;
- (void)processCurrentFrame3D;

@end
