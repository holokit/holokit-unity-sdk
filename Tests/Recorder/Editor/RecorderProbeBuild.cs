#if UNITY_EDITOR && UNITY_IOS && HOLOKIT_RECORDER_TESTS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

// Native diagnostics are injected into the dedicated test export only.
// Shipping SDK source and release builds expose no test ABI or album override.
static class RecorderProbeBuild
{
    [PostProcessBuild(999)]
    static void AddDiagnostics(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        string path = Path.Combine(output, "Libraries/io.holokit.unity-sdk/Runtime/iOS/NativeCode/VideoRecorder.mm");
        string source = File.ReadAllText(path);
        source = source.Replace("if (!error && UIVideoAtPathIsCompatibleWithSavedPhotosAlbum(path))",
            "if (false && !error && UIVideoAtPathIsCompatibleWithSavedPhotosAlbum(path))");
        source = source.Replace("@interface VideoRecorder ()", @"
static NSHashTable* HoloKitProbeWriters;
static NSHashTable* HoloKitProbeTextures;
static NSHashTable* HoloKitProbeInputs;
static NSHashTable* HoloKitProbeAdaptors;
static void HoloKitProbeTrack(id object, NSHashTable* __strong * table) {
    if (!*table) *table = [NSHashTable weakObjectsHashTable];
    [*table addObject:object];
}
@interface VideoRecorder ()");
        source = source.Replace("    if (err)",
            "    HoloKitProbeTrack(self.writer, &HoloKitProbeWriters);\n    if (err)");
        source = source.Replace("    self.slotTextures[index] = texture;",
            "    self.slotTextures[index] = texture;\n    HoloKitProbeTrack(texture, &HoloKitProbeTextures);");
        source = source.Replace("    // Recording start", @"
    HoloKitProbeTrack(self.videoWriterInput, &HoloKitProbeInputs);
    for (AVAssetWriterInput* input in self.audio.inputs) HoloKitProbeTrack(input, &HoloKitProbeInputs);
    HoloKitProbeTrack(self.bufferAdaptor, &HoloKitProbeAdaptors);
    // Recording start");
        source += @"
#include <mach/mach.h>
extern ""C"" int HoloKit_StopProbe_LiveTextures() {
    @autoreleasepool { return (int)HoloKitProbeTextures.allObjects.count; }
}
extern ""C"" int HoloKit_MixedProbe_LiveInputs();
extern ""C"" int HoloKit_StopProbe_LiveInputs() {
    @autoreleasepool { return (int)HoloKitProbeInputs.allObjects.count + HoloKit_MixedProbe_LiveInputs(); }
}
extern ""C"" uint64_t HoloKit_StopProbe_GpuBytes() {
    @autoreleasepool {
    uint64_t textureBytes = 0;
    NSArray* textures = HoloKitProbeTextures.allObjects;
    for (id<MTLTexture> texture in textures) textureBytes += texture.allocatedSize;
    NSLog(@""[MEMORY-OWNERS] writers=%lu textures=%lu textureBytes=%llu inputs=%lu adaptors=%lu"",
        (unsigned long)HoloKitProbeWriters.allObjects.count,
        (unsigned long)textures.count, textureBytes,
        (unsigned long)HoloKitProbeInputs.allObjects.count,
        (unsigned long)HoloKitProbeAdaptors.allObjects.count);
    return MTLCreateSystemDefaultDevice().currentAllocatedSize;
    }
}
extern ""C"" uint64_t HoloKit_StopProbe_FootprintBytes() {
    task_vm_info_data_t info = {};
    mach_msg_type_number_t count = TASK_VM_INFO_COUNT;
    if (task_info(mach_task_self(), TASK_VM_INFO, (task_info_t)&info, &count) != KERN_SUCCESS) return 0;
    NSLog(@""[MEMORY-VM] internal=%llu device=%llu compressed=%llu graphics=%lld media=%lld"",
        info.internal, info.device, info.compressed,
        info.ledger_tag_graphics_footprint, info.ledger_tag_media_footprint);
    return info.phys_footprint;
}
";
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(RecorderProbeBuild).Assembly);
        source += File.ReadAllText(Path.Combine(package.resolvedPath, "Tests/Recorder/Native~/capture_reference.mm"));
        File.WriteAllText(path, source);
        string mixPath = Path.Combine(output, "Libraries/io.holokit.unity-sdk/Runtime/iOS/NativeCode/HoloKitMixedMovie.mm");
        string mixSource = File.ReadAllText(mixPath);
        mixSource = mixSource.Replace("@implementation HoloKitMixedMovie {", @"
static NSHashTable* HoloKitProbeMixInputs;
extern ""C"" int HoloKit_MixedProbe_LiveInputs() {
    @autoreleasepool { return (int)HoloKitProbeMixInputs.allObjects.count; }
}
@implementation HoloKitMixedMovie {");
        mixSource = mixSource.Replace("    _inputs = @[videoInput, audioInput];", @"
    _inputs = @[videoInput, audioInput];
    if (!HoloKitProbeMixInputs) HoloKitProbeMixInputs = [NSHashTable weakObjectsHashTable];
    for (AVAssetWriterInput* input in _inputs) [HoloKitProbeMixInputs addObject:input];");
        File.WriteAllText(mixPath, mixSource);
        string microphonePath = Path.Combine(output, "Libraries/io.holokit.unity-sdk/Runtime/iOS/NativeCode/HoloKitAudioCapture.mm");
        File.AppendAllText(microphonePath, @"
#if TARGET_OS_IOS
extern ""C"" uint64_t HoloKit_StopProbe_MicrophoneSamplesWritten() {
    return microphoneWritten.load(std::memory_order_acquire);
}
extern ""C"" int HoloKit_StopProbe_BypassVoiceProcessing(int bypass) {
    AVAudioInputNode* input = Microphone().engine.inputNode;
    if (!input.isVoiceProcessingEnabled) return -1;
    input.voiceProcessingBypassed = bypass != 0;
    return input.isVoiceProcessingBypassed == (bypass != 0) ? 0 : -1;
}
#endif
");
    }
}
#endif
