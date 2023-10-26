// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#import "DeviceData.h"

bool HoloInteractiveHoloKit_DeviceData_supportLiDAR(void) {
    return [DeviceData supportLiDAR];
}
