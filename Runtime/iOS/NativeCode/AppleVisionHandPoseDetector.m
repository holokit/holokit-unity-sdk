#import "AppleVisionHandPoseDetector.h"

@implementation AppleVisionHandPoseDetector

API_AVAILABLE(ios(14.0))
VNDetectHumanHandPoseRequest *m_HandPoseRequest;
ARSession *m_ARSession;

- (instancetype)initWithARSession:(ARSession *)arSession maximumHandCount:(int)maximumHandCount {
    if (self = [super init]) {
        if (@available(iOS 14.0, *)) {
            m_HandPoseRequest = [[VNDetectHumanHandPoseRequest alloc] init];
            m_HandPoseRequest.usesCPUOnly = false;
            m_HandPoseRequest.maximumHandCount = maximumHandCount;
            m_ARSession = arSession;
        } else {
            NSLog(@"Apple Vision framework is only available on iOS 14.0 or higher");
        }
    }
    return self;
}

- (BOOL)processCurrentFrame {
    if (@available(iOS 14.0, *)) {
        VNImageRequestHandler *imageRequestHandler = [[VNImageRequestHandler alloc] initWithCVPixelBuffer: m_ARSession.currentFrame.capturedImage orientation:kCGImagePropertyOrientationUp options:[NSMutableDictionary dictionary]];
        @try {
            NSArray<VNRequest *> *requests = [[NSArray alloc] initWithObjects:m_HandPoseRequest, nil];
            [imageRequestHandler performRequests:requests error:nil];
            return YES;
        } @catch(NSException *e) {
            return NO;
        }
    }
}

- (int)getResultCount {
    if (@available(iOS 14.0, *)) {
        return (int)m_HandPoseRequest.results.count;
    } else {
        return 0;
    }
}

- (VNRecognizedPoint *)getHandJointWithHandIndex:(int)handIndex jointIndex:(int)jointIndex  API_AVAILABLE(ios(14.0)){
    if (m_HandPoseRequest.results.count <= handIndex) {
        return nil;
    }
    VNHumanHandPoseObservation *observation = m_HandPoseRequest.results[handIndex];
    return [observation recognizedPointForJointName:[AppleVisionHandPoseDetector getVNHumanHandPoseObservationJointNameWithJointIndex:jointIndex] error:nil];
}

+ (VNRecognizedPointKey)getVNHumanHandPoseObservationJointNameWithJointIndex:(int)jointIndex {
    if (@available(iOS 14.0, *)) {
        switch(jointIndex) {
            case 0:
                return VNHumanHandPoseObservationJointNameWrist;
            case 1:
                return VNHumanHandPoseObservationJointNameThumbCMC;
            case 2:
                return VNHumanHandPoseObservationJointNameThumbMP;
            case 3:
                return VNHumanHandPoseObservationJointNameThumbIP;
            case 4:
                return VNHumanHandPoseObservationJointNameThumbTip;
            case 5:
                return VNHumanHandPoseObservationJointNameIndexMCP;
            case 6:
                return VNHumanHandPoseObservationJointNameIndexPIP;
            case 7:
                return VNHumanHandPoseObservationJointNameIndexDIP;
            case 8:
                return VNHumanHandPoseObservationJointNameIndexTip;
            case 9:
                return VNHumanHandPoseObservationJointNameMiddleMCP;
            case 10:
                return VNHumanHandPoseObservationJointNameMiddlePIP;
            case 11:
                return VNHumanHandPoseObservationJointNameMiddleDIP;
            case 12:
                return VNHumanHandPoseObservationJointNameMiddleTip;
            case 13:
                return VNHumanHandPoseObservationJointNameRingMCP;
            case 14:
                return VNHumanHandPoseObservationJointNameRingPIP;
            case 15:
                return VNHumanHandPoseObservationJointNameRingDIP;
            case 16:
                return VNHumanHandPoseObservationJointNameRingTip;
            case 17:
                return VNHumanHandPoseObservationJointNameLittleMCP;
            case 18:
                return VNHumanHandPoseObservationJointNameLittlePIP;
            case 19:
                return VNHumanHandPoseObservationJointNameLittleDIP;
            case 20:
                return VNHumanHandPoseObservationJointNameLittleTip;
            default:
                return VNHumanHandPoseObservationJointNameWrist;
        }
    } else {
        return nil;
    }
}

@end
