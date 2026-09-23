#import <ARKit/ARKit.h>
#import "HoloKitARKitNativeProvider.h"

// Native pose and background reference for the iOS 27 compatibility path.
extern "C" int HoloKit_GetARCameraReference(void* nativeSession, int orientation,
    int width, int height, float* view, float* projection, float* display) {
    if (!nativeSession || orientation < 1 || orientation > 4) return 0;
    ARSession* session = (__bridge ARSession*)((UnityXRNativeSession*)nativeSession)->sessionPtr;
    ARFrame* frame = session.currentFrame;
    if (!frame) return 0;
    // Unity's landscape names are the reverse of UIKit's (OrientationSupport.mm).
    UIInterfaceOrientation pose = orientation == 3 ? UIInterfaceOrientationLandscapeRight
        : orientation == 4 ? UIInterfaceOrientationLandscapeLeft
        : (UIInterfaceOrientation)orientation;
    simd_float4x4 v = [frame.camera viewMatrixForOrientation:pose];
    simd_float4x4 p = [frame.camera projectionMatrixForOrientation:pose
        viewportSize:CGSizeMake(width, height) zNear:.1 zFar:20];
    memcpy(view, &v, sizeof(v));
    memcpy(projection, &p, sizeof(p));
    CGAffineTransform d = [frame displayTransformForOrientation:pose viewportSize:CGSizeMake(width,height)];
    display[0]=d.a; display[1]=d.b; display[2]=d.c; display[3]=d.d; display[4]=d.tx; display[5]=d.ty;
    display[6]=CVPixelBufferGetWidth(frame.capturedImage);
    display[7]=CVPixelBufferGetHeight(frame.capturedImage);
    return 1;
}
