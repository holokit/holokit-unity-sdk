#if UNITY_IOS && DEVELOPMENT_BUILD && HOLOKIT_RECORDER_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HoloKit.iOS;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.Tests
{
// A repeatable stimulus, not a claim of synchronization or AEC quality. The
// artifact analyzer must measure decoded flashes/pulses and microphone leakage.
public sealed class RecorderAudioProbe : MonoBehaviour
{
    public static Action<HoloKitVideoRecorder> PrepareScene;
    public static Action RestoreScene;
    [Serializable] public class Take
    {
        public string name, path, sharingPath, error, videoBackend;
        public int frames, dropped;
        public bool audioFailed;
        public double scheduledDsp, duration, originHostTime;
        public List<CameraTiming> cameraTiming;
    }
    [Serializable] public class CameraTiming { public double frameEndHostTime, cameraHostTime, exposure; }
    [Serializable] class Report { public string runId, mode, diagnostic; public bool completed, interrupted; public int failures; public List<Take> takes; }
    readonly List<Take> takes = new();
    HoloKitVideoRecorder recorder;
    AudioSource stimulus;
    GameObject patch;
    Material patchMaterial;
    double scheduled = -1;
    int failures;
    bool completed, interrupted;
    string diagnostic;
    ARCameraManager arCamera;
    double cameraHostTime, cameraExposure;
    readonly List<CameraTiming> cameraTimings = new();
    static bool External => RecorderStopProbe.RequestedMode.StartsWith("audio-external");
    static bool CameraClockExperiment => RecorderStopProbe.RequestedMode == "audio-external-camera";
    static bool NativeReference => RecorderStopProbe.RequestedMode == "audio-external-reference";
    static readonly FieldInfo Origin = typeof(HoloKitVideoRecorder).GetField("_recordingOrigin", BindingFlags.Instance | BindingFlags.NonPublic);
    double originalOrigin;
    int previousSleepTimeout;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (RecorderStopProbe.RequestedMode.StartsWith("audio"))
            new GameObject(nameof(RecorderAudioProbe)).AddComponent<RecorderAudioProbe>();
    }
    void Check(bool condition, string message)
    {
        if (!condition) failures++;
        Debug.Log($"[AUDIO-TEST] {(condition ? "PASS" : "FAIL")} {message}");
    }
    IEnumerator Start()
    {
        previousSleepTimeout = Screen.sleepTimeout;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        yield return new WaitForSeconds(6);
        recorder = FindFirstObjectByType<HoloKitVideoRecorder>();
        if (recorder == null) { Check(false, "recorder missing"); Complete(); yield break; }
        PrepareScene?.Invoke(recorder);
        yield return null;
        if (RecorderStopProbe.RequestedMode == "audio-low-latency")
        {
            var configuration = AudioSettings.GetConfiguration();
            configuration.dspBufferSize = 256;
            Check(AudioSettings.Reset(configuration), "test-only 256-frame DSP configuration applied");
            yield return new WaitForSeconds(1);
        }
        AudioSettings.GetDSPBufferSize(out int bufferFrames, out int bufferCount);
        Debug.Log($"[AUDIO-TEST] Unity DSP rate={AudioSettings.outputSampleRate} bufferFrames={bufferFrames} bufferCount={bufferCount}");
        var camera = recorder.GetComponent<Camera>();
        recorder.GameAudioListener = camera.GetComponent<AudioListener>();
        var manager = recorder.GetComponentInParent<HoloKitCameraManager>();
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        recorder.RecordIn4K = false;
        if (NativeReference)
        {
            yield return RecordNativeReference();
            Complete();
            yield break;
        }
        if (External)
        {
            arCamera = camera.GetComponent<ARCameraManager>();
            if (arCamera != null) arCamera.frameReceived += CameraFrame;
            var observe = StartCoroutine(SampleCameraTiming());
            // The computer supplies both the visible flash and audible pulse.
            // No Unity overlay or local playback may substitute for camera input.
            yield return new WaitForSeconds(1);
            // Allow the operator to start the 90-second browser fixture after
            // this take reports ready. Keep the app visible after collection.
            yield return Record("environment-external", RecordingAudioMode.Environment, true, 120);
            StopCoroutine(observe);
            if (arCamera != null) arCamera.frameReceived -= CameraFrame;
            Complete();
            yield break;
        }
        // Existing shipping UI shader is also usable on this camera-facing quad.
        var shader = Shader.Find("UI/Default");
        if (shader == null) { Check(false, "flash shader unavailable"); Complete(); yield break; }
        patchMaterial = new Material(shader) { color = Color.black };
        patch = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(patch.GetComponent<Collider>());
        patch.transform.SetParent(camera.transform, false);
        patch.transform.localPosition = new Vector3(0, 0, .3f);
        patch.transform.localScale = Vector3.one * .12f;
        patch.GetComponent<Renderer>().sharedMaterial = patchMaterial;
        stimulus = gameObject.AddComponent<AudioSource>();
        stimulus.spatialBlend = 0;
        stimulus.volume = .5f;
        stimulus.loop = true;
        const int rate = 48000;
        var samples = new float[rate * 2];
        for (int i = 0; i < rate / 20; i++)
        {
            float envelope = Mathf.Min(1, i / 48f, (rate / 20 - i) / 48f);
            samples[i * 2] = .5f * envelope * Mathf.Sin(2 * Mathf.PI * 440 * i / rate);
            samples[i * 2 + 1] = .5f * envelope * Mathf.Sin(2 * Mathf.PI * 660 * i / rate);
        }
        stimulus.clip = AudioClip.Create("HoloKit synchronization pulses", rate, 2, rate, false);
        stimulus.clip.SetData(samples, 0);
        yield return new WaitForSeconds(1);
        yield return Record("environment-speaker", RecordingAudioMode.Environment, true, 8);
        yield return Record("game-only", RecordingAudioMode.GameAndMicrophone, false,
            RecorderStopProbe.RequestedMode == "audio-long" ? 610 : 8);
        yield return Record("game-bypassed-microphone", RecordingAudioMode.GameAndMicrophone, true, 8);
        yield return Record("game-aec-microphone", RecordingAudioMode.GameAndMicrophone, true,
            RecorderStopProbe.RequestedMode == "audio-long" ? 610 : 8);
        recorder.MicrophoneInput.Release(this);
        Complete();
    }
    IEnumerator Record(string name, RecordingAudioMode mode, bool microphone, double seconds)
    {
        recorder.AudioMode = mode;
        recorder.IncludeMicrophone = microphone;
        recorder.CreateMixedSharingCopy = name == "game-aec-microphone";
        recorder.StartRecording();
        float deadline = Time.realtimeSinceStartup + 10;
        while (recorder.IsPreparing && Time.realtimeSinceStartup < deadline) yield return null;
        Check(recorder.IsRecording, name + " started: " + recorder.LastError);
        if (!recorder.IsRecording) yield break;
        if (name.Contains("-microphone"))
        {
            // Keep exactly the same input/output graph, gain and route alive
            // across bypass and AEC takes. Only the processing flag changes.
            Check(recorder.MicrophoneInput.Acquire(this, MicrophoneProcessing.EchoCancellation), "fixture holds shared AEC input");
            Check(HoloKit_StopProbe_BypassVoiceProcessing(name.Contains("bypassed") ? 1 : 0) == 0,
                name + " processing state applied");
        }
        scheduled = AudioSettings.dspTime + 1;
        if (stimulus != null) stimulus.PlayScheduled(scheduled);
        originalOrigin = (double)Origin.GetValue(recorder);
        var take = new Take { name = name, path = recorder.LastRecordingPath, sharingPath = recorder.LastSharingPath,
            scheduledDsp = scheduled, duration = seconds, originHostTime = originalOrigin, videoBackend = "HoloKit" };
        if (name == "environment-external") take.cameraTiming = cameraTimings;
        takes.Add(take);
        Write(false);
        Debug.Log($"[AUDIO-TEST] STIMULUS name={name} dsp={scheduled:R} path={take.path}");
        yield return new WaitForSeconds((float)seconds);
        Check(recorder.IsRecording, name + " stayed active");
        recorder.EndRecording();
        if (CameraClockExperiment) Origin.SetValue(recorder, originalOrigin);
        if (stimulus != null) stimulus.Stop();
        scheduled = -1;
        deadline = Time.realtimeSinceStartup + 15;
        while (recorder.IsFinishing && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!recorder.IsFinishing, name + " finalized");
        take.error = recorder.LastError;
        take.audioFailed = recorder.AudioFailed;
        take.frames = recorder.SubmittedFrames;
        take.dropped = recorder.DroppedFrames;
        Check(!take.audioFailed, name + " no audio loss");
        Check(string.IsNullOrEmpty(take.error), name + " no native finalization error");
        if (recorder.CreateMixedSharingCopy)
            Check(take.sharingPath != take.path && File.Exists(take.sharingPath), name + " mixed sharing copy exists");
        yield return null;
        yield return null;
        Check(HoloKit_StopProbe_LiveInputs() == 0, name + " writer and sharing inputs released");
        Write(false);
        yield return new WaitForSeconds(1);
    }
    IEnumerator RecordNativeReference()
    {
        var session = FindFirstObjectByType<ARSession>();
        var cameraManager = FindFirstObjectByType<ARCameraManager>();
        bool wasEnabled = session != null && session.enabled;
        bool cameraWasEnabled = cameraManager != null && cameraManager.enabled;
        if (cameraManager != null) cameraManager.enabled = false;
        if (session != null) session.enabled = false;
        yield return new WaitForSeconds(.5f);
        var take = new Take { name = "environment-external", duration = 120, videoBackend = "AVCaptureMovieFileOutput",
            path = Path.Combine(Application.temporaryCachePath, $"NativeReference_{Guid.NewGuid():N}.mov") };
        Check(HoloKit_StopProbe_ReferenceStart(take.path) == 0, "native reference requested");
        float deadline = Time.realtimeSinceStartup + 12;
        while (HoloKit_StopProbe_ReferenceState() == 1 && Time.realtimeSinceStartup < deadline) yield return null;
        bool started = HoloKit_StopProbe_ReferenceState() == 2;
        if (started)
        {
            // The file output can briefly report didStart before an immediate
            // device interruption. Do not tell the operator to start Safari.
            yield return new WaitForSeconds(1);
            started = HoloKit_StopProbe_ReferenceState() == 2;
        }
        Check(started, "native reference recording started");
        if (started)
        {
            takes.Add(take);
            Write(false); // Only now may the runner announce EXTERNAL-READY.
            deadline = Time.realtimeSinceStartup + (float)take.duration;
            while (HoloKit_StopProbe_ReferenceState() == 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Time.realtimeSinceStartup >= deadline, "native reference stayed active for full duration");
        }
        HoloKit_StopProbe_ReferenceStop();
        deadline = Time.realtimeSinceStartup + 15;
        while (HoloKit_StopProbe_ReferenceState() > 0 && HoloKit_StopProbe_ReferenceState() < 4 &&
            Time.realtimeSinceStartup < deadline) yield return null;
        take.error = Marshal.PtrToStringUTF8(HoloKit_StopProbe_ReferenceError());
        diagnostic = take.error;
        Check(HoloKit_StopProbe_ReferenceState() == 4, "native reference finalized: " + take.error);
        if (session != null) session.enabled = wasEnabled;
        if (cameraManager != null) cameraManager.enabled = cameraWasEnabled;
    }
    void Update()
    {
        if (patchMaterial == null) return;
        double phase = AudioSettings.dspTime - scheduled;
        patchMaterial.color = scheduled >= 0 && phase >= 0 && phase % 1 < .05 ? Color.white : Color.black;
    }
    void CameraFrame(ARCameraFrameEventArgs args)
    {
        cameraHostTime = args.timestampNs.GetValueOrDefault() * 1e-9;
        cameraExposure = args.exposureDuration.GetValueOrDefault();
    }
    IEnumerator SampleCameraTiming()
    {
        var endOfFrame = new WaitForEndOfFrame();
        while (true)
        {
            yield return endOfFrame;
            if (recorder.IsRecording)
            {
                // This observer was started before the recorder's end-of-frame
                // coroutine. Test only: replace render-time video PTS with the
                // latest camera capture time. Native audio keeps its unchanged
                // common origin. No production timing policy is changed here.
                if (CameraClockExperiment && originalOrigin > 0 && cameraHostTime > 0)
                    Origin.SetValue(recorder, originalOrigin + HoloKit_AudioClockTime() - cameraHostTime);
                cameraTimings.Add(new CameraTiming { frameEndHostTime = HoloKit_AudioClockTime(),
                    cameraHostTime = cameraHostTime, exposure = cameraExposure });
            }
        }
    }
    [DllImport("__Internal")] static extern double HoloKit_AudioClockTime();
    // Keep timing evidence when an unrelated app interrupts this fixture.
    void OnApplicationPause(bool paused)
    {
        if (!paused || completed || takes.Count == 0) return;
        interrupted = true;
        if (NativeReference) HoloKit_StopProbe_ReferenceStop();
        Write(false);
    }
    void Write(bool completed) => File.WriteAllText(Path.Combine(Application.persistentDataPath, "recorder-audio-results.json"),
        JsonUtility.ToJson(new Report { runId = RecorderStopProbe.RunId, mode = RecorderStopProbe.RequestedMode,
            diagnostic = diagnostic,
            completed = completed, interrupted = interrupted, failures = failures, takes = takes }, true));
    [DllImport("__Internal")] static extern int HoloKit_StopProbe_LiveInputs();
    [DllImport("__Internal")] static extern int HoloKit_StopProbe_BypassVoiceProcessing(int bypass);
    [DllImport("__Internal")] static extern int HoloKit_StopProbe_ReferenceStart(string path);
    [DllImport("__Internal")] static extern int HoloKit_StopProbe_ReferenceState();
    [DllImport("__Internal")] static extern IntPtr HoloKit_StopProbe_ReferenceError();
    [DllImport("__Internal")] static extern void HoloKit_StopProbe_ReferenceStop();
    void Complete()
    {
        completed = true;
        Write(true);
        Debug.Log($"[AUDIO-TEST] COMPLETE failures={failures}");
        Screen.sleepTimeout = previousSleepTimeout;
        if (stimulus != null) { Destroy(stimulus.clip); Destroy(stimulus); }
        if (patch != null) Destroy(patch);
        if (patchMaterial != null) Destroy(patchMaterial);
        RestoreScene?.Invoke();
        if (!External) Application.Quit();
    }
}
}
#endif
