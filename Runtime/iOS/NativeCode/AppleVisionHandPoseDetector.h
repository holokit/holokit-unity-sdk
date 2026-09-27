// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#import <Vision/Vision.h>
#import <ARKit/ARKit.h>
#import "UnityXRInterface.h"

// detector, hand count, 2D joints (x, y per joint), 3D joints (x, y, z; NULL for 2D-only
// requests), confidences (one per joint), chiralities (-1 left, 0 unknown, 1 right, one per hand).
typedef void (*OnHandPoseUpdatedCallback)(void * _Nonnull, int, float * _Nullable, float * _Nullable, float * _Nullable, int * _Nullable);

@interface AppleVisionHandPoseDetector : NSObject

@property (nonatomic, assign) OnHandPoseUpdatedCallback _Nullable onHandPoseUpdatedCallback;

- (nonnull instancetype)initWithARSession:(ARSession *_Nonnull)arSession maximumHandCount:(int)maximumHandCount;
- (void)processCurrentFrame2D;
- (void)processCurrentFrame3D;

@end
