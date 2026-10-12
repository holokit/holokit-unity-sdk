// SPDX-License-Identifier: MIT
#pragma once
#ifdef __cplusplus
extern "C" {
#endif
// Host time is seconds on the same monotonic clock as AVAudioTime.hostTime.
typedef void (*HoloKitAudioSink)(const float*, int frames, int channels, int sampleRate, double hostTime);
double HoloKit_AudioClockTime(void);
// Permission: 0 not determined, 1 granted, -1 denied. Request is asynchronous.
int HoloKit_MicrophonePermission(void);
void HoloKit_MicrophoneRequestPermission(void);
// processing: 0 raw (Measurement mode), 1 echo cancellation, 2 iOS default, 3 voice (automatic gain).
int HoloKit_MicrophoneStart(int processing);
// Built-in microphone for the next start: 0 automatic, 1 bottom, 2 front, 3 back (omnidirectional
// where supported). iOS ignores the choice while voice processing (1, 3) owns the input.
void HoloKit_MicrophoneSetPosition(int position);
void HoloKit_MicrophoneStop(void);
int HoloKit_MicrophoneIsRunning(void);
int HoloKit_MicrophoneSampleRate(void);
int HoloKit_MicrophoneBufferedSamples(void);
int HoloKit_MicrophoneRetain(void);
const char* HoloKit_MicrophoneLastError(void);
int HoloKit_MicrophoneReadLatest(float* destination, int count);
int HoloKit_MicrophoneReadSpectrum(float* destination, int bins);
void HoloKit_MicrophoneSetRecordingSink(HoloKitAudioSink sink);
#ifdef __cplusplus
}
#endif
