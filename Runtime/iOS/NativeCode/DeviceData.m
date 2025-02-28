// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#import "DeviceData.h"

@interface DeviceData()

@end

@implementation DeviceData

+ (BOOL)supportLiDAR {
    BOOL support = [ARWorldTrackingConfiguration supportsSceneReconstruction:ARSceneReconstructionMesh];
    return support;
}

@end
