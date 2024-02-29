// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
// SPDX-License-Identifier: MIT

#import <Vision/Vision.h>
#import <ARKit/ARKit.h>
#import "UnityXRInterface.h"

typedef void (*OnHandPoseUpdatedCallback)(void * _Nonnull, int, float * _Nullable, float * _Nullable, float * _Nullable);

@interface AppleVisionHandPoseDetector : NSObject

@property (nonatomic, assign) OnHandPoseUpdatedCallback _Nullable onHandPoseUpdatedCallback;

- (nonnull instancetype)initWithARSession:(ARSession *_Nonnull)arSession maximumHandCount:(int)maximumHandCount;
- (void)processCurrentFrame2D;
- (void)processCurrentFrame3D;

@end
