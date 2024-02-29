// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
// SPDX-License-Identifier: MIT

#import "VideoRecorder.h"

#include <TargetConditionals.h>

#if TARGET_OS_IOS
#import <UIKit/UIKit.h>
#endif

#import <AVFoundation/AVFoundation.h>
#import <Metal/Metal.h>
#import <CoreMedia/CMBlockBuffer.h>
#import <Accelerate/Accelerate.h>
#import <AudioToolbox/AudioToolbox.h>

@interface VideoRecorder ()

@property (nonatomic, strong) AVAssetWriter* writer;
@property (nonatomic, strong) AVAssetWriterInput* videoWriterInput;
@property (nonatomic, strong) AVAssetWriterInput* audioWriterInput;
@property (nonatomic, strong) AVAssetWriterInputPixelBufferAdaptor* bufferAdaptor;
@property (assign) AudioStreamBasicDescription audioFormat;
@property (assign) CMFormatDescriptionRef cmFormat;
@property (nonatomic, strong) AVCaptureSession* captureSession;
@property (assign) bool isRecording;
@property (assign) int width;
@property (assign) int height;
@property (nonatomic, strong) AVCaptureDevice* audioDeviceMic;
@property (nonatomic, strong) AVCaptureDeviceInput* audioInputMic;
@property (nonatomic, strong) AVAudioEngine* audioEngine;
@property (nonatomic, strong) AVCaptureAudioDataOutput* audioOutput;

@end

@implementation VideoRecorder

- (instancetype)init {
    if (self = [super init]) {
    }
    return self;
}

+ (id)sharedInstance {
    static dispatch_once_t onceToken = 0;
    static id _sharedObject = nil;
    dispatch_once(&onceToken, ^{
        _sharedObject = [[self alloc] init];
    });
    return _sharedObject;
}

// - (void) startMicrophone {
//     self.captureSession = [[AVCaptureSession alloc] init];
//     [self.captureSession setSessionPreset:AVCaptureSessionPresetHigh];
//     self.audioDeviceMic = [AVCaptureDevice defaultDeviceWithMediaType:AVMediaTypeAudio];
//     self.audioInputMic = [AVCaptureDeviceInput deviceInputWithDevice:self.audioDeviceMic error:nil];
//     if ([self.captureSession canAddInput:self.audioInputMic]) {
//         [self.captureSession addInput:self.audioInputMic];
//     }
//     self.audioOutput = [[AVCaptureAudioDataOutput alloc] init];
//     [self.audioOutput setSampleBufferDelegate:self queue:dispatch_get_main_queue()];
//      if ([self.captureSession canAddOutput:self.audioOutput]) {
//         [self.captureSession addOutput:self.audioOutput];
//     }

//     // Initialize AVAudioEngine
//     self.audioEngine = [[AVAudioEngine alloc] init];
//     self.outputNode = [self.audioEngine outputNode];
//     [self.outputNode setVoiceProcessingEnabled:YES];
//     self.outputFormat = [outputNode inputFormatForBus: 0];
//     playerNode = [[AVAudioPlayerNode alloc] init];
//     [self.audioEngine attachNode: playerNode];
//     AVAudioMixerNode *mixerNode = [self.audioEngine mainMixerNode];
//     [self.audioEngine connect:inputNode to:mixerNode format:inputFormat];
//     NSArray<AVAudioSessionPortDescription *> *inputPorts = [audioSession availableInputs];

//     for (AVAudioSessionPortDescription *port in inputPorts) {
//         if ([port.portType isEqualToString:AVAudioSessionPortUSBAudio]) {
//             usbPort = port;
//             break;
//         }
//     }
//     NSError *inputError = nil;
//     [audioSession setPreferredInput:usbPort error:&inputError];


//     [self.outputNode installTapOnBus:0
//           bufferSize:1024
//           format:recordingFormat
//           block:^(AVAudioPCMBuffer *buffer, AVAudioTime *when) {
//                    [request appendBuffer:buffer];
                   
//     }];
    
//     [self.audioEngine prepare];
//     [self.audioEngine start];

//     [engine connect: playerNode to: mainMixerNode format: outputFormat];

//     // Create AVAudioPlayerNodes for each microphone
//     AVAudioPlayerNode *playerNode1 = [[AVAudioPlayerNode alloc] init];

// //    self.mixerNode = [self.audioEngine mainMixerNode];
//     [self.audioEngine attachNode:playerNode1];
//     [self.audioEngine attachNode:playerNode2];
    
// //    [self.audioEngine connect:playerNode1 to:self.mixerNode format:nil];
// //    [self.audioEngine connect:playerNode2 to:self.mixerNode format:nil];
// //    NSError *audioEngineError;
// //    [self.audioEngine startAndReturnError:&audioEngineError];
// //    if (audioEngineError) {
// //        NSLog(@"Error starting AVAudioEngine: %@", audioEngineError.localizedDescription);
// //    }
// //
//    AVAudioFormat *audioOutputFormat = [self.audioEngine.outputNode outputFormatForBus:0];
//    CMFormatDescriptionRef audioFormatDescription = CMAudioFormatDescriptionCreate(audioOutputFormat.streamDescription,
//        sizeof(AudioStreamBasicDescription), kCMAudioFormatFlags_IsFloat);

//     // Start AVCaptureSession
//     [self.captureSession startRunning];
// }

- (int)startRecording:(const char *)filePath width:(int)width height:(int)height audioSampleRate:(float)audioSampleRate audioChannelCount:(int)audioChannelCount
{
    if (self.writer)
    {
        NSLog(@"Recording has already been initiated.");
        return -1;
    }
    
    // Asset writer setup
    NSURL* filePathURL = [NSURL fileURLWithPath:[NSString stringWithUTF8String:filePath]];
    
    NSError* err;
    self.writer = [[AVAssetWriter alloc] initWithURL: filePathURL
                                        fileType: AVFileTypeMPEG4
                                           error: &err];
    
    if (err)
    {
        NSLog(@"Failed to initialize AVAssetWriter (%@)", err);
        return -1;
    }
    
    NSDictionary* settings = @{
        AVVideoCodecKey: AVVideoCodecTypeHEVC,
        AVVideoWidthKey: @(width),
        AVVideoHeightKey: @(height) };
    
    self.videoWriterInput = [AVAssetWriterInput assetWriterInputWithMediaType: AVMediaTypeVideo outputSettings: settings];
    self.videoWriterInput.expectsMediaDataInRealTime = YES;

    // Pixel buffer adaptor setup
    NSDictionary* attribs = @{
        (NSString*)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
        (NSString*)kCVPixelBufferWidthKey: @(width),
        (NSString*)kCVPixelBufferHeightKey: @(height)
    };
    
    self.bufferAdaptor = [AVAssetWriterInputPixelBufferAdaptor
                      assetWriterInputPixelBufferAdaptorWithAssetWriterInput: self.videoWriterInput
                      sourcePixelBufferAttributes: attribs];
    
    // Audio writer input setup
    NSDictionary* audioSettings = @{
        AVFormatIDKey: @(kAudioFormatMPEG4AAC),
        AVSampleRateKey: @(audioSampleRate),
        AVNumberOfChannelsKey: @(audioChannelCount),
        AVEncoderAudioQualityKey: @(AVAudioQualityHigh)
    };
    self.audioWriterInput = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeAudio
                         outputSettings:audioSettings];
    self.audioWriterInput.expectsMediaDataInRealTime = YES;
    
    AudioStreamBasicDescription audioFormat;
    audioFormat = {0};
    audioFormat.mSampleRate = audioSampleRate; // Sample rate, 44100Hz is CD quality
    audioFormat.mFormatID = kAudioFormatLinearPCM; // Specify the data format to be PCM
    audioFormat.mFormatFlags = kLinearPCMFormatFlagIsFloat; // Flags specific for the format
    audioFormat.mFramesPerPacket = 1; // Each packet contains one frame for PCM data
    audioFormat.mChannelsPerFrame = (uint32_t) audioChannelCount; // Set the number of channels
    audioFormat.mBitsPerChannel = sizeof(float) * 8; // Number of bits per channel, 32 for float
    audioFormat.mBytesPerFrame = (uint32_t) audioChannelCount * sizeof(float); // Bytes per frame
    audioFormat.mBytesPerPacket = audioFormat.mBytesPerFrame * audioFormat.mFramesPerPacket; // Bytes per packet
    audioFormat.mReserved = 0;
    self.audioFormat = audioFormat;
    
    CMFormatDescriptionRef cmFormat;
    OSStatus status = CMAudioFormatDescriptionCreate(kCFAllocatorDefault,
                                   &audioFormat,
                                   0,
                                   NULL,
                                   0,
                                   NULL,
                                   NULL,
                                   &cmFormat
                                   );
    
    if (status != noErr) {
        NSLog(@"CMAudioFormatDescriptionCreate hasn't been initiated.");
        return -1;
    }
    self.cmFormat = cmFormat;
   
    [self.writer addInput:self.videoWriterInput];
    [self.writer addInput:self.audioWriterInput];
    
    // Recording start
    if (![self.writer startWriting])
    {
        NSLog(@"Failed to start (%ld: %@)", self.writer.status, self.writer.error);
        return -1;
    }

    self.width = width;
    self.height = height;
    [self.writer startSessionAtSourceTime:kCMTimeZero];
    self.isRecording = YES;

    return 0;
}

- (int)appendAudioFrame:(void *)source size:(int)size time:(double)time
{
    if (!self.isRecording) {
        return -2;
    }

    if (!self.writer)
    {
        NSLog(@"Recording hasn't been initiated.");
        return -1;
    }

    if (!self.audioWriterInput.isReadyForMoreMediaData)
    {
        NSLog(@"Audio Writer is not ready.");
        return -1;
    }

    // Write _audioInputWriter with buffer

    CMTime presentationTimestamp = CMTimeMakeWithSeconds(time, 240); // Adjust timescale as needed
    
    CMBlockBufferRef blockBuffer;
    OSStatus status = CMBlockBufferCreateWithMemoryBlock(kCFAllocatorDefault, NULL, size,
                                                         kCFAllocatorDefault, NULL, 0, size,
                                                         kCMBlockBufferAssureMemoryNowFlag, &blockBuffer);
    
    if (status != kCMBlockBufferNoErr) {
        NSLog(@"CMBlockBufferCreateWithMemoryBlock error");
        return -1;
    }
    
    status = CMBlockBufferReplaceDataBytes(source, blockBuffer, 0, size);
    
    if (status != kCMBlockBufferNoErr) {
        NSLog(@"CMBlockBufferReplaceDataBytes error");
        return -1;
    }

    CMItemCount nSamples = size / self.audioFormat.mBytesPerFrame;
    
    CMSampleBufferRef sampleBuffer;
    status = CMAudioSampleBufferCreateReadyWithPacketDescriptions(kCFAllocatorDefault,
                                                                blockBuffer,
                                                                self.cmFormat,
                                                                nSamples,
                                                                presentationTimestamp,
                                                                NULL,
                                                                &sampleBuffer);
    
    if (status != noErr) {
        CFRelease(blockBuffer);
        return -1;
    }

    if (!CMSampleBufferDataIsReady(sampleBuffer))
    {
        NSLog(@"sample buffer is not ready");
        return -1;
    }
   
    if (!CMSampleBufferIsValid(sampleBuffer))
    {
        NSLog(@"Audio sapmle buffer is not valid");
        return -1;
    }
        
    bool result = [self.audioWriterInput appendSampleBuffer:sampleBuffer];
    if (!result) {
        NSLog(@"appendSampleBuffer error");
        return -1;
    }
    
    CFRelease(sampleBuffer);
    CFRelease(blockBuffer);
    return 0;
}

- (int)appendVideoFrame:(void *)source size:(int)size time:(double)time
{
    if (!self.isRecording) {
        return -2;
    }

    if (!self.writer)
    {
        NSLog(@"Recording hasn't been initiated.");
        return -1;
    }
    
    if (!self.videoWriterInput.isReadyForMoreMediaData)
    {
        NSLog(@"Video Writer is not ready.");
        return -1;
    }
    
    if (!self.bufferAdaptor.pixelBufferPool)
    {
        NSLog(@"Video Writer pixelBufferPool is empty.");
        return -1;
    }

    // Buffer allocation
    CVPixelBufferRef pixelBuffer = nil;
    CVReturn ret = CVPixelBufferPoolCreatePixelBuffer(NULL, self.bufferAdaptor.pixelBufferPool, &pixelBuffer);
    
    if (ret != kCVReturnSuccess)
    {
        NSLog(@"Can't allocate a pixel buffer (%d)", ret);
        NSLog(@"%ld: %@", self.writer.status, self.writer.error);
        return -1;
    }
    
    // Buffer copy
    CVPixelBufferLockBaseAddress(pixelBuffer, 0);
    void* baseAddress = CVPixelBufferGetBaseAddress(pixelBuffer);
    int bytesPerRow = (int) CVPixelBufferGetBytesPerRow(pixelBuffer);
    int bufferSize = (int) CVPixelBufferGetDataSize(pixelBuffer);
   
    vImage_Buffer srcBuffer;
    srcBuffer.data = source;
    srcBuffer.height = self.height;
    srcBuffer.width = self.width;
    srcBuffer.rowBytes = self.width * 4;

    vImage_Buffer dstBuffer;
    dstBuffer.data = baseAddress;
    dstBuffer.height = self.height;
    dstBuffer.width = self.width;
    dstBuffer.rowBytes = bytesPerRow;

    // ARGB to BGRA
    // ARGB
    // 0 denotes the alpha channel, 1 the red channel, 2 the green channel, and 3 the blue channel.
    const uint8_t permuteMap[4] = { 0, 1, 2, 3 };
    assert(size == self.width * self.height * 4);
    vImagePermuteChannels_ARGB8888(&srcBuffer, &dstBuffer, permuteMap, kvImageNoFlags);
    CVPixelBufferUnlockBaseAddress(pixelBuffer, 0);

    // Buffer submission
    [self.bufferAdaptor appendPixelBuffer:pixelBuffer
                    withPresentationTime:CMTimeMakeWithSeconds(time, 240)];
    
    CVPixelBufferRelease(pixelBuffer);

    return 0;
}

- (void)endRecording
{
    if (!self.isRecording) {
        return;
    }

    if (!self.writer)
    {
        NSLog(@"Recording hasn't been initiated.");
        return;
    }
    
    [self.videoWriterInput markAsFinished];
    [self.audioWriterInput markAsFinished];
    
#if TARGET_OS_IOS
    NSString* path = self.writer.outputURL.path;
    [self.writer finishWritingWithCompletionHandler: ^{
        if (UIVideoAtPathIsCompatibleWithSavedPhotosAlbum(path)) {
            UISaveVideoAtPathToSavedPhotosAlbum(path, nil, nil, nil);
        }
    }];
#else
    [self.writer finishWritingWithCompletionHandler: ^{}];
#endif
    self.writer = NULL;
    self.videoWriterInput = NULL;
    self.audioWriterInput = NULL;
    self.bufferAdaptor = NULL;
    self.isRecording = NO;
}

@end


extern "C" {

int HoloKitVideoRecorder_StartRecording(const char* filePath, int width, int height,
                                        float audioSampleRate, int audioChannelCount) {
    return [[VideoRecorder sharedInstance] startRecording:filePath width:width height:height audioSampleRate:audioSampleRate audioChannelCount:audioChannelCount];
}

int HoloKitVideoRecorder_AppendAudioFrame(void* source, int size, double time)
{
    return [[VideoRecorder sharedInstance] appendAudioFrame:source size:size time:time];
}

int HoloKitVideoRecorder_AppendVideoFrame(void* source, int size, double time)
{
    return [[VideoRecorder sharedInstance] appendVideoFrame:source size:size time:time];
}

void HoloKitVideoRecorder_EndRecording(void)
{
    [[VideoRecorder sharedInstance] endRecording];
}

}

