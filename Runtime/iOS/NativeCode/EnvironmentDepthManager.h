#import <ARKit/ARKit.h>
#import "UnityXRInterface.h"

@interface EnvironmentDepthManager : NSObject

- (nonnull instancetype)initWithARSession:(ARSession *_Nonnull)arSession;
- (float)getDepthWithX:(float)x y:(float)y;
- (simd_float3)unprojectScreenPointWithLocationX:(float)locationX locationY:(float)locationY depth:(float)depth;

@end
