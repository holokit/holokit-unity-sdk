// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
// SPDX-License-Identifier: MIT

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

typedef void (*OnThermalStateChangedCallback)(void * _Nonnull, int);

@interface AppleNativeProvider : NSObject

@property (nonatomic, assign) OnThermalStateChangedCallback _Nullable onThermalStateChangedCallback;

- (int)getThermalState;
- (double)getSystemUptime;

@end


