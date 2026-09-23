// SPDX-License-Identifier: MIT
#import "HoloKitMixedMovie.h"
#import <AVFoundation/AVFoundation.h>

@implementation HoloKitMixedMovie {
    AVURLAsset* _asset;
    AVAssetReader* _reader;
    AVAssetWriter* _writer;
    NSArray<AVAssetReaderOutput*>* _outputs;
    NSArray<AVAssetWriterInput*>* _inputs;
    dispatch_queue_t _queue;
    NSURL* _destination;
    void (^_completion)(NSError*);
    int _ended;
    BOOL _finished;
}
+ (instancetype)exportMovie:(NSURL*)source to:(NSURL*)destination completion:(void (^)(NSError*))completion {
    HoloKitMixedMovie* job = [HoloKitMixedMovie new];
    job->_queue = dispatch_queue_create("io.holokit.recording.mix", DISPATCH_QUEUE_SERIAL);
    job->_completion = [completion copy];
    job->_destination = destination;
    job->_asset = [AVURLAsset URLAssetWithURL:source options:nil];
    dispatch_async(job->_queue, ^{
        if (job->_finished) return;
        [job->_asset loadTracksWithMediaType:AVMediaTypeVideo completionHandler:^(NSArray<AVAssetTrack*>* video, NSError* error) {
            dispatch_async(job->_queue, ^{
                if (job->_finished) return;
                if (error || video.count != 1) { [job fail:error reason:@"The movie needs exactly one video track."]; return; }
                [job->_asset loadTracksWithMediaType:AVMediaTypeAudio completionHandler:^(NSArray<AVAssetTrack*>* audio, NSError* error) {
                    dispatch_async(job->_queue, ^{ @autoreleasepool {
                        if (job->_finished) return;
                        if (error || audio.count == 0) [job fail:error reason:@"The movie has no audio tracks to mix."];
                        else [job prepareVideo:video.firstObject audio:audio];
                    } });
                }];
            });
        }];
    });
    return job;
}
- (void)prepareVideo:(AVAssetTrack*)video audio:(NSArray<AVAssetTrack*>*)audio {
    if (_finished) return;
    NSError* error = nil;
    _reader = [[AVAssetReader alloc] initWithAsset:_asset error:&error];
    if (!_reader) { [self fail:error reason:@"Cannot read the original movie."]; return; }
    if ([NSFileManager.defaultManager fileExistsAtPath:_destination.path]) {
        [self fail:nil reason:@"The sharing destination already exists."]; return;
    }
    _writer = [[AVAssetWriter alloc] initWithURL:_destination fileType:AVFileTypeMPEG4 error:&error];
    if (!_writer) { [self fail:error reason:@"Cannot create the sharing movie."]; return; }
    // This work runs on a serial background queue. No main-thread media loading,
    // video decoding, resizing, GPU readback or HEVC re-encoding takes place.
    AVAssetReaderTrackOutput* videoOutput = [AVAssetReaderTrackOutput assetReaderTrackOutputWithTrack:video outputSettings:nil];
    videoOutput.alwaysCopiesSampleData = NO;
    CMFormatDescriptionRef hint = (__bridge CMFormatDescriptionRef)video.formatDescriptions.firstObject;
    AVAssetWriterInput* videoInput = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeVideo outputSettings:nil sourceFormatHint:hint];
    videoInput.transform = video.preferredTransform;
    AVAssetReaderAudioMixOutput* audioOutput = [AVAssetReaderAudioMixOutput assetReaderAudioMixOutputWithAudioTracks:audio audioSettings:@{
        AVFormatIDKey: @(kAudioFormatLinearPCM), AVSampleRateKey: @48000, AVNumberOfChannelsKey: @2,
        AVLinearPCMBitDepthKey: @32, AVLinearPCMIsFloatKey: @YES,
        AVLinearPCMIsBigEndianKey: @NO, AVLinearPCMIsNonInterleaved: @NO }];
    NSMutableArray* parameters = [NSMutableArray array];
    for (AVAssetTrack* track in audio) {
        AVMutableAudioMixInputParameters* parameter = [AVMutableAudioMixInputParameters audioMixInputParametersWithTrack:track];
        [parameter setVolume:1.0f / audio.count atTime:kCMTimeZero];
        [parameters addObject:parameter];
    }
    AVMutableAudioMix* mix = [AVMutableAudioMix audioMix];
    mix.inputParameters = parameters;
    audioOutput.audioMix = mix;
    AVAssetWriterInput* audioInput = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeAudio outputSettings:@{
        AVFormatIDKey: @(kAudioFormatMPEG4AAC), AVSampleRateKey: @48000,
        AVNumberOfChannelsKey: @2, AVEncoderAudioQualityKey: @(AVAudioQualityHigh) }];
    _outputs = @[videoOutput, audioOutput];
    _inputs = @[videoInput, audioInput];
    for (NSUInteger i = 0; i < _inputs.count; i++) {
        if (![_reader canAddOutput:_outputs[i]] || ![_writer canAddInput:_inputs[i]]) {
            [self fail:nil reason:@"This movie's streams cannot be exported."]; return;
        }
        [_reader addOutput:_outputs[i]];
        [_writer addInput:_inputs[i]];
    }
    if (![_reader startReading] || ![_writer startWriting]) {
        [self fail:_reader.error ?: _writer.error reason:@"Could not start sharing export."]; return;
    }
    [_writer startSessionAtSourceTime:kCMTimeZero];
    for (NSUInteger i = 0; i < _inputs.count; i++) {
        AVAssetWriterInput* input = _inputs[i];
        AVAssetReaderOutput* output = _outputs[i];
        [input requestMediaDataWhenReadyOnQueue:_queue usingBlock:^{ @autoreleasepool {
            if (self->_finished) return;
            while (input.readyForMoreMediaData) {
                CMSampleBufferRef buffer = [output copyNextSampleBuffer];
                if (buffer) {
                    BOOL appended = [input appendSampleBuffer:buffer];
                    CFRelease(buffer);
                    if (!appended) { [self fail:self->_writer.error reason:@"Sharing export append failed."]; return; }
                } else {
                    if (self->_reader.status == AVAssetReaderStatusFailed) {
                        [self fail:self->_reader.error reason:@"Sharing export decode failed."]; return;
                    }
                    [input markAsFinished];
                    if (++self->_ended == 2) {
                        [self->_writer finishWritingWithCompletionHandler:^{
                            dispatch_async(self->_queue, ^{ @autoreleasepool {
                                if (self->_writer.status == AVAssetWriterStatusCompleted) [self complete:nil];
                                else [self fail:self->_writer.error reason:@"Sharing export finalization failed."];
                            } });
                        }];
                    }
                    return;
                }
            }
            if (self->_writer.status == AVAssetWriterStatusFailed)
                [self fail:self->_writer.error reason:@"Sharing export failed."];
        } }];
    }
}
- (void)fail:(NSError*)error reason:(NSString*)reason {
    if (_finished) return;
    [_reader cancelReading];
    if (_writer.status == AVAssetWriterStatusWriting || _writer.status == AVAssetWriterStatusUnknown)
        [_writer cancelWriting];
    // The original file is untouched. Remove only this job's incomplete output.
    if (_writer) [NSFileManager.defaultManager removeItemAtURL:_destination error:nil];
    [self complete:error ?: [NSError errorWithDomain:@"HoloKitRecording" code:1 userInfo:@{NSLocalizedDescriptionKey:reason}]];
}
- (void)complete:(NSError*)error {
    if (_finished) return;
    _finished = YES;
    auto completion = _completion;
    _completion = nil;
    _reader = nil; _writer = nil; _outputs = nil; _inputs = nil; _asset = nil;
    if (completion) completion(error);
}
- (void)cancel {
    dispatch_async(_queue, ^{ @autoreleasepool { [self fail:nil reason:@"Sharing export was cancelled."]; } });
}
@end
