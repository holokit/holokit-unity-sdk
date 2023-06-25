#import "EnvironmentDepthManager.h"

void* HoloInteractiveHoloKit_EnvironmentDepthManager_initWithARSession(UnityXRNativeSession *arSessionPtr) {
    ARSession *arSession = (__bridge ARSession *)arSessionPtr->sessionPtr;
    EnvironmentDepthManager *depthManager = [[EnvironmentDepthManager alloc] initWithARSession: arSession];
    return (__bridge_retained void *)depthManager;
}

float HoloInteractiveHoloKit_EnvironmentDepthManager_getDepth(void *self, float x, float y) {
    EnvironmentDepthManager *depthManager = (__bridge EnvironmentDepthManager *)self;
    return [depthManager getDepthWithX: x y:y];
}

void HoloInteractiveHoloKit_EnvironmentDepthManager_unprojectScreenPoint(void *self, float locationX, float locationY, float depth, float *x, float *y, float *z) {
    EnvironmentDepthManager *depthManager = (__bridge EnvironmentDepthManager *)self;
    simd_float3 unprojectedPoint = [depthManager unprojectScreenPointWithLocationX:locationX locationY:locationY depth:depth];
    *x = unprojectedPoint.x;
    *y = unprojectedPoint.y;
    *z = unprojectedPoint.z;
}
