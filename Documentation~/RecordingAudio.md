# Recording audio

`HoloKitVideoRecorder` owns movie configuration. Application content owns its
analysis/effects and references the SDK's shared microphone input.

| Inspector mode | Captured sound | Microphone processing |
| --- | --- | --- |
| Environment | Environmental microphone, one mono track | Measurement input; voice processing off |
| Environment, Microphone Processing = Default | Environmental microphone, one mono track | iOS default input with automatic gain (as Unity's `Microphone`); quiet sounds read louder, levels uncalibrated |
| Game And Microphone | Game stereo track and microphone mono track | Native voice processing with AEC; AGC off |
| Game And Microphone, Include Microphone off | Game track only | Recorder does not acquire or request microphone input |

Assign one `HoloKitMicrophoneCapture` component as **Microphone Input**. Assign
Main Camera's existing `AudioListener` as **Game Audio Listener** for game capture.
Do not create a microphone AudioListener or play a microphone AudioClip through
Unity's game mix. The SDK taps the selected listener without muting its playback.
AEC changes the iOS audio-session mode/route behavior; enabling it is not a promise
of perfect acoustic isolation or unchanged hardware output volume.

Game Gain and Microphone Gain control recorded samples independently of playback.
Zero gain retains the track and its timeline. These values can change during a
recording. Source selection, microphone processing and track topology take effect
at the next recording; do not switch processing while another consumer holds it.

`MicrophoneProcessing` has three values: `Environment` (raw level for measurement and
analysis calibrated to it), `EchoCancellation` (voice processing, used by Game And Microphone) and
`Default` (iOS automatic gain, matching apps that used Unity's `Microphone`). A sound-reactive app
whose thresholds were tuned with Unity's `Microphone` should use `Default` for both its analysis
and the recorder, because all consumers of the shared input must request the same processing.

## Shared analysis

An analysis component acquires the same input on enable and releases it on disable:

```csharp
input.Acquire(this, MicrophoneProcessing.Environment);
// Wait for input.IsReady, or inspect input.IsPreparing and input.LastError.
input.ReadLatestSamples(samples); // reusable float[]
input.ReadSpectrum(spectrum);    // reusable float[1024]
// When this consumer is done:
input.Release(this);
```

Acquire/Release run on Unity's main thread. Acquiring again with the same owner is
idempotent. All concurrent consumers must request the same processing mode: raw
analysis and AEC recording cannot silently change each other's input. Recording
releases only its own lease; analysis can continue before and after a movie.
Do not destroy or disable the shared input while its consumers still need it.

`StartRecording()` prepares microphone permission/input asynchronously when needed.
Use `IsPreparing`, `IsRecording`, `IsFinishing`, and `LastError` for UI state.
`EndRecording()` also cancels pending preparation. A route/output-format change
ends the current segment; a resumed recording starts a new file.

## Files and sharing

`LastRecordingPath` is the original movie. Game and microphone are actual AAC
tracks named Game and Microphone, not left/right channels in a single track.
Multi-track playback/import varies between applications.

**Create Mixed Sharing Copy** preserves that original and creates a separate
`.share.mp4` with one stereo AAC track. It mixes the existing tracks on their
original time axis with equal headroom (half gain for each of two source tracks).
Compressed video is copied byte-for-byte without CPU rendering or re-encoding.
`LastSharingPath` identifies the sharing file, or the original for single-source
recordings. Finalization remains asynchronous and `IsFinishing` includes the
optional export. An export error is available through `LastError`; it does not
delete the original movie. The sharing copy is used for the Photos save when
requested. A single-source movie already has one audio track and needs no extra copy.

## Timing and lifecycle

Video timestamps and microphone sample timestamps use the same native monotonic
host clock. Game DSP time is anchored to that clock before capture. Tracks retain
real start offsets and gaps; none is rebased to its first callback. Audio threads
copy into bounded queues rather than waiting on AVAssetWriter. A timestamped tail
is clipped at the common stop boundary and drained by a worker.

Ordinary stop/disable/destroy do not synchronously wait for GPU readback. At the
OS-suspension boundary only, Unity must complete its own pending GPU copies before
managed callbacks stop; native file finalization then uses a background task.

File decoding and matching durations do not prove perceptual synchronization.
Use the SDK's flash/pulse, long-duration, microphone and lifecycle fixtures in
[the recorder regression guide](../Tests/Recorder/README.md). Hardware buffering
can delay sound heard through the speaker; do not subtract a measured acoustic
round-trip delay from microphone timestamps without separating playback and input
latency. The test-only low-latency fixture changes Unity's DSP buffer; normal SDK
recording does not change the application's global audio configuration.
