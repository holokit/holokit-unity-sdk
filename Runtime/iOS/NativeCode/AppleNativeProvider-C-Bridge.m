// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#import "AppleNativeProvider.h"

void* HoloInteractiveHoloKit_AppleNativeProvider_init() {
    AppleNativeProvider *provider = [[AppleNativeProvider alloc] init];
    return (__bridge_retained void *)provider;
}

void HoloInteractiveHoloKit_AppleNativeProvider_registerCallbacks(void *self,
                                                                  OnThermalStateChangedCallback onThermalStateChangedCallback) {
    AppleNativeProvider *provider = (__bridge AppleNativeProvider *)self;
    [provider setOnThermalStateChangedCallback:onThermalStateChangedCallback];
}

int HoloInteractiveHoloKit_AppleNativeProvider_getThermalState(void *self) {
    AppleNativeProvider *provider = (__bridge AppleNativeProvider *)self;
    return [provider getThermalState];
}

double HoloInteractiveHoloKit_AppleNativeProvider_getSystemUptime(void *self) {
    AppleNativeProvider *provider = (__bridge AppleNativeProvider *)self;
    return [provider getSystemUptime];
}
