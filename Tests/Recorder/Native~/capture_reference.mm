// Test-export only: an AVFoundation camera/audio reference, without Unity
// rendering, SDK audio queues, or timestamp changes. Never saved to Photos.
#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>
extern "C" UIView* UnityGetGLView();

@interface HoloKitReferencePreview : UIView
@end
@implementation HoloKitReferencePreview
+ (Class)layerClass { return AVCaptureVideoPreviewLayer.class; }
@end

@interface HoloKitCaptureReference : NSObject <AVCaptureFileOutputRecordingDelegate>
@property(atomic) int state; // 0 idle, 1 starting, 2 recording, 3 finishing, 4 complete, -1 failed
@property(atomic, copy) NSString* error;
@property(nonatomic, strong) dispatch_queue_t queue;
@property(nonatomic, strong) AVCaptureSession* session;
@property(nonatomic, strong) AVCaptureMovieFileOutput* movie;
@property(nonatomic, strong) HoloKitReferencePreview* preview;
@property(nonatomic, strong) UILabel* label;
@property(nonatomic, copy) NSString* previousCategory;
@property(nonatomic, copy) NSString* previousMode;
@property(nonatomic) AVAudioSessionCategoryOptions previousOptions;
@end

@implementation HoloKitCaptureReference
- (instancetype)init {
    if ((self = [super init])) _queue = dispatch_queue_create("holokit.test.capture-reference", DISPATCH_QUEUE_SERIAL);
    return self;
}
- (void)finish:(NSString*)error {
    // Called on the session queue. Publish completion only after resources and
    // preview are released; Unity may then restart AR and microphone analysis.
    self.error = error;
    [self.session stopRunning];
    self.session = nil;
    self.movie = nil;
    if (self.previousCategory) {
        [AVAudioSession.sharedInstance setCategory:self.previousCategory mode:self.previousMode
            options:self.previousOptions error:nil];
        self.previousCategory = nil;
    }
    dispatch_async(dispatch_get_main_queue(), ^{
        [self.preview removeFromSuperview];
        self.preview = nil;
        self.label = nil;
        self.state = self.error ? -1 : 4;
    });
}
- (void)start:(NSString*)path {
    self.state = 1;
    self.error = nil;
    dispatch_async(self.queue, ^{
        NSError* error = nil;
        if ([NSFileManager.defaultManager fileExistsAtPath:path]) { [self finish:@"Reference destination already exists."]; return; }
        AVCaptureDevice* camera = [AVCaptureDevice defaultDeviceWithDeviceType:AVCaptureDeviceTypeBuiltInWideAngleCamera
            mediaType:AVMediaTypeVideo position:AVCaptureDevicePositionBack];
        AVCaptureDevice* microphone = [AVCaptureDevice defaultDeviceWithMediaType:AVMediaTypeAudio];
        if (!camera || !microphone) { [self finish:@"Reference camera/microphone unavailable."]; return; }
        AVCaptureDeviceInput* video = [AVCaptureDeviceInput deviceInputWithDevice:camera error:&error];
        AVCaptureDeviceInput* audio = [AVCaptureDeviceInput deviceInputWithDevice:microphone error:&error];
        if (!video || !audio) { [self finish:error.localizedDescription ?: @"Reference camera/microphone unavailable."]; return; }
        AVAudioSession* audioSession = AVAudioSession.sharedInstance;
        self.previousCategory = audioSession.category;
        self.previousMode = audioSession.mode;
        self.previousOptions = audioSession.categoryOptions;
        self.session = [AVCaptureSession new];
        self.session.usesApplicationAudioSession = YES;
        self.session.automaticallyConfiguresApplicationAudioSession = YES;
        self.session.configuresApplicationAudioSessionToMixWithOthers = YES;
        self.session.sessionPreset = AVCaptureSessionPreset1920x1080;
        self.movie = [AVCaptureMovieFileOutput new];
        self.movie.maxRecordedDuration = CMTimeMake(135, 1);
        [self.session beginConfiguration];
        if (![self.session canAddInput:video] || ![self.session canAddInput:audio]) {
            [self.session commitConfiguration]; [self finish:@"Reference input configuration rejected."]; return;
        }
        [self.session addInput:video];
        [self.session addInput:audio];
        if (![self.session canAddOutput:self.movie]) {
            [self.session commitConfiguration]; [self finish:@"Reference movie output rejected."]; return;
        }
        [self.session addOutput:self.movie];
        AVCaptureConnection* connection = [self.movie connectionWithMediaType:AVMediaTypeVideo];
        AVCaptureConnection* audioConnection = [self.movie connectionWithMediaType:AVMediaTypeAudio];
        NSLog(@"[REFERENCE] videoConnection=%@ audioConnection=%@", connection, audioConnection);
        if ([connection isVideoRotationAngleSupported:0]) connection.videoRotationAngle = 0;
        if (connection.isVideoStabilizationSupported) connection.preferredVideoStabilizationMode = AVCaptureVideoStabilizationModeOff;
        [self.session commitConfiguration];
        NSLog(@"[REFERENCE] configuration committed; starting AVCaptureSession");
        [self.session startRunning];
        NSLog(@"[REFERENCE] AVCaptureSession start returned running=%d", self.session.isRunning);
        if (!self.session.isRunning) { [self finish:@"Reference capture session did not start."]; return; }
        NSLog(@"[REFERENCE] starting AVCaptureMovieFileOutput");
        [self.movie startRecordingToOutputFileURL:[NSURL fileURLWithPath:path] recordingDelegate:self];
    });
}
- (void)captureOutput:(AVCaptureFileOutput*)output didStartRecordingToOutputFileAtURL:(NSURL*)url fromConnections:(NSArray*)connections {
    NSLog(@"[REFERENCE] didStart url=%@ connections=%@", url, connections);
    dispatch_async(self.queue, ^{
        if (self.state == 3) { [self.movie stopRecording]; return; }
        self.state = 2;
        dispatch_async(dispatch_get_main_queue(), ^{
            UIView* parent = UnityGetGLView();
            self.preview = [[HoloKitReferencePreview alloc] initWithFrame:parent.bounds];
            self.preview.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
            // Movie output is the reference. A second preview connection can
            // reconfigure the capture session just as file recording begins.
            self.label = [[UILabel alloc] initWithFrame:CGRectMake(0, 20, parent.bounds.size.width, 40)];
            self.label.autoresizingMask = UIViewAutoresizingFlexibleWidth;
            self.label.textAlignment = NSTextAlignmentCenter;
            self.label.textColor = UIColor.whiteColor;
            self.label.backgroundColor = [UIColor.blackColor colorWithAlphaComponent:.7];
            self.label.text = @"● REC — native camera/audio reference";
            [self.preview addSubview:self.label];
            [parent addSubview:self.preview];
        });
    });
}
- (void)captureOutput:(AVCaptureFileOutput*)output didFinishRecordingToOutputFileAtURL:(NSURL*)url
    fromConnections:(NSArray*)connections error:(NSError*)error {
    NSLog(@"[REFERENCE] didFinish url=%@ error=%@ domain=%@ code=%ld userInfo=%@",
        url, error, error.domain, (long)error.code, error.userInfo);
    dispatch_async(self.queue, ^{ [self finish:error.description]; });
}
- (void)stop {
    dispatch_async(self.queue, ^{
        if (self.state == 1 || self.state == 2) {
            self.state = 3;
            dispatch_async(dispatch_get_main_queue(), ^{ self.label.text = @"Saving native reference recording…"; });
            [self.movie stopRecording];
        }
    });
}
@end

static HoloKitCaptureReference* HoloKitReference() {
    static HoloKitCaptureReference* reference;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ reference = [HoloKitCaptureReference new]; });
    return reference;
}
extern "C" int HoloKit_StopProbe_ReferenceStart(const char* path) {
    auto reference = HoloKitReference();
    if (reference.state > 0 && reference.state < 4) return -1;
    [reference start:[NSString stringWithUTF8String:path]];
    return 0;
}
extern "C" int HoloKit_StopProbe_ReferenceState() { return HoloKitReference().state; }
extern "C" const char* HoloKit_StopProbe_ReferenceError() { return HoloKitReference().error.UTF8String; }
extern "C" void HoloKit_StopProbe_ReferenceStop() { [HoloKitReference() stop]; }
