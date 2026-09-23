// SPDX-License-Identifier: MIT
#import <Foundation/Foundation.h>
#import <Metal/Metal.h>

@interface VideoRecorder : NSObject
+ (id)sharedInstance;
- (void)setMetalDevice:(id<MTLDevice>)device;
- (int)allocateGPUSlots:(int)count width:(int)width height:(int)height;
// Acquire from the writer pool. A buffer retained by the encoder is not reused.
- (void*)acquireGPUSlot:(int)index;
// Caller must observe a completed GPU fence before submission or slot reuse.
- (int)submitGPUSlot:(int)index time:(double)time;
- (void)releaseGPUSlots;
@end
