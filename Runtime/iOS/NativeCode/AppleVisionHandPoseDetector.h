#import <Vision/Vision.h>
#import <ARKit/ARKit.h>
#import "UnityXRInterface.h"

typedef void (*OnHandPose2DUpdatedCallback)(void * _Nonnull, int, void * _Nullable);
typedef void (*OnHandPose3DUpdatedCallback)(void * _Nonnull, int, void * _Nullable);

@interface AppleVisionHandPoseDetector : NSObject

@property (nonatomic, assign) OnHandPose2DUpdatedCallback _Nullable onHandPose2DUpdatedCallback;
@property (nonatomic, assign) OnHandPose3DUpdatedCallback _Nullable onHandPose3DUpdatedCallback;

- (nonnull instancetype)initWithARSession:(ARSession *_Nonnull)arSession maximumHandCount:(int)maximumHandCount;
- (void)processCurrentFrame2D;
- (void)processCurrentFrame3D;

@end
