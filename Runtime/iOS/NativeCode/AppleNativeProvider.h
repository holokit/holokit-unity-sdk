// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

typedef void (*OnThermalStateChangedCallback)(void * _Nonnull, int);

@interface AppleNativeProvider : NSObject

@property (nonatomic, assign) OnThermalStateChangedCallback _Nullable onThermalStateChangedCallback;

- (int)getThermalState;
- (double)getSystemUptime;

@end


