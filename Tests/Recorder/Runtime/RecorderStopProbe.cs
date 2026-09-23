#if UNITY_IOS && DEVELOPMENT_BUILD && HOLOKIT_RECORDER_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HoloKit;
using HoloKit.iOS;
using UnityEngine;
using UnityEngine.Rendering;

// Only runs with the one-shot recorder-test-mode.txt in the recovery test build.
// Reflection keeps benchmark-only controls out of the recorder's public API.
namespace HoloKit.Tests
{
public class RecorderStopProbe : MonoBehaviour
{
    // IL2CPP on iOS does not reliably expose launch arguments to Environment.
    // A one-shot file in the app container selects this probe before scene load.
    public static string RunId { get; private set; }
    static readonly string Mode = ReadMode();
    public static string RequestedMode => Mode;
    static bool BackgroundOnly => Mode == "background";
    public static bool Requested => BackgroundOnly || Mode == "stop" || Mode == "memory" || Mode == "audio" || Mode == "audio-long" || Mode == "audio-low-latency" || Mode.StartsWith("audio-external");
    static string ReadMode()
    {
        string path = Path.Combine(Application.persistentDataPath, "recorder-test-mode.txt");
        if (!File.Exists(path)) return "";
        string[] lines = File.ReadAllText(path).Trim().Split('\n');
        string mode = lines[0].Trim();
        RunId = lines.Length > 1 ? lines[1].Trim() : "";
        File.Delete(path);
        return mode;
    }
    // Optional application lifecycle hooks. The SDK test has no game-content dependencies.
    public static Action<HoloKitVideoRecorder> PrepareScene;
    public static Action<bool> SetSleeping;
    int awakeFrameRate;
    bool sawPause;
    void OnApplicationPause(bool paused) { if (paused) sawPause = true; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() { if (Requested && !Mode.StartsWith("audio")) new GameObject(nameof(RecorderStopProbe)).AddComponent<RecorderStopProbe>(); }
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    HoloKitVideoRecorder recorder;
    readonly List<Result> results = new();
    int failures;
    bool completed;
    [Serializable] class Result
    {
        public string scenario, path;
        public bool fourK, sync, gpu;
        public int pending, frames, dropped, retainedGpuTextures, retainedWriterInputs;
        public double stopMs, finishMs;
        public float maxFrameMs;
        public ulong gpuBytesAfterStop, footprintBytesAfterStop;
    }
    [Serializable] class Report
    {
        public int failures;
        public bool completed;
        public string runId, mode, deviceModel, operatingSystem;
        public List<Result> results;
    }
    object Get(string name) => typeof(HoloKitVideoRecorder).GetField(name, Private).GetValue(recorder);
    void Set(string name, object value) => typeof(HoloKitVideoRecorder).GetField(name, Private).SetValue(recorder, value);
    void Call(string name, params object[] args) => typeof(HoloKitVideoRecorder).GetMethod(name, Private).Invoke(recorder, args);
    Queue<int> Pending => (Queue<int>)Get("_pending");
    void Check(bool pass, string message)
    {
        if (!pass) failures++;
        Debug.Log($"[STOP-TEST] {(pass ? "PASS" : "FAIL")} {message}");
    }
    IEnumerator Start()
    {
        yield return new WaitForSeconds(6);
        recorder = FindFirstObjectByType<HoloKitVideoRecorder>();
        Check(recorder != null, "scene contains a HoloKitVideoRecorder");
        if (recorder == null) { WriteReport(); Application.Quit(); yield break; }
        PrepareScene?.Invoke(recorder);
        var manager = FindFirstObjectByType<HoloKitCameraManager>();
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        awakeFrameRate = Application.targetFrameRate;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        yield return new WaitForSeconds(2);
        Debug.Log("[STOP-TEST] BEGIN");
        if (Mode == "memory")
        {
            yield return MemoryRun();
            completed = true;
            WriteReport();
            Debug.Log($"[STOP-TEST] COMPLETE memory failures={failures}");
            Application.Quit();
            yield break;
        }
        if (BackgroundOnly)
        {
            yield return BackgroundRun();
            completed = true;
            WriteReport();
            Debug.Log($"[STOP-TEST] COMPLETE background failures={failures}");
            Application.Quit();
            yield break;
        }
        yield return SharedMicrophoneRun();
        // Empty recording and each live ring occupancy, including slot wrap.
        yield return Run("empty", false, false, 0, 0);
        for (int n = 1; n <= 3; n++) yield return Run("pending-" + n, true, false, .6f, n);
        yield return Run("disabled", true, false, .6f, 3);
        yield return Run("quick-sleep", true, false, .6f, 3);
        // Interleave both methods and camera formats, reversing pair order each round.
        // First round is warm-up; 20 measured samples per method and format.
        for (int round = 0; round <= 20; round++)
        foreach (bool fourK in new[] { false, true })
        for (int pair = 0; pair < 2; pair++)
        {
            bool sync = (pair + round) % 2 == 0;
            yield return Run(round == 0 ? "warmup" : "benchmark", fourK, sync, .7f, 1);
        }
        yield return Run("destroyed", true, false, .6f, 3);
        completed = true;
        WriteReport();
        Debug.Log($"[STOP-TEST] COMPLETE failures={failures} samples={results.Count}");
        Application.Quit();
    }
    IEnumerator SharedMicrophoneRun()
    {
        var input = recorder.MicrophoneInput;
        Check(input != null, "shared microphone assigned");
        if (input == null) yield break;
        int originalConsumers = input.ConsumerCount;
        recorder.AudioMode = RecordingAudioMode.Environment;
        // Follow the scene's Environment processing (raw or Default) so an app's own analysis lease is compatible.
        MicrophoneProcessing environment = recorder.EnvironmentProcessing;
        input.Acquire(this, environment);
        float deadline = Time.realtimeSinceStartup + 5;
        while (input.IsPreparing && Time.realtimeSinceStartup < deadline) yield return null;
        Check(input.IsReady, "independent analysis consumer ready");
        if (input.IsReady)
        {
            Check(input.Acquire(this, environment) &&
                input.ConsumerCount == originalConsumers + 1, "repeated acquisition is idempotent");
            Check(!input.Acquire(recorder, MicrophoneProcessing.EchoCancellation) &&
                input.ConsumerCount == originalConsumers + 1 && input.Processing == environment,
                "conflicting AEC request preserves environmental input");
            yield return Run("shared-microphone", false, false, .7f, 1);
            Check(input.IsReady && input.ConsumerCount == originalConsumers + 1,
                "stop preserves independent analysis ownership");
            ulong samples = HoloKit_StopProbe_MicrophoneSamplesWritten();
            yield return new WaitForSeconds(.2f);
            Check(HoloKit_StopProbe_MicrophoneSamplesWritten() > samples,
                "analysis receives new microphone samples after recording stops");
        }
        input.Release(this);
        Check(input.ConsumerCount == originalConsumers, "analysis probe releases only its own lease");
    }
    [DllImport("__Internal")] static extern ulong HoloKit_StopProbe_MicrophoneSamplesWritten();

    IEnumerator Run(string scenario, bool fourK, bool sync, float duration, int requestedPending)
    {
        recorder.RecordIn4K = fourK;
        recorder.StartRecording();
        float prepareDeadline = Time.realtimeSinceStartup + 5;
        while (recorder.IsPreparing && Time.realtimeSinceStartup < prepareDeadline) yield return null;
        Check(recorder.IsRecording && recorder.UsesGpuPath, scenario + " started on GPU");
        if (!recorder.IsRecording) yield break;
        if (duration > 0) yield return new WaitForSeconds(duration);
        // End the capture coroutine and construct a real pending-copy workload.
        var capture = (Coroutine)Get("_capture");
        if (capture != null) recorder.StopCoroutine(capture);
        Set("_capture", null);
        if (requestedPending > 0)
        {
            // Give previous copies time to pass, then submit fresh copies this frame.
            yield return new WaitForEndOfFrame();
            Call("DrainSlots");
            for (int i = Pending.Count; i < requestedPending; i++)
            {
                Set("_nextCaptureTime", 0.0);
                Call("CaptureFrame");
            }
        }
        var result = new Result { scenario = scenario, fourK = fourK, sync = sync,
            pending = Pending.Count, gpu = recorder.UsesGpuPath, path = recorder.LastRecordingPath };
        Check(result.pending == requestedPending, scenario + $" pending {result.pending}/{requestedPending}");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        if (scenario == "disabled") recorder.enabled = false;
        else if (scenario == "destroyed") DestroyImmediate(recorder);
        else if (scenario == "quick-sleep")
        {
            Sleep(true);
        }
        else if (sync) StopSynchronously();
        else recorder.EndRecording();
        result.stopMs = timer.Elapsed.TotalMilliseconds;
        // A second stop must be harmless; a start during drain must be rejected.
        recorder.EndRecording();
        if (recorder.IsFinishing && scenario != "destroyed" && scenario != "quick-sleep")
        {
            recorder.StartRecording();
            Check(!recorder.IsRecording && recorder.LastRecordingPath == result.path, "restart blocked during finishing");
        }
        while (recorder.IsFinishing && timer.Elapsed.TotalSeconds < 15)
        {
            yield return null;
            result.maxFrameMs = Mathf.Max(result.maxFrameMs, Time.unscaledDeltaTime * 1000);
        }
        result.finishMs = timer.Elapsed.TotalMilliseconds;
        for (int i = 0; i < 2; i++)
        {
            yield return null;
            result.maxFrameMs = Mathf.Max(result.maxFrameMs, Time.unscaledDeltaTime * 1000);
        }
        Check(!recorder.IsFinishing, scenario + " finalized within timeout");
        Check(!recorder.AudioFailed, scenario + " audio drained without loss");
        result.frames = recorder.SubmittedFrames;
        result.dropped = recorder.DroppedFrames;
        if (duration > 0)
        {
            Check(result.frames > 0 && File.Exists(result.path) && new FileInfo(result.path).Length > 1024, scenario + " movie exists");
            Check(result.dropped == 0, scenario + " no dropped frames");
        }
        result.gpuBytesAfterStop = HoloKit_StopProbe_GpuBytes();
        result.footprintBytesAfterStop = HoloKit_StopProbe_FootprintBytes();
        result.retainedGpuTextures = HoloKit_StopProbe_LiveTextures();
        Check(result.retainedGpuTextures == 0, scenario + " recording textures released");
        result.retainedWriterInputs = HoloKit_StopProbe_LiveInputs();
        Check(result.retainedWriterInputs == 0, scenario + " writer inputs released");
        results.Add(result);
        Debug.Log("[STOP-TEST] RESULT " + JsonUtility.ToJson(result));
        if (scenario == "disabled") recorder.enabled = true;
        if (scenario == "quick-sleep")
        {
            Sleep(false);
        }
        WriteReport();
        yield return new WaitForSeconds(.25f);
    }
    void Sleep(bool sleeping)
    {
        if (SetSleeping != null) SetSleeping(sleeping);
        else
        {
            recorder.gameObject.SetActive(!sleeping);
            Application.targetFrameRate = sleeping ? 5 : awakeFrameRate;
        }
    }

    IEnumerator BackgroundRun()
    {
        recorder.RecordIn4K = true;
        recorder.StartRecording();
        yield return new WaitForSeconds(2);
        Check(recorder.IsRecording && recorder.UsesGpuPath, "background recording started on GPU");
        sawPause = false;
        Debug.Log("[STOP-TEST] BACKGROUND-READY");
        float deadline = Time.realtimeSinceStartup + 60;
        while (!sawPause && Time.realtimeSinceStartup < deadline) yield return null;
        Check(sawPause, "actual OS pause received");
        Check(!recorder.IsRecording, "pause stopped capture");
        deadline = Time.realtimeSinceStartup + 15;
        while (recorder.IsFinishing && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!recorder.IsFinishing, "background movie finalized after resume");
        Check(!recorder.AudioFailed, "background audio drained without loss");
        var result = new Result { scenario = "background", fourK = true, gpu = recorder.UsesGpuPath,
            path = recorder.LastRecordingPath, frames = recorder.SubmittedFrames, dropped = recorder.DroppedFrames };
        Check(result.frames > 0 && File.Exists(result.path), "background movie exists");
        Check(result.dropped == 0, "background no dropped frames");
        results.Add(result);
        yield return Run("after-background", true, false, .7f, 1);
    }

    void MemoryPoint(string phase, int iteration) => Debug.Log($"[MEMORY-TEST] {phase} {iteration} " +
        $"gpu={HoloKit_StopProbe_GpuBytes()} footprint={HoloKit_StopProbe_FootprintBytes()}");

    IEnumerator MemoryRun()
    {
        // Change one workload at a time: AR alone, format switches, then captures
        // with a fixed AR format. Distinguishes writer/render resources from ARKit.
        var enhancement = FindFirstObjectByType<ARBackgroundVideoEnhancementManager>();
        enhancement.IsEnabled = false;
        for (int i = 0; i <= 6; i++)
        {
            MemoryPoint("idle", i);
            yield return new WaitForSeconds(5);
        }
        for (int i = 0; i < 20; i++)
        {
            enhancement.IsEnabled = true;
            yield return new WaitForSeconds(.6f);
            enhancement.IsEnabled = false;
            yield return new WaitForSeconds(.6f);
            MemoryPoint("format-switch", i);
        }
        for (int i = 0; i < 20; i++)
        {
            yield return Run("fixed-format", false, false, .7f, 1);
            MemoryPoint("fixed-format", i);
        }
        enhancement.IsEnabled = true;
        yield return new WaitForSeconds(2);
        for (int i = 0; i < 20; i++)
        {
            yield return Run("fixed-4k-format", true, false, .7f, 1);
            MemoryPoint("fixed-4k-format", i);
        }
        enhancement.IsEnabled = false;
        for (int i = 0; i < 10; i++)
        {
            yield return Run("empty", false, false, 0, 0);
            MemoryPoint("empty", i);
        }
        for (int i = 0; i <= 6; i++)
        {
            MemoryPoint("cooldown", i);
            yield return new WaitForSeconds(5);
        }
    }
    void StopSynchronously()
    {
        // Reproduce the old GPU waits in the same binary and capture workload.
        // EndRecording then performs the same audio shutdown and resource cleanup.
        while (Pending.Count > 0)
        {
            int index = Pending.Dequeue();
            var request = AsyncGPUReadback.Request(((Texture2D[])Get("_slots"))[index], 0, 0, 1, 0, 1, 0, 1);
            request.WaitForCompletion();
            if (request.hasError) Check(false, "baseline readback succeeded");
            else Call("SubmitSlot", index);
        }
        AsyncGPUReadback.WaitAllRequests();
        recorder.EndRecording();
    }
    [DllImport("__Internal")] static extern ulong HoloKit_StopProbe_GpuBytes();
    [DllImport("__Internal")] static extern int HoloKit_StopProbe_LiveTextures();
    [DllImport("__Internal")] static extern int HoloKit_StopProbe_LiveInputs();
    [DllImport("__Internal")] static extern ulong HoloKit_StopProbe_FootprintBytes();
    void WriteReport() => File.WriteAllText(Path.Combine(Application.persistentDataPath, "recorder-stop-results.json"),
        JsonUtility.ToJson(new Report { runId = RunId, failures = failures, completed = completed,
            mode = Mode, deviceModel = SystemInfo.deviceModel,
            operatingSystem = SystemInfo.operatingSystem, results = results }, true));
}
}
#endif
