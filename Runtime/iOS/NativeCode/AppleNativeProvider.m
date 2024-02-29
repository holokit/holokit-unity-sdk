// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
// SPDX-License-Identifier: MIT

#import "AppleNativeProvider.h"

@interface AppleNativeProvider()

@end

@implementation AppleNativeProvider

- (instancetype)init {
    if (self = [super init]) {
        [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(OnThermalStateChanged) name:NSProcessInfoThermalStateDidChangeNotification object:nil];
    }
    return self;
}

- (void)OnThermalStateChanged {
    if (self.onThermalStateChangedCallback != NULL) {
        NSProcessInfoThermalState thermalState = [[NSProcessInfo processInfo] thermalState];
        dispatch_async(dispatch_get_main_queue(), ^{
            self.onThermalStateChangedCallback((__bridge void *)self, (int)thermalState);
        });
    }
}

- (int)getThermalState {
    NSProcessInfoThermalState thermalState = [[NSProcessInfo processInfo] thermalState];
    return (int)thermalState;
}

- (double)getSystemUptime {
    return [[NSProcessInfo processInfo] systemUptime];
}

@end
