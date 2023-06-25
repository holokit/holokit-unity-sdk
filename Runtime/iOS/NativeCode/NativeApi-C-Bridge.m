// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#import <Foundation/Foundation.h>

void HoloInteractiveHoloKit_NativeApi_CFRelease(void* ptr)
{
    if (ptr)
    {
        CFRelease(ptr);
    }
}
