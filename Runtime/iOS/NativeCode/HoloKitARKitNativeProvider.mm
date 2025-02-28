// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#import "HoloKitARKitNativeProvider.h"

@interface HoloKitARKitNativeProvider()

@end

@implementation HoloKitARKitNativeProvider

- (instancetype)init {
    if (self = [super init]) {
        
    }
    return self;
}

+ (float *)getUnityMatrix:(simd_float4x4)arkitMatrix {
    float *unityMatrix = new float[16] { arkitMatrix.columns[1].x, -arkitMatrix.columns[0].x, -arkitMatrix.columns[2].x, arkitMatrix.columns[3].x,                                                 arkitMatrix.columns[1].y, -arkitMatrix.columns[0].y, -arkitMatrix.columns[2].y, arkitMatrix.columns[3].y,                                                -arkitMatrix.columns[1].z, arkitMatrix.columns[0].z, arkitMatrix.columns[2].z, -arkitMatrix.columns[3].z,                                                  arkitMatrix.columns[0].w, arkitMatrix.columns[1].w, arkitMatrix.columns[2].w, arkitMatrix.columns[3].w };
    return unityMatrix;
}

+ (simd_float4x4)getSimdFloat4x4WithPosition:(float [3])position rotation:(float [4])rotation {
    simd_float4x4 transform_matrix = matrix_identity_float4x4;
    float converted_rotation[4];
    // The structure of converted_rotation is { w, x, y, z }
    converted_rotation[0] = rotation[3];
    converted_rotation[1] = -rotation[0];
    converted_rotation[2] = -rotation[1];
    converted_rotation[3] = rotation[2];
    // Convert quaternion to rotation matrix
    // See: https://automaticaddison.com/how-to-convert-a-quaternion-to-a-rotation-matrix/
    transform_matrix.columns[0].x = 2 * (converted_rotation[0] * converted_rotation[0] + converted_rotation[1] * converted_rotation[1]) - 1;
    transform_matrix.columns[0].y = 2 * (converted_rotation[1] * converted_rotation[2] + converted_rotation[0] * converted_rotation[3]);
    transform_matrix.columns[0].z = 2 * (converted_rotation[1] * converted_rotation[3] - converted_rotation[0] * converted_rotation[2]);
    transform_matrix.columns[1].x = 2 * (converted_rotation[1] * converted_rotation[2] - converted_rotation[0] * converted_rotation[3]);
    transform_matrix.columns[1].y = 2 * (converted_rotation[0] * converted_rotation[0] + converted_rotation[2] * converted_rotation[2]) - 1;
    transform_matrix.columns[1].z = 2 * (converted_rotation[2] * converted_rotation[3] + converted_rotation[0] * converted_rotation[1]);
    transform_matrix.columns[2].x = 2 * (converted_rotation[1] * converted_rotation[3] + converted_rotation[0] * converted_rotation[2]);
    transform_matrix.columns[2].y = 2 * (converted_rotation[2] * converted_rotation[3] - converted_rotation[0] * converted_rotation[1]);
    transform_matrix.columns[2].z = 2 * (converted_rotation[0] * converted_rotation[0] + converted_rotation[3] * converted_rotation[3]) - 1;
    // Convert translate into matrix
    transform_matrix.columns[3].x = position[0];
    transform_matrix.columns[3].y = position[1];
    transform_matrix.columns[3].z = -position[2];
    return transform_matrix;
}

- (void)setVideoEnhancement:(bool)enabled {
    if (@available(iOS 16, *)) {
        ARWorldTrackingConfiguration *config = (ARWorldTrackingConfiguration *)self.session.configuration;
        if (ARWorldTrackingConfiguration.recommendedVideoFormatFor4KResolution == nil) {
            NSLog(@"[HoloKitARKitNativeProvider] Current device does not support 4K resolution video format.");
            return;
        }
        
        if (!enabled) {
            config.videoFormat = ARWorldTrackingConfiguration.supportedVideoFormats[0];
            config.videoHDRAllowed = false;
            [self.session runWithConfiguration:config];
            NSLog(@"[HoloKitARKitNativeProvider] AR background video enhancement disabled.");
            return;
        }
        
        config.videoFormat = ARWorldTrackingConfiguration.recommendedVideoFormatFor4KResolution;
        if (!config.videoFormat.videoHDRSupported) {
            config.videoHDRAllowed = true;
        } else {
            NSLog(@"[HoloKitARKitNativeProvider] Current device does not support HDR video format, only 4K resolution is being used.");
        }
        
        [self.session runWithConfiguration:config];
        NSLog(@"[HoloKitARKitNativeProvider] AR background video enhancement enabled.");
    } else {
        NSLog(@"[HoloKitARKitNativeProvider] Video enhancement is only supported on iOS 16 or higher.");
    }
}

#pragma mark - ARSessionDelegate

- (void)session:(ARSession *)session didUpdateFrame:(ARFrame *)frame {
    if (self.unityARSessionDelegate != NULL) {
        [self.unityARSessionDelegate session:session didUpdateFrame:frame];
    }
    
    if (self.onARSessionUpdatedFrame != NULL) {
        float *matrix = [HoloKitARKitNativeProvider getUnityMatrix:frame.camera.transform];
        double timestamp = frame.timestamp;
        dispatch_async(dispatch_get_main_queue(), ^{
            self.onARSessionUpdatedFrame((__bridge void *)self, timestamp, matrix);
            delete[](matrix);
        });
    }
}

- (void)session:(ARSession *)session didAddAnchors:(NSArray<__kindof ARAnchor*>*)anchors {
    if (self.unityARSessionDelegate != NULL) {
        [self.unityARSessionDelegate session:session didAddAnchors:anchors];
    }
}

- (void)session:(ARSession *)session didUpdateAnchors:(NSArray<__kindof ARAnchor*>*)anchors {
    if (self.unityARSessionDelegate != NULL) {
        [self.unityARSessionDelegate session:session didUpdateAnchors:anchors];
    }
}

- (void)session:(ARSession *)session didRemoveAnchors:(NSArray<__kindof ARAnchor*>*)anchors {
    if (self.unityARSessionDelegate != NULL) {
        [self.unityARSessionDelegate session:session didRemoveAnchors:anchors];
    }
}

- (void)session:(ARSession *)session didOutputCollaborationData:(ARCollaborationData *)data {
    if (self.unityARSessionDelegate != NULL) {
        [self.unityARSessionDelegate session:session didOutputCollaborationData:data];
    }
}

@end
