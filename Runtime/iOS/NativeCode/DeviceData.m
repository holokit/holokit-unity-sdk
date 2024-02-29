// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
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
