#import <ARKit/ARKit.h>
#import "HoloKitARKitNativeProvider.h"

// ARKit's own pose, projection and display transform for an interface orientation: the device
// probe's reference for AR Foundation's camera (A4.fov-*). HoloKitProbeBuild appends this file to
// the probe export's HoloKitARKitNativeProvider.mm; apps never contain it.
extern "C" int HoloKit_GetARCameraReference(void* nativeSession, int orientation,
    int width, int height, float* view, float* projection, float* display) {
    if (!nativeSession || orientation < 1 || orientation > 4) return 0;
    ARSession* session = (__bridge ARSession*)((UnityXRNativeSession*)nativeSession)->sessionPtr;
    ARFrame* frame = session.currentFrame;
    if (!frame) return 0;
    // ARKit's view rotation angle: the clockwise degrees that keep the camera image level, 0 at UIKit's
    // LandscapeRight, 90 Portrait, 180 LandscapeLeft, 270 PortraitUpsideDown. Unity's landscape names
    // are the reverse of UIKit's (OrientationSupport.mm).
    static const CGFloat angles[] = {0, 90, 270, 0, 180};
    const CGFloat angle = angles[orientation];
    const CGSize viewport = CGSizeMake(width, height);
    simd_float4x4 v = [frame.camera viewMatrixForViewRotationAngle:angle];
    simd_float4x4 p = [frame.camera projectionMatrixForViewRotationAngle:angle viewportSize:viewport zNear:.1 zFar:20];
    memcpy(view, &v, sizeof(v));
    memcpy(projection, &p, sizeof(p));
    CGAffineTransform d = [frame displayTransformForViewRotationAngle:angle viewportSize:viewport];
    display[0]=d.a; display[1]=d.b; display[2]=d.c; display[3]=d.d; display[4]=d.tx; display[5]=d.ty;
    display[6]=CVPixelBufferGetWidth(frame.capturedImage);
    display[7]=CVPixelBufferGetHeight(frame.capturedImage);
    return 1;
}
