# Recorder regression tests

These tests belong to the HoloKit SDK and do not depend on EchoVision scripts.

## EditMode

Add `"testables": ["io.holokit.unity-sdk"]` to the consuming project's
`Packages/manifest.json`, select iOS, then run EditMode tests with Unity CLI:

```sh
unity test . --mode EditMode --output recorder-results.xml --editor-version 6000.3.24f1
```

`HoloKit.Recorder.Tests` covers GPU callback ordering and failures, capture cadence,
orientation aspect fitting, and restoration of the 4K preference.

The macOS command-line harness exercises the actual native writer without
importing a macOS plugin into Unity:

```sh
python3 Packages/io.holokit.unity-sdk/Tests/Recorder/Tools/native_recorder.py /tmp/native-recorder
```

`Native~/audio_buffer.cpp` tests the bounded PCM queue and common-time-window
clipping on the host. Its folder is excluded from Unity's native plugin importer.

## Physical iOS device

Build a **Development** player with `HOLOKIT_RECORDER_TESTS` in its scripting
defines. The test scene needs a configured `HoloKitVideoRecorder`,
`HoloKitCameraManager`, and `ARBackgroundVideoEnhancementManager`, with the shared `HoloKitMicrophoneCapture` input configured. The recorder's GPU path is required. Use a dedicated test scene:
the final stop scenario intentionally destroys its recorder component.

The SDK's test build postprocessor disables Photos saving in the exported test
player and adds memory counters. It does not modify the production native source.
Normal SDK consumers do not compile these probe assemblies without the define.

After installation, place a one-shot `Documents/recorder-test-mode.txt` in the
app's data container before launch. Its contents select:

- `stop`: empty recording, 1–3 pending GPU copies, disabled/destroyed recorder,
  fast sleep/resume, and 20 interleaved samples per stop method and camera format.
  The synchronous method deliberately recreates the old waits **only in tests**.
  An independent microphone consumer also checks idempotent acquisition, rejection
  of conflicting AEC processing, and continued arrival of new analysis samples
  after recording stops. This does not substitute for the application's visual
  sound-trigger test.
- `background`: waits for `[STOP-TEST] BACKGROUND-READY`; send the app to the
  background and resume it, then verify finalization and a second recording.
- `audio`: raw microphone with speaker pulses, game-only, then game plus microphone with voice processing bypassed/enabled on the
  same input/output graph. A camera-facing patch flashes with the scheduled game pulses.
- `audio-long`: the same fixture, extending each game take to 610 seconds.
- `audio-low-latency`: applies a 256-frame Unity DSP buffer in the test player
  to separate speaker buffering from capture timing. Production settings remain unchanged.
- `audio-external`: records 120 seconds of the real camera and raw microphone,
  without the virtual flash patch or phone speaker stimulus. Point the camera at
  `Tools/external_stimulus.html` on a computer and start its flash/pulse sequence.
  The collector checks physical onset offsets on the original movie timeline.
  Keep the browser visible on the display facing the phone. Browser speaker timing
  is estimated and display presentation is not instrument-calibrated; this fixture
  cannot establish sub-frame accuracy by itself.
  Wait for the runner's `EXTERNAL-READY` message before starting the 90-second
  web sequence. This mode leaves the app open after recording and invokes the
  optional `RecorderAudioProbe.RestoreScene` hook to restore application analysis.
  The page displays `HH:MM:SS.mmm`, pulse number and animation-frame number using
  that same estimated speaker clock. Its downloadable JSON records each displayed
  value and a run ID; the timecode is elapsed stimulus time, not synchronized
  wall-clock time across devices. Select a screen ROI away from the central text
  when measuring flash brightness.
- `audio-external-camera`: the same external fixture, with a **test-only**
  camera-capture timestamp experiment. The probe substitutes the latest AR frame
  time for rendered video time while retaining the native audio origin. It tests
  whether camera-frame age explains physical sync error; it does not establish
  game-animation/game-audio alignment or change the SDK's production policy.
- `audio-external-reference`: an AVFoundation `AVCaptureMovieFileOutput` baseline
  of the same physical stimulus. Temporarily stops AR and SDK microphone analysis,
  displays a recording indicator, and records camera
  and microphone directly. Its report identifies the reference backend. Compare
  timing with the SDK take to investigate fixture latency; reference success is
  not an SDK pass or a photodiode calibration. This native helper is included only
  in the dedicated test export, never in the shipping plugin.
- `memory`: idle AR, camera-format switches without recording, repeated recordings
  at each fixed camera format, empty recordings, and cooldown. Inspect memory
  trends separately; this diagnostic currently has no universal memory threshold.

The file is consumed once. Without it, the SDK probe does not run. It writes
`Documents/recorder-stop-results.json` after each case, marks `completed` only at
the end, and exits the player. Collect and validate the movies:

```sh
python3 Packages/io.holokit.unity-sdk/Tests/Recorder/Tools/recorder_stop_artifacts.py \
  --device DEVICE_UDID --bundle-id YOUR_APP_BUNDLE_ID --output /tmp/recorder-results
```

For an installed test player, the runner handles mode selection, launch, optional
OS background/resume, collection, and validation in one command:

```sh
python3 Packages/io.holokit.unity-sdk/Tests/Recorder/Tools/run_recorder_probe.py \
  --device DEVICE_UDID --bundle-id YOUR_APP_BUNDLE_ID \
  --mode background --output /tmp/recorder-background
```

The collector requires `devicectl`, `ffmpeg`, and `ffprobe`. It checks completion,
device assertions (including release of native recording textures), encoded frame
counts, full decode, and audio/video end times.
A partial run cannot pass. End-time agreement alone does **not** establish
perceptual audiovisual synchronization; flash/pulse and long-drift tests remain
separate acceptance checks.

Optional `RecorderStopProbe.PrepareScene` and `SetSleeping` hooks let a consuming
project wire its input and application-specific sleep behavior. Register them
before scene load. Without a sleep hook, the test disables/re-enables the
recorder's GameObject and temporarily sets 5 fps. EchoVision keeps its mask sleep
adapter in its own `Assets/Tests` folder.

## Audio measurements

The audio runner uses `recorder_audio_artifacts.py` (also requires NumPy) to check
track topology, decode flashes and pulses on the original file timeline, and
measure offset/drift and speaker leakage. It does not rebase individual tracks.
The external microphone detector compares the 1200 Hz tone with neighboring
frequency noise, using 20 ms windows and requiring 40 ms of sustained evidence.
This supports quiet raw inputs without lowering the 50 ms sync or 20 ms drift
gates. Run its synthetic regression with:

```sh
python3 -B -m unittest discover -s Packages/io.holokit.unity-sdk/Tests/Recorder/Tools -p 'test_recorder_*.py'
```

Runner regression also rejects stale run IDs, incomplete JSON writes, incorrect
reference modes/backends, and interrupted runs without restarting the player.

A quiet speaker baseline makes the AEC test inconclusive/failing. Passing this
fixture alone does not establish near-end speech preservation or interruption
behavior, and a short run does not establish long-recording drift.

`RecorderAudioProbe.PrepareScene` releases application analysis consumers for this
fixture so it can switch between raw and AEC input. Normal EchoVision analysis
keeps its own microphone lease; stopping the recorder must not release that lease.

The native host suite also exercises none/game/microphone/both source masks,
staggered first callbacks, separate 48 kHz stereo and 44.1 kHz mono tracks, and
decoded pulse offsets, initial/live recording gains, and the optional single-track
sharing copy. The latter must preserve the original compressed video hash.
These synthetic host cases test writer timing only.

The runner adds an invocation ID as the mode file's second line and requires the
same ID in the result. Rebuild the player when updating this protocol; an older
installed probe cannot satisfy it. The optional collector `--run-id` prevents a
previous run's completed report from being mistaken for current evidence.
