#import "EnvironmentDepthManager.h"

@interface EnvironmentDepthManager()

@property (nonatomic, strong) ARSession *arSession;

@end

@implementation EnvironmentDepthManager

- (instancetype)initWithARSession:(ARSession *)arSession {
    if (self = [super init]) {
        self.arSession = arSession;
    }
    return self;
}

- (float)getDepthWithX:(float)x y:(float)y {
    if (@available(iOS 14.0, *)) {
        if (self.arSession.currentFrame.sceneDepth == nil) {
            NSLog(@"Failed to get scene depth");
            return 0;
        }
        
        CVPixelBufferLockBaseAddress(self.arSession.currentFrame.sceneDepth.depthMap, 0);
        size_t depthBufferWidth = CVPixelBufferGetWidth(self.arSession.currentFrame.sceneDepth.depthMap);
        size_t depthBufferHeight = CVPixelBufferGetHeight(self.arSession.currentFrame.sceneDepth.depthMap);
        Float32 *depthBufferBaseAddress = (Float32 *)CVPixelBufferGetBaseAddress(self.arSession.currentFrame.sceneDepth.depthMap);
        
        int depthMapX = x * depthBufferWidth;
        int depthMapY = (1 - y) * depthBufferHeight;
        float depth = (float)depthBufferBaseAddress[depthMapY * depthBufferWidth + depthMapX];
        
        CVPixelBufferUnlockBaseAddress(self.arSession.currentFrame.sceneDepth.depthMap, 0);
        return depth;
    } else {
        return 0;
    }
}

- (simd_float3)unprojectScreenPointWithLocationX:(float)locationX locationY:(float)locationY depth:(float)depth {
    CGFloat screenX = (CGFloat)locationX * self.arSession.currentFrame.camera.imageResolution.width;
    CGFloat screenY = (CGFloat)(1 - locationY) * self.arSession.currentFrame.camera.imageResolution.height;
    CGPoint screenPoint = CGPointMake(screenX, screenY);
    simd_float3 unprojectedPoint = [self unprojectScreenPoint:screenPoint depth:depth];
    return simd_make_float3(unprojectedPoint.x, unprojectedPoint.y, -unprojectedPoint.z);
}

- (simd_float3)unprojectScreenPoint:(CGPoint)screenPoint depth:(float)depth {
    simd_float4x4 translation = matrix_identity_float4x4;
    translation.columns[3].z = -depth;
    simd_float4x4 planeOrigin = simd_mul(self.arSession.currentFrame.camera.transform, translation);
    simd_float3 xAxis = simd_make_float3(1, 0, 0);
    simd_float4x4 rotation = simd_matrix4x4(simd_quaternion(0.5 * M_PI, xAxis));
    simd_float4x4 plane = simd_mul(planeOrigin, rotation);
    simd_float3 unprojectedPoint = [self.arSession.currentFrame.camera unprojectPoint:screenPoint ontoPlaneWithTransform:plane orientation:UIInterfaceOrientationLandscapeRight viewportSize:self.arSession.currentFrame.camera.imageResolution];
    return unprojectedPoint;
}

@end
