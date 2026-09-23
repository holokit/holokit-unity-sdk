// SPDX-License-Identifier: MIT
#pragma once
#import <Foundation/Foundation.h>

// Produces a separate sharing file: compressed video passes through unchanged,
// source audio tracks are mixed on their existing timeline with equal headroom.
@interface HoloKitMixedMovie : NSObject
+ (instancetype)exportMovie:(NSURL*)source to:(NSURL*)destination
    completion:(void (^)(NSError* error))completion;
- (void)cancel;
@end
