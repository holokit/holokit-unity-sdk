# Feature probe (device)

An opt-in device probe for the SDK's non-recorder features. It compiles only in a
**Development** iOS build with the `HOLOKIT_SDK_TESTS` scripting define and runs only
when a one-shot `Documents/sdk-test-mode.txt` exists in the app container.

The probe loads the HoloKit sample scenes and checks:

| Suite | Checks |
| --- | --- |
| `device` | LiDAR support, thermal state, native uptime clock |
| `rendering` | phone-model profile, Mono/Stereo switching and events, eye viewports, eye separation, center-eye pose |
| `orientation` | AR background in four interface orientations (the runner compares a fiducial across screenshots), and the virtual camera's field of view against ARKit's own projection for each |
| `ui` | `HoloKitDefaultUICanvas` render-mode button wiring and label, and the button staying in Stereo while the stereo bar is off (the default) (button invoked, not physically tapped) |
| `tracking` | native ARSession frame callback rate/age, delegate restore, low-latency stereo pose, world-origin reset |
| `enhancement` | 4K background on/off, stereo rule, recording override, restore |
| `microphone` | shared `HoloKitMicrophoneCapture` leases, conflicting processing, 440 Hz detection |
| `hand` | no hand, one hand + 3D joints/depth, two hands, left-right sweep, lost event |
| `handmotion` | first-person hand moving on a known 4 s sine: pose update rate, capture-to-pose latency, continuity, trajectory R², depth stability, pinch while moving |
| `handvideo` | real first-person hands moving (Pexels clips in `gesture-test.mp4`) played in Safari; the runner compares phone tracking with Vision on the source frames: detection recall, affine trajectory R², pinch-distance correlation, two-hand agreement (untested when the clips show too few two-hand frames; B3 covers two hands), alignment lag |
| `gesturevideo` | real hands making gestures while they move (Pexels clips in `gesture-test.mp4`); the runner applies the SDK's gesture classifier and filter to Vision on the source frames and compares the phone's `HandGestureRecognitionManager`: gesture agreement on settled frames (W1), pinch-event recall and precision (W2) |
| `gesture` | pinch and five gestures against alternating photos |
| `stereobar` | turns the stereo bar on; its controls and millimetre layout, Default UI Canvas giving way, alignment guide auto-hide, TRIGGER event and custom label, thermal frame rate, Stereo 2K / 4K takes through RECORD (the runner checks each movie's size and frame rate with ffprobe), MONO |
| `lowlatency` | Low Latency Tracking sample in Stereo: prediction on, render-to-present latency, prediction lead, IMU sample age, frame rate |
| `pinchtoplace` | Pinch To Place sample: fingertip dots, a placement from a synthetic pinch, objects placed on the pinch photo and not on the open hand |
| `gaze` | gaze enter/select/exit, gaze + pinch selection, no selection off target |

Scene names default to the sample scenes. Override one with a mode line
`scene.<suite>=<SceneName>`; `expect.lidar=0` for devices without LiDAR.

## Running without a person

Mount the phone with its camera facing a Safari window on the Mac.

```sh
python3 Tests/Probe/Tools~/fetch_stimuli.py        # once; Commons photos and Pexels hand videos, with Vision
                                                   # ground truth (stimuli/ and videos/CREDITS.txt; needs ffmpeg)
python3 Tests/Probe/Tools~/run_feature_probe.py --device UDID --bundle-id BUNDLE --output /tmp/sdk-probe
```

The runner copies the mode file, launches the app with `devicectl --console`, switches
`sdk_stimuli.html` when the probe logs `[SDK-STIM] <name> <seconds>`, collects
`sdk-probe-results.json` and screenshots, and writes `summary.json`. It never changes the
system volume. The 440 Hz microphone check plays a tone only with `--allow-speaker`;
otherwise it is reported as untested, not passed.

Hand stimuli are shown at arm's length. A head-worn HoloKit sees a real hand (wrist to middle
fingertip about 18.5 cm) 0.25-0.65 m from its camera, so every hand photo spans about 85% of the
window's height and the videos are zoomed 1.4x in a crop where their median hand does the same (the
window must be at least 1.6 times as wide as it is tall). The video checks score only ground-truth
hands wholly inside that crop.

Known limitation (0.6.0-preview.2): through the phone's camera, the zoomed video hands are often
not detectable, not even by Vision on the Mac in the phone's own frames (`sdk-handvideo-10s.png`,
`sdk-gesturevideo-30s.png`): hands cut by the crop and low contrast. So V1/V2 and W1/W2 fail or stay
untested for stimulus reasons; their thresholds (0.8) are provisional until clips with whole,
high-contrast hands at arm's length replace these. Moving hands and gestures are covered meanwhile by
`handmotion` (M1-M6, including pinches while moving) and `gesture` (C1, C2).

Before camera suites the runner launches a short `aim` run and stops with what to fix
(`--no-preflight` skips it) unless:

- the phone sees all four coloured corners of the Safari window;
- nothing covers the window, such as a notification (turn on Do Not Disturb);
- the window's hands look like a real hand within arm's reach: the runner turns the window's height
  in the camera and the camera's field of view into that distance, and when it is beyond 0.65 m says
  how much closer to move the phone;
- Vision finds the hand photo through the camera;
- the phone is not thermally Serious.

The runner keeps the Mac's display awake (`caffeinate`): a locked screen hides the stimuli. Suites
run hand and camera first, while the phone is cool. Leave the Mac and the phone alone during a run:
the phone's camera also sees the Mac's keyboard (a typing hand is a real hand in view), and a window
brought in front of Safari hides the stimulus. The runner watches the window stack about once a second
(`windows.swift`) and reports failures in camera suites run while the stimulus was covered as
untested, naming the app; the video suites leave out frames where the phone tracked a hand outside the
shown video; and the runner stops at once, naming the suite, when the app goes to the background (the
phone locked, swiped to Home, a call, or iPhone Mirroring of the test phone).
Failures caused by the environment are reported as untested with the reason: a suite during which
the phone was touched (an invisible overlay blocks and counts touches), and rate or timing checks
while the phone was Serious or hotter.

On-screen photos stand in for real hands. They exercise Vision/LiDAR integration and the
gesture logic, not real-hand latency or accuracy. Grant camera and microphone permission to
the bundle once before unattended runs.

Build the probe player from a carrier project that lists this package in `testables`:

```sh
Unity -batchmode -quit -projectPath <carrier> -buildTarget iOS \
  -executeMethod HoloKit.Tests.Editor.HoloKitProbeBuild.Build -buildOutput Build_Probe [-probeBundleId <id>]
```

It first copies the package's `Samples~` into `Assets/Samples` (`HoloKitSampleSync`, also under
**HoloKit > Samples**), so the probe always runs the current sample scenes.
