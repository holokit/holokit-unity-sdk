// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

typedef void (*OnThermalStateChangedCallback)(void * _Nonnull, int);

@interface AppleNativeProvider : NSObject

@property (nonatomic, assign) OnThermalStateChangedCallback _Nullable onThermalStateChangedCallback;

- (int)getThermalState;
- (double)getSystemUptime;

@end


