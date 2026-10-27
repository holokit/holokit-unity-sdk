# Recorder and microphone API proposal (0.6.x)

Status: **proposal for review**. Nothing here is implemented yet. It evolves the 0.6.0 API; it is not
a rewrite.

## Summary

- The recorder has two independent switches, **Record game audio** and **Record microphone**, and a
  microphone processing choice. Each enabled source becomes its own AAC track.
- The microphone is one app-wide input. The recorder opens it only while recording; apps that analyse
  sound open the same input with `HoloKitMicrophone.Open`.
- Apps may keep using Unity `Microphone`. The SDK tolerates it (joins the audio session instead of
  taking it over, restarts after interruptions) and says so when it cannot.
- Checked against five apps (Body Oracle, EchoVision, FungiSync, MOFA, GravField); all fit without
  custom tracks or extra components.

## Signal flow

```
SOURCES                 REAL TIME (iOS, one choice per app)         CONSUMERS (all get the same stream)
                        +--------------------------------+
 room sound ---+        | input processing:              |    +--> HoloKitMicrophone.Open() -> visuals
 players ------+-> MIC->|  Raw       linear, no AGC      |----+--> recorder MIC track (while recording)
 speaker leak -+        |  Default   automatic gain      |    +--> Unity Microphone (app's own; stops
      ^                 |  VoiceChat Apple voice proc.:  |         on X3, SDK restarts, rule 4)
      |                 |   echo cancel, noise suppress, |
      |                 |   AGC; lossy; may duck output  |
      |                 +--------------------------------+
      |
 Unity audio mix --+--> speaker ------------(room)--------------> leaks back into MIC
                   +--> AudioListener tap ------------------------> recorder GAME track (clean digital)

RECORDING (host clock, tracks sample-aligned)
  master.mp4 = HEVC video + [GAME AAC] + [MIC AAC]           <- never modified; input for post
     |
     +-- mixed copy on --> OFFLINE EXPORT (HoloKitMixedMovie)
                             [P2, optional] MIC' = AEC3(MIC, reference = GAME)
                             mix GAME + MIC' --> .share.mp4 --> Photos

Measured conflicts: Raw/VoiceChat change a running Unity mic's level (X2); stopping VoiceChat
silences it (X2); Unity Microphone.Start stops the SDK mic (X3, fixed by restart); two Unity
Microphone.Start calls leave the first clip silent (X4).
```

### Choosing a configuration

```
Need a mic track?
 no  -> game only ................................................. Body Oracle
 yes -> Must game sound stay out of the mic track?
         no  -> Default; analysis and recording share one input ... EchoVision, FungiSync
         yes -> Does anything analyse the mic (visuals)?
                 yes -> Default + post AEC3 (VoiceChat would alter the analysis
                        and suppress ambience) ........................ GravField
                 no  -> pick one:
                        real time: VoiceChat -> share copy clean at once; lossy,
                                   may duck playback, Unity-output cancel unverified
                        post:      Raw + AEC3 -> raw master kept, share copy cleaned
                                   ............................................. MOFA (default)
```

## Evidence (iPhone 15 Pro, iOS 27, 2026-09-24)

Coexistence probe `Tests/Probe/Runtime/HoloKitFeatureProbe.Coexist.cs`, spoken loop plus room noise:

| Case | Result |
|---|---|
| X1 Unity `Microphone` alone | runs |
| X2 Unity mic running, recorder in Default | both run; Unity level −5.4 dB during, +3.5 dB after (speech stimulus varies by several dB; not yet conclusive, Q2/Q3) |
| X2 Unity mic running, recorder in Raw (Measurement) | both run; Unity level **−13.6 dB** while recording (session mode is app-wide) |
| X2 Unity mic running, recorder in VoiceChat | Unity level +13.7 dB during; Unity mic silent after the recorder stops (not reproduced in the lab once the output stayed on the speaker) |
| X3 SDK mic running, then Unity `Microphone.Start` | **SDK mic stops** ("audio-route change or interruption"); Unity runs |
| X4 Unity `Microphone.Start` twice | first clip goes silent (−66 dB); Unity supports one consumer per device |

Consequences:
1. iOS has one `AVAudioSession` per app, so processing is app-wide. Only Default leaves another
   microphone user unaffected.
2. Whoever reconfigures the session last can stop the other input. The SDK cannot prevent this; it
   must restart and report.

### Audio lab (`holokit-audio-lab`, iPhone 15 Pro "I", iOS 27, 2026-09-24)

A standalone Unity project runs 25 scenarios; the phone plays a known speech clip as game sound, the
Mac plays a second voice (or pink noise) as the room. For each microphone signal the part explained
by the game clip (linear, coherence-based) is separated from the rest. Levels in dBFS; "game" is the
game leakage, "room" the rest. Full data: `holokit-audio-lab/runs/full2`, `runs/full3`, `notes.md`.

| Measurement | Result |
|---|---|
| SDK mic track, game playing | Default: game −18.9, room −29.0 · Raw: game −52.7, room −48.5 · VoiceChat: game −43.2 (cancelled), room −21.2 |
| Running Unity mic while the recorder uses Raw | −17.7 → −48.6 → −18.4 (speech); −39.3 → −58.7 → −40.5 (noise, 3 repeats): **−19 to −31 dB, app-wide** |
| Running Unity mic while the recorder uses Default | −17.6 → −18.9 → −17.6 (speech); −39.3 → −39.6 → −41.0 (noise, 3 repeats): **neutral (≤1.5 dB)** |
| Running Unity mic while the recorder uses VoiceChat | game −18.0 → **−43.0** (≥25 dB; ≈50 dB without room sound), room −28.8 → −22.7: **echo cancellation applies to Unity's data too**; recovers after |
| Unity `Microphone.Start` while the SDK input runs | mode kept, options 0xd → 0x21 (drops DefaultToSpeaker) → SDK input stops |
| Unity `Microphone.Start` **during a take** | SDK input stops, **whole movie lost** (0 bytes, "Audio capture failed … changed format") |
| Unity `Microphone.End` during a take | session → Ambient; **whole movie lost** (0 bytes) although the SDK input still reported ready |
| Unity mic with "Force iOS Speakers When Recording" = 0 | output route → Receiver (earpiece); FungiSync and GravField ship with 0 |
| EchoVision pattern (analysis + record, Default) | analysis stream continuous before/during/after the take (0 silent frames) |
| Analysis in Default + recorder asking for VoiceChat | `StartRecording` fails: "The shared microphone is already using a different processing mode." |
| MOFA (game + mic, VoiceChat, mixed copy) | game track clean (room −48.6); mic track game −45.1, room −23.2; share copy mixes both |
| GravField on 0.6 (Unity mic analysis + game + mic track) | recorder forces VoiceChat: the Unity analysis input loses the game (−18.5 → −48.4) and gains room (−30.5 → −26.1) while recording |
| Body Oracle (game only) | one clean game track, microphone never opened |
| Ducking of game playback by VoiceChat | **not measured**: the Mac microphone could not separate the phone's game sound from room noise |

Not verified: the FungiSync/GravField "AudioSource at −80 dB" analysis path (the lab's substitute,
source volume 1e-4, returned a constant spectrum — a lab artifact, not app evidence).

## API

```csharp
namespace HoloKit.iOS
{
    // Serialized values unchanged: 0 = former Environment, 1 = former EchoCancellation, 2 = Default.
    public enum MicrophoneProcessing
    {
        Raw = 0,        // Measurement mode: no automatic gain or voice processing (calibrated levels)
        VoiceChat = 1,  // real-time, irreversible voice processing (echo cancel, noise suppression); kept for compatibility
        Default = 2,    // iOS default input with automatic gain (same as Unity Microphone)
    }

    public sealed class HoloKitVideoRecorder : MonoBehaviour
    {
        // Audio (Inspector)
        public bool RecordGameAudio;                  // Unity mix at GameAudioListener, playback unchanged
        public AudioListener GameAudioListener;       // auto-filled with the main camera's listener
        public bool RecordMicrophone;                 // SDK microphone, opened only while recording
        public MicrophoneProcessing MicrophoneProcessing = MicrophoneProcessing.Default;
        public float GameGain = 1, MicrophoneGain = 1;          // recorded level only
        public bool CreateMixedSharingCopy;           // with both tracks: .share.mp4 with one mixed track,
                                                      // saved to Photos instead of the two-track master
        // Video (Inspector)
        public bool CaptureCameraIn4K = true;         // renamed from RecordIn4K: ARKit camera feed only,
                                                      // the movie stays at screen resolution, 30 fps

        // Unchanged: StartRecording, EndRecording, ToggleRecording, IsPreparing, IsRecording,
        // IsFinishing, LastRecordingPath, LastSharingPath, LastError, Submitted/DroppedFrames.

        public event Action<RecordingResult> RecordingFinished;
    }

    public readonly struct RecordingResult
    {
        public readonly string MoviePath, SharingPath, Error;
        public readonly double Duration;
        public readonly int DroppedFrames;
        public readonly int MicrophoneInterruptions;          // restarts during the take (gaps are silence)
        public readonly MicrophoneProcessing EffectiveProcessing; // what iOS actually applied
    }

    // One app-wide input. Reference counted; runs while any handle is open.
    public static class HoloKitMicrophone
    {
        // Returns null and sets LastError if the input already runs with a different processing.
        public static MicrophoneHandle Open(UnityEngine.Object owner,
            MicrophoneProcessing processing = MicrophoneProcessing.Default);
        public static bool IsRunning { get; }
        public static MicrophoneProcessing Processing { get; }
        public static int SampleRate { get; }
        public static string LastError { get; }
        public static event Action<string> Interrupted;   // followed by an automatic restart
    }

    public sealed class MicrophoneHandle : IDisposable
    {
        public int ReadLatest(float[] mono);             // newest samples, main thread
        public void ReadSpectrum(float[] bins);          // 1024 bins
        public void Dispose();                           // releases this owner's reference
    }
}
```

`HoloKitMicrophoneCapture` (component with `Acquire`/`Release`) stays as an obsolete wrapper over
`HoloKitMicrophone` for one release.

### Rules

1. **No implicit microphone.** With `RecordMicrophone` off the recorder never requests permission.
2. **One processing per app.** The recorder opens the microphone with `MicrophoneProcessing`. If an
   analysis handle already runs with a different processing, `StartRecording` fails with a
   `LastError` naming both; it never changes the processing under a running handle.
3. **Join, do not take over — but keep the category.** In Default the SDK only ensures category
   `PlayAndRecord` and leaves mode and options alone when they are compatible; it does not toggle
   `setActive` on stop while other audio is running. While it holds an input and another component
   drops the category (Unity `Microphone.End` sets `Ambient`), it sets `PlayAndRecord` again.
4. **Restart and report; never lose the take.** On a route change, configuration change,
   interruption or media-services reset the input restarts; the movie gets silence for the gap on the
   host clock (A/V sync unchanged), and the result counts the interruption. The writer must survive a
   changed input format (today Unity `Microphone.Start`/`End` mid-take produce a 0-byte movie). If
   another component changed the mode, `EffectiveProcessing` shows it.
5. **Warn on app-wide processing.** Raw and VoiceChat are marked "affects the whole app" in the
   Inspector. At `StartRecording`, if Unity `Microphone.IsRecording(null)`: Raw logs a warning;
   VoiceChat logs that it will also cancel the game and boost the room in the app's own microphone
   (measured: game −25 dB, room +6 dB in a running Unity mic) — analysis-driven visuals change while
   recording.
6. **Speaker route.** The Inspector warns when Unity's "Force iOS Speakers When Recording" is off in a
   project that uses Unity `Microphone`: the game then plays from the earpiece.

### Inspector

```
HoloKit Video Recorder
  Audio
    [x] Record game audio      Listener: Main Camera (AudioListener)
    [x] Record microphone      Processing: Default ▾   (Raw / VoiceChat: affects the whole app)
        Game gain 1.0   Microphone gain 1.0
    [ ] Mixed copy for sharing (shown when both tracks are on)
  Video
    [x] Capture camera in 4K   (movie resolution = screen, 30 fps)
```

## Walk-through: five apps

| App | Game track | Mic track | Mic analysis | Processing | Mic open |
|---|---|---|---|---|---|
| Body Oracle | on | off | none | — | never |
| EchoVision | off | on | yes | Default | whole session |
| FungiSync | off | on | yes | Default | whole session |
| MOFA | on | on, separate + mixed copy | none | Raw (Default as fallback, Q1); leakage removed in post | recording only |
| GravField | on (silent when the piece has no sound) | on, separate | yes | Default | whole session |

### Body Oracle — game sound only
Recorder: game on, microphone off. No permission prompt, no audio session changes.
Today: no recorder in its scenes yet (`bodyoracle/Assets`); it adds the SDK's default UI canvas.
**Fits.**

### EchoVision — no game sound; environment drives visuals and is recorded
```csharp
mic = HoloKitMicrophone.Open(this);          // Default; sound-wave analysis all session
mic.ReadSpectrum(spectrum);                  // replaces MicrophoneAPI (33-line adapter)
// recorder: game off, microphone on, Default → shares the running input
```
Default is required: its automatic gain measured +17.3 dB over Raw, which restored the sensitivity users noticed missing.
**Fits.** Recording shares the running input; no restart, so the visuals never glitch at record start.

### FungiSync — no game sound; environment recorded
Today `Assets/Scripts/Sound/HolokitMicrophoneAPI.cs` starts Unity `Microphone` and plays the clip
through an AudioSource routed to a −80 dB mixer group for analysis, plus a second "recording" source.
- Recommended: replace it with `HoloKitMicrophone.Open` (as EchoVision); drop both AudioSources.
- Unchanged code also works: Unity mic starts first (X2 order), recorder in Default. A restart of the
  Unity mic mid-take (e.g. `OnEnable`) is X3 and is covered by rule 4.
**Fits.** Minor: the −80 dB analysis source still enters the game mix; harmless with game track off.

### MOFA — game sound plus players' voices, separate tracks for post
Recorder: game on, microphone on (bound to the existing *Record Microphone* setting, which is not
wired today — `HoloKitAppRecorder.cs:64-71` is commented out), mixed copy on for Photos.
```csharp
recorder.RecordGameAudio = true;
recorder.RecordMicrophone = HoloKitApp.Instance.GlobalSettings.RecordMicrophone;
recorder.MicrophoneProcessing = MicrophoneProcessing.Raw;   // linear input for post; Q1
```
Separation is done **after recording**, not by iOS voice processing:
- The game track is the exact digital signal that was played, so it is an ideal reference for
  removing the speaker leakage from the mic track offline (adaptive filter / DAW echo removal).
  Both tracks share the host clock, so they are sample-aligned without manual syncing.
- VoiceChat stays available as the **real-time option** (Apple's voice processing, no post step): the
  share copy is clean immediately. Costs: irreversible (echo cancellation, noise suppression,
  automatic gain), may duck the game's playback while recording, and not yet verified to cancel
  Unity's output (Unity plays through its own output unit, not the voice-processing unit). The
  default for MOFA is the post path because it keeps a raw master.
- Raw is preferred over Default because a fixed, linear gain makes leakage subtraction simpler;
  automatic gain changes the leakage level over time. Raw is ~17 dB quieter; `MicrophoneGain` or
  normalisation in post compensates. MOFA has no other microphone user, so Raw's app-wide effect
  does not matter here.
**Fits.** Today MOFA's own recorder (`com.holoi.xr.holokit`) would play the
microphone out of the speaker when enabled; the SDK recorder never does.

### GravField — optional game sound plus environment
Recorder: game on, microphone on, Default. VoiceChat is wrong here: voice processing suppresses
non-speech sound, and the environment is the content.
Analysis today: `Assets/Scripts/GameLogic/AudioProcessor.cs` (Unity `Microphone` + AudioSource, same
pattern as FungiSync); migrate to `HoloKitMicrophone.Open` or keep it as in FungiSync.
**Fits.** When the piece has no sound the game track is silence; turning it off is a scene choice.

### What the walk-through removed from earlier drafts
None of the five apps needs: a Unity-Microphone recording source, a `CustomAudioTrack.Push` for
arbitrary PCM, more than two tracks, a stereo `Screen` video source, or an app-managed session flag.
They move to *Deferred* and come back only with a real user.

## Open questions

- **Answered by the lab:** VoiceChat cancels in every microphone user (Q: "does it reach Unity's
  data?" — yes); Default is level-neutral; Unity `Microphone.Start` rewrites options (0xd → 0x21) and
  keeps mode, `Microphone.End` sets Ambient (former Q3).
- **Still open — ducking:** measure VoiceChat's effect on game playback with a microphone next to the
  phone (the Mac microphone was too far).
- **Q1 (MOFA): Raw or Default for post-processing?** Record the same take both ways with game sound
  playing, run offline leakage removal with the game track as reference, and compare residual
  leakage and voice quality. Raw is expected to win (linear gain); Default is the fallback if Raw's
  level is too low in practice.
- **Q2: does Default's automatic gain pump on ambience?** Listen to FungiSync / GravField takes; if it
  does, Raw with a fixed `MicrophoneGain` is the fallback for environment-only recordings.

## Migration from 0.6.0

| 0.6.0 | Proposed |
|---|---|
| `AudioMode = Environment`, `EnvironmentProcessing = p` | game off, microphone on, processing `p` |
| `AudioMode = GameAndMicrophone`, `IncludeMicrophone = true` | game on, microphone on, **VoiceChat** |
| `AudioMode = GameAndMicrophone`, `IncludeMicrophone = false` | game on, microphone off |
| Recorder without microphone input (created one implicitly) | unchanged fields; microphone opened by the recorder itself, no scene object needed |
| `HoloKitMicrophoneCapture.Acquire/Release/ReadLatestSamples` | `HoloKitMicrophone.Open` → handle (old names obsolete) |
| `RecordIn4K` | `CaptureCameraIn4K` (`FormerlySerializedAs`) |

An editor upgrader converts `_audioMode`/`_includeMicrophone` to the two switches on load; old
properties remain as obsolete forwarding members for one release.

## Priority

- **P0** keep the take alive across session changes (Unity `Microphone.Start`/`End` currently destroy
  the whole movie); restart after interruption; keep `PlayAndRecord` while holding an input; two
  switches + independent processing; no implicit microphone; `HoloKitMicrophone.Open`; migration.
- **P1** `RecordingFinished` with interruptions and effective processing; Unity-mic warnings (rule 5);
  speaker-route warning (rule 6); ducking measurement; MOFA Raw vs Default takes for post.
- **P2** optional on-device leakage removal in the existing mixed-copy export (`HoloKitMixedMovie`,
  already an offline AVAssetReader/Writer pass): subtract the game track from the mic track before
  mixing, so the Photos copy is clean while the master keeps both raw tracks. Apple's voice processing
  cannot run offline (`setVoiceProcessingEnabled` in an offline manual-rendering `AVAudioEngine`
  fails with -10863, kAudioUnitErr_CannotDoInCurrentContext; checked on macOS 27, iOS not yet
  verified), so use an embeddable echo canceller that takes a render (game) and a capture (mic)
  stream: WebRTC AEC3 or SpeexDSP (both BSD). Offline it can also search the delay and run
  non-causally, which real-time cancellers cannot.
- **Done (0.6.0)** `CaptureCameraIn4K` (renamed from `RecordIn4K`) and a true 4K output in Stereo
  (`StereoResolution`, 2K / 4K, as the Swift SDK).
- **Deferred** `Screen` video source, custom PCM tracks, frame rate /
  bitrate / Photos toggles, watermark (MOFA had one, currently disabled).
