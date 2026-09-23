// SPDX-License-Identifier: MIT
#pragma once
#import <AVFoundation/AVFoundation.h>

// Source bits: game = 1, microphone = 2. Each source has a real AAC track.
@interface HoloKitRecordingAudio : NSObject
@property(nonatomic, readonly) NSArray<AVAssetWriterInput*>* inputs;
@property(nonatomic, readonly) int droppedPackets;
@property(nonatomic, readonly) BOOL failed;
- (instancetype)initWithWriter:(AVAssetWriter*)writer sources:(int)sources
    gameRate:(int)gameRate gameChannels:(int)channels microphoneRate:(int)microphoneRate
    origin:(double)origin;
- (BOOL)begin;
- (void)setGameGain:(float)game microphoneGain:(float)microphone;
// Asynchronous, including producer quiescence and encoder backpressure.
- (void)stopAt:(double)hostTime;
- (void)finishAt:(double)hostTime completion:(void (^)(void))completion;
@end

#ifdef __cplusplus
extern "C" {
#endif
// Copies into bounded queues. No allocation, encoder call or wait on an audio thread.
int HoloKit_RecordingAudioPush(int source, const float* samples, int frames,
    int channels, int sampleRate, double hostTime);
#ifdef __cplusplus
}
#endif
