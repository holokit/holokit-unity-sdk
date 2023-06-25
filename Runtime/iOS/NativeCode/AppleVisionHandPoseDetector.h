#import <Vision/Vision.h>
#import <ARKit/ARKit.h>
#import "UnityXRInterface.h"

@interface AppleVisionHandPoseDetector : NSObject

- (nonnull instancetype)initWithARSession:(ARSession *_Nonnull)arSession maximumHandCount:(int)maximumHandCount;
- (BOOL)processCurrentFrame;
- (int)getResultCount;
- (VNRecognizedPoint *)getHandJointWithHandIndex:(int)handIndex jointIndex:(int)jointIndex;

@end
