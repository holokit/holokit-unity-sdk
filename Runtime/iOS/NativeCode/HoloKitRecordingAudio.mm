// SPDX-License-Identifier: MIT
#import "HoloKitRecordingAudio.h"
#import "HoloKitAudioBuffer.h"
#import "HoloKitAudioCapture.h"
#include <atomic>
#include <limits>
#include <memory>

namespace {
struct Track {
    holokit::AudioPacketQueue packets;
    AVAssetWriterInput* __strong input = nil;
    CMAudioFormatDescriptionRef format = nullptr;
    int rate = 0, channels = 0;
    double end = -1;
    std::atomic<float> gain {1};
    ~Track() { if (format) CFRelease(format); }
};
struct AudioState {
    Track tracks[2];
    std::atomic<int> dropped {0};
    std::atomic<bool> failed {false};
};
std::atomic<AudioState*> active {nullptr};
// Increment BEFORE reading active. A retired state cannot be freed while any
// callback may have loaded it; finalization polls this count on its worker queue.
std::atomic<unsigned> publishers {0};
void MicrophonePacket(const float* samples, int frames, int channels, int rate, double host) {
    HoloKit_RecordingAudioPush(2, samples, frames, channels, rate, host);
}
}

int HoloKit_RecordingAudioPush(int source, const float* samples, int frames,
                             int channels, int rate, double host) {
    publishers.fetch_add(1);
    AudioState* state = active.load();
    int result = 0;
    if (!state) result = -2;
    else if ((source != 1 && source != 2) || !samples || frames <= 0 || !std::isfinite(host)) {
        state->failed.store(true); result = -1;
    } else {
        Track& track = state->tracks[source == 1 ? 0 : 1];
        if (!track.rate) result = -2; // this source is not selected
        else if (rate != track.rate || channels != track.channels) {
            state->failed.store(true); result = -1; // never silently change a track's format
        } else {
            for (int offset = 0; offset < frames; offset += holokit::AudioPacket::MaxFrames) {
                int count = std::min(int(holokit::AudioPacket::MaxFrames), frames - offset);
                if (!track.packets.push(samples + offset * channels, count, channels, rate,
                                       host + double(offset) / rate, track.gain.load())) {
                    state->dropped.fetch_add(1); result = -3;
                }
            }
        }
    }
    publishers.fetch_sub(1);
    return result;
}

@implementation HoloKitRecordingAudio {
    std::unique_ptr<AudioState> _state;
    AVAssetWriter* _writer;
    dispatch_queue_t _queue;
    dispatch_source_t _timer;
    double _origin, _stop, _deadline, _closeAfter;
    BOOL _closing, _unpublished;
    void (^_completion)(void);
}
- (instancetype)initWithWriter:(AVAssetWriter*)writer sources:(int)sources
    gameRate:(int)gameRate gameChannels:(int)channels microphoneRate:(int)microphoneRate
    origin:(double)origin {
    if (!(self = [super init])) return nil;
    if (sources < 0 || sources > 3 || !std::isfinite(origin)) return nil;
    _state = std::make_unique<AudioState>();
    _writer = writer;
    _origin = origin;
    _stop = std::numeric_limits<double>::infinity();
    NSMutableArray* inputs = [NSMutableArray array];
    for (int index = 0; index < 2; index++) {
        if (!(sources & (1 << index))) continue;
        Track& track = _state->tracks[index];
        track.rate = index == 0 ? gameRate : microphoneRate;
        track.channels = index == 0 ? channels : 1;
        if (track.rate <= 0 || track.channels < 1 || track.channels > 2) return nil;
        NSDictionary* settings = @{ AVFormatIDKey: @(kAudioFormatMPEG4AAC),
            AVSampleRateKey: @(track.rate), AVNumberOfChannelsKey: @(track.channels),
            AVEncoderAudioQualityKey: @(AVAudioQualityHigh) };
        track.input = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeAudio outputSettings:settings];
        track.input.expectsMediaDataInRealTime = YES;
        AVMutableMetadataItem* name = [AVMutableMetadataItem metadataItem];
        name.identifier = AVMetadataCommonIdentifierTitle;
        name.value = index == 0 ? @"Game" : @"Microphone";
        track.input.metadata = @[name];
        AudioStreamBasicDescription format = {};
        format.mSampleRate = track.rate;
        format.mFormatID = kAudioFormatLinearPCM;
        format.mFormatFlags = kAudioFormatFlagsNativeFloatPacked;
        format.mFramesPerPacket = 1;
        format.mChannelsPerFrame = track.channels;
        format.mBitsPerChannel = 32;
        format.mBytesPerFrame = format.mBytesPerPacket = track.channels * sizeof(float);
        if (CMAudioFormatDescriptionCreate(kCFAllocatorDefault, &format, 0, nullptr, 0,
                nullptr, nullptr, &track.format) != noErr || ![writer canAddInput:track.input]) return nil;
        [writer addInput:track.input];
        [inputs addObject:track.input];
    }
    _inputs = inputs;
    _queue = dispatch_queue_create("io.holokit.recording.audio", DISPATCH_QUEUE_SERIAL);
    return self;
}
- (BOOL)begin {
    AudioState* expected = nullptr;
    if (!active.compare_exchange_strong(expected, _state.get())) return NO;
#if TARGET_OS_IOS
    HoloKit_MicrophoneSetRecordingSink(MicrophonePacket);
#endif
    _timer = dispatch_source_create(DISPATCH_SOURCE_TYPE_TIMER, 0, 0, _queue);
    dispatch_source_set_timer(_timer, DISPATCH_TIME_NOW, 5 * NSEC_PER_MSEC, NSEC_PER_MSEC);
    // This intentional timer ownership ends after final drain, not when the
    // MonoBehaviour is disabled or destroyed.
    dispatch_source_set_event_handler(_timer, ^{ @autoreleasepool { [self pump]; } });
    dispatch_resume(_timer);
    return YES;
}
- (int)droppedPackets { return _state->dropped.load(); }
- (BOOL)failed { return _state->failed.load(); }
- (void)setGameGain:(float)game microphoneGain:(float)microphone {
    if (!std::isfinite(game) || !std::isfinite(microphone)) { _state->failed.store(true); return; }
    _state->tracks[0].gain.store(std::clamp(game, 0.0f, 2.0f));
    _state->tracks[1].gain.store(std::clamp(microphone, 0.0f, 2.0f));
}
- (void)stopAt:(double)hostTime {
    dispatch_async(_queue, ^{
        if (self->_closing) return;
        self->_closing = YES;
        self->_stop = hostTime;
        self->_closeAfter = HoloKit_AudioClockTime() + 0.15;
        self->_deadline = self->_closeAfter + 5;
    });
}
- (void)finishAt:(double)hostTime completion:(void (^)(void))completion {
    [self stopAt:hostTime];
    dispatch_async(_queue, ^{ self->_completion = [completion copy]; });
}
- (BOOL)append:(const holokit::AudioPacket&)packet track:(Track&)track {
    auto range = holokit::framesInRecording(packet, _origin, _stop);
    if (range.end <= range.begin) return YES;
    double timestamp = packet.hostTime + double(range.begin) / packet.sampleRate - _origin;
    if (track.end >= 0 && timestamp < track.end - 1.5 / track.rate) {
        _state->failed.store(true); return YES;
    }
    const int frames = range.end - range.begin;
    const int bytes = frames * track.channels * sizeof(float);
    CMBlockBufferRef block = nullptr;
    CMSampleBufferRef buffer = nullptr;
    OSStatus status = CMBlockBufferCreateWithMemoryBlock(kCFAllocatorDefault, nullptr, bytes,
        kCFAllocatorDefault, nullptr, 0, bytes, kCMBlockBufferAssureMemoryNowFlag, &block);
    if (status == noErr) {
        float scaled[holokit::AudioPacket::MaxFrames * 2];
        const float gain = packet.gain;
        for (int i = 0; i < frames * track.channels; i++)
            scaled[i] = std::clamp(packet.samples[range.begin * track.channels + i] * gain, -1.0f, 1.0f);
        status = CMBlockBufferReplaceDataBytes(scaled, block, 0, bytes);
    }
    if (status == noErr) status = CMAudioSampleBufferCreateReadyWithPacketDescriptions(
        kCFAllocatorDefault, block, track.format, frames, CMTimeMakeWithSeconds(timestamp, track.rate), nullptr, &buffer);
    BOOL appended = status == noErr && [track.input appendSampleBuffer:buffer];
    if (buffer) CFRelease(buffer);
    if (block) CFRelease(block);
    if (!appended) _state->failed.store(true);
    else track.end = timestamp + double(frames) / track.rate;
    return YES;
}
- (void)pump {
    double now = HoloKit_AudioClockTime();
    if (_closing && !_unpublished && now >= _closeAfter) {
        AudioState* expected = _state.get();
        active.compare_exchange_strong(expected, nullptr);
        _unpublished = YES;
    }
    BOOL empty = YES;
    for (Track& track : _state->tracks) {
        while (const auto* packet = track.packets.front()) {
            if (_writer.status != AVAssetWriterStatusWriting || (_closing && now > _deadline)) {
                _state->dropped.fetch_add(1);
                _state->failed.store(true);
            } else {
                // Hold a small timestamped tail so a stop command can clip a
                // buffer straddling the boundary before it reaches the encoder.
                if (!_closing && packet->hostTime + double(packet->frames) / packet->sampleRate > now - 0.15) break;
                if (!track.input.readyForMoreMediaData) break;
                [self append:*packet track:track];
            }
            track.packets.pop();
        }
        if (track.packets.front()) empty = NO;
    }
    if (!_closing || !_unpublished || publishers.load() != 0 || !empty || !_completion) return;
    if (_writer.status == AVAssetWriterStatusWriting)
        for (AVAssetWriterInput* input in _inputs) [input markAsFinished];
    NSLog(@"[RecorderAudio] DRAINED dropped=%d failed=%d", self.droppedPackets, self.failed);
    dispatch_source_cancel(_timer);
    _timer = nil;
    auto done = _completion;
    _completion = nil;
    if (done) done();
}
@end
