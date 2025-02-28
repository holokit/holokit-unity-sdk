// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#import "AppleNativeProvider.h"

void* HoloKit_AppleNativeProvider_init() {
    AppleNativeProvider *provider = [[AppleNativeProvider alloc] init];
    return (__bridge_retained void *)provider;
}

void HoloKit_AppleNativeProvider_registerCallbacks(void *self,
                                                                  OnThermalStateChangedCallback onThermalStateChangedCallback) {
    AppleNativeProvider *provider = (__bridge AppleNativeProvider *)self;
    [provider setOnThermalStateChangedCallback:onThermalStateChangedCallback];
}

int HoloKit_AppleNativeProvider_getThermalState(void *self) {
    AppleNativeProvider *provider = (__bridge AppleNativeProvider *)self;
    return [provider getThermalState];
}

double HoloKit_AppleNativeProvider_getSystemUptime(void *self) {
    AppleNativeProvider *provider = (__bridge AppleNativeProvider *)self;
    return [provider getSystemUptime];
}
