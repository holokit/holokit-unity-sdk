// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
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
