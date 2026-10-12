// SPDX-License-Identifier: MIT
#import "HoloKitAudioCapture.h"
#include <TargetConditionals.h>
#import <AVFoundation/AVFoundation.h>
#import <mach/mach_time.h>
double HoloKit_AudioClockTime() { return [AVAudioTime secondsForHostTime:mach_absolute_time()]; }
#if TARGET_OS_IOS
#import <Accelerate/Accelerate.h>
#import <mach/mach_time.h>
#include <array>
#include <atomic>
#include <algorithm>
#include <cmath>

namespace {
constexpr int RingSize = 96000;
constexpr int SpectrumSize = 2048;
std::array<std::atomic<float>, RingSize> microphoneSamples;
std::atomic<uint64_t> microphoneWritten {0};
std::atomic<int> microphoneRate {0};
std::atomic<bool> missingHostTime {false};
std::atomic<HoloKitAudioSink> recordingSink {nullptr};
}

@interface HoloKitMicrophone : NSObject
@property(nonatomic, strong) AVAudioEngine* engine;
@property(nonatomic, copy) NSString* error;
@property(nonatomic, copy) NSString* previousCategory;
@property(nonatomic, copy) NSString* previousMode;
@property(nonatomic) AVAudioSessionCategoryOptions previousOptions;
@property(nonatomic) int processing; // 0 raw (Measurement), 1 echo cancellation, 2 iOS default (system gain)
@property(nonatomic) BOOL tapInstalled;
@property(nonatomic) NSUInteger users;
@end

@implementation HoloKitMicrophone
- (void)restoreSession {
    if (!self.previousCategory) return;
    AVAudioSession* session = AVAudioSession.sharedInstance;
    // Do not deactivate the shared session: Unity may still be playing game audio.
    [session setCategory:self.previousCategory mode:self.previousMode
                 options:self.previousOptions error:nil];
    self.previousCategory = nil;
    self.previousMode = nil;
}
- (void)stop {
    [self.engine stop];
    if (self.tapInstalled) [self.engine.inputNode removeTapOnBus:0];
    self.tapInstalled = NO;
    self.engine = nil;
    microphoneRate.store(0, std::memory_order_release);
    microphoneWritten.store(0, std::memory_order_release);
    [self restoreSession];
}
- (BOOL)fail:(NSString*)message {
    self.error = message ?: @"Microphone capture failed.";
    [self stop];
    return NO;
}
- (BOOL)start:(int)processing {
    const BOOL aec = processing == 1;
    const BOOL voice = processing == 3;  // voice processing with automatic gain
    if (self.engine.isRunning) {
        if (processing == self.processing) return YES;
        self.error = @"Cannot change microphone processing while the shared input is running.";
        return NO;
    }
    [self stop];
    self.error = nil;
    AVAudioSession* session = AVAudioSession.sharedInstance;
    self.previousCategory = session.category;
    self.previousMode = session.mode;
    self.previousOptions = session.categoryOptions;
    NSError* error = nil;
    AVAudioSessionCategoryOptions options = AVAudioSessionCategoryOptionDefaultToSpeaker |
        AVAudioSessionCategoryOptionAllowBluetoothHFP | AVAudioSessionCategoryOptionMixWithOthers;
    if (![session setCategory:AVAudioSessionCategoryPlayAndRecord
                        mode:aec || voice ? AVAudioSessionModeVoiceChat
                            : processing == 2 ? AVAudioSessionModeDefault : AVAudioSessionModeMeasurement
                     options:options error:&error]) return [self fail:error.localizedDescription];
    if (![session setActive:YES error:&error]) return [self fail:error.localizedDescription];

    self.engine = [AVAudioEngine new];
    AVAudioInputNode* input = self.engine.inputNode;
    // Setting the chat session mode alone does NOT enable echo cancellation.
    if (aec || voice) {
        if (![input setVoiceProcessingEnabled:YES error:&error] || !input.isVoiceProcessingEnabled)
            return [self fail:error.localizedDescription ?: @"Voice processing is unavailable."];
        input.voiceProcessingAGCEnabled = voice;
        if (@available(iOS 17.0, *)) {
            AVAudioVoiceProcessingOtherAudioDuckingConfiguration ducking = {
                .enableAdvancedDucking = NO, .duckingLevel = AVAudioVoiceProcessingOtherAudioDuckingLevelMin
            };
            input.voiceProcessingOtherAudioDuckingConfiguration = ducking;
        }
    }
    self.processing = processing;
    AVAudioFormat* format = [input outputFormatForBus:0];
    if (!format.channelCount || format.sampleRate <= 0 || format.commonFormat != AVAudioPCMFormatFloat32)
        return [self fail:@"Microphone has no supported floating-point input format."];
    microphoneRate.store((int)format.sampleRate, std::memory_order_release);
    microphoneWritten.store(0, std::memory_order_release);
    missingHostTime.store(false);
    // Input is tapped but never connected to the output: no microphone monitoring.
    [input installTapOnBus:0 bufferSize:1024 format:format block:^(AVAudioPCMBuffer* buffer, AVAudioTime* time) {
        if (!time.isHostTimeValid) { missingHostTime.store(true); return; }
        if (!buffer.floatChannelData) return;
        const int channels = (int)buffer.format.channelCount;
        const int rate = (int)buffer.format.sampleRate;
        const double began = [AVAudioTime secondsForHostTime:time.hostTime];
        float mono[1024];
        for (int offset = 0; offset < (int)buffer.frameLength; offset += 1024) {
            int frames = std::min(1024, (int)buffer.frameLength - offset);
            for (int i = 0; i < frames; i++) {
                float sum = 0;
                for (int channel = 0; channel < channels; channel++)
                    sum += buffer.format.isInterleaved
                        ? buffer.floatChannelData[0][(offset + i) * channels + channel]
                        : buffer.floatChannelData[channel][offset + i];
                mono[i] = sum / channels;
            }
            uint64_t cursor = microphoneWritten.load(std::memory_order_relaxed);
            for (int i = 0; i < frames; i++)
                microphoneSamples[(cursor + i) % RingSize].store(mono[i], std::memory_order_relaxed);
            microphoneWritten.store(cursor + frames, std::memory_order_release);
            if (auto sink = recordingSink.load(std::memory_order_acquire))
                sink(mono, frames, 1, rate, began + double(offset) / rate);
        }
    }];
    self.tapInstalled = YES;
    [self.engine prepare];
    if (![self.engine startAndReturnError:&error]) return [self fail:error.localizedDescription];
    NSLog(@"[Microphone] processing=%d session mode=%@", processing, session.mode);
    NSLog(@"[Microphone] latency input=%.6f output=%.6f IO=%.6f node=%.6f inputPresentation=%.6f outputPresentation=%.6f AGC=%d",
        session.inputLatency, session.outputLatency, session.IOBufferDuration, input.latency,
        input.presentationLatency, aec ? self.engine.outputNode.presentationLatency : session.outputLatency,
        aec || voice ? input.voiceProcessingAGCEnabled : NO);
    NSLog(@"[Microphone] running aec=%d bypass=%d rate=%.0f input=%@ output=%@ volume=%.3f inputGain=%.3f",
        input.isVoiceProcessingEnabled, input.isVoiceProcessingBypassed, format.sampleRate,
        session.currentRoute.inputs, session.currentRoute.outputs, session.outputVolume, session.inputGain);
    return YES;
}
@end

static HoloKitMicrophone* Microphone() {
    static HoloKitMicrophone* microphone;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ microphone = [HoloKitMicrophone new]; });
    return microphone;
}

int HoloKit_MicrophonePermission() {
    if (@available(iOS 17.0, *)) {
        switch (AVAudioApplication.sharedInstance.recordPermission) {
            case AVAudioApplicationRecordPermissionGranted: return 1;
            case AVAudioApplicationRecordPermissionDenied: return -1;
            default: return 0;
        }
    }
    switch (AVAudioSession.sharedInstance.recordPermission) {
        case AVAudioSessionRecordPermissionGranted: return 1;
        case AVAudioSessionRecordPermissionDenied: return -1;
        default: return 0;
    }
}
void HoloKit_MicrophoneRequestPermission() {
    if (@available(iOS 17.0, *)) [AVAudioApplication requestRecordPermissionWithCompletionHandler:^(BOOL granted) {}];
    else [AVAudioSession.sharedInstance requestRecordPermission:^(BOOL granted) {}];
}
int HoloKit_MicrophoneStart(int processing) {
    if (processing < 0 || processing > 2) return -1;
    if (![Microphone() start:processing]) return -1;
    Microphone().users++;
    return 0;
}
int HoloKit_MicrophoneRetain() {
    if (!Microphone().engine.isRunning) return -1;
    Microphone().users++;
    return 0;
}
void HoloKit_MicrophoneStop() {
    HoloKitMicrophone* microphone = Microphone();
    if (microphone.users && --microphone.users == 0) [microphone stop];
}
int HoloKit_MicrophoneIsRunning() { return Microphone().engine.isRunning ? 1 : 0; }
int HoloKit_MicrophoneSampleRate() { return microphoneRate.load(std::memory_order_acquire); }
int HoloKit_MicrophoneBufferedSamples() { return (int)std::min(uint64_t(RingSize), microphoneWritten.load(std::memory_order_acquire)); }
const char* HoloKit_MicrophoneLastError() {
    return missingHostTime.load() ? "Microphone host timestamps are unavailable." : Microphone().error.UTF8String;
}
void HoloKit_MicrophoneSetRecordingSink(HoloKitAudioSink sink) { recordingSink.store(sink, std::memory_order_release); }
int HoloKit_MicrophoneReadLatest(float* destination, int count) {
    if (!destination || count <= 0 || count > RingSize) return 0;
    uint64_t end = microphoneWritten.load(std::memory_order_acquire);
    int copied = (int)std::min(uint64_t(count), end);
    std::fill(destination, destination + count - copied, 0);
    for (int i = 0; i < copied; i++)
        destination[count - copied + i] = microphoneSamples[(end - copied + i) % RingSize].load(std::memory_order_relaxed);
    if (microphoneWritten.load(std::memory_order_acquire) - end > uint64_t(RingSize - copied)) {
        std::fill(destination, destination + count, 0);
        return 0;
    }
    return copied;
}
int HoloKit_MicrophoneReadSpectrum(float* destination, int bins) {
    if (!destination || bins != SpectrumSize / 2) return 0;
    // Analysis runs on the caller's main thread, never on the audio callback.
    static FFTSetup fft = vDSP_create_fftsetup(11, kFFTRadix2);
    float samples[SpectrumSize], real[SpectrumSize / 2], imag[SpectrumSize / 2];
    if (!fft || !HoloKit_MicrophoneReadLatest(samples, SpectrumSize)) {
        std::fill(destination, destination + bins, 0);
        return 0;
    }
    static const auto window = [] {
        std::array<float, SpectrumSize> values;
        for (int i = 0; i < SpectrumSize; i++) {
            double phase = 2 * M_PI * i / (SpectrumSize - 1);
            values[i] = float(0.35875 - 0.48829 * std::cos(phase) +
                0.14128 * std::cos(2 * phase) - 0.01168 * std::cos(3 * phase));
        }
        return values;
    }();
    vDSP_vmul(samples, 1, window.data(), 1, samples, 1, SpectrumSize);
    DSPSplitComplex split = {real, imag};
    vDSP_ctoz((DSPComplex*)samples, 2, &split, 1, SpectrumSize / 2);
    vDSP_fft_zrip(fft, &split, 1, 11, FFT_FORWARD);
    imag[0] = 0;
    vDSP_zvabs(&split, 1, destination, 1, bins);
    float scale = 1.0f / SpectrumSize;
    vDSP_vsmul(destination, 1, &scale, destination, 1, bins);
    return bins;
}
#endif
