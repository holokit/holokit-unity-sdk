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
| `handvideo` | real hand video (Pexels, first-person style) played in Safari; the runner compares phone tracking with Vision on the source frames: detection recall, affine trajectory R², pinch-distance correlation, two-hand agreement, alignment lag |
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
python3 Tests/Probe/Tools~/fetch_stimuli.py        # once; downloads Commons photos (see stimuli/CREDITS.txt)
python3 Tests/Probe/Tools~/run_feature_probe.py --device UDID --bundle-id BUNDLE --output /tmp/sdk-probe
```

The runner copies the mode file, launches the app with `devicectl --console`, switches
`sdk_stimuli.html` when the probe logs `[SDK-STIM] <name> <seconds>`, collects
`sdk-probe-results.json` and screenshots, and writes `summary.json`. It never changes the
system volume. The 440 Hz microphone check plays a tone only with `--allow-speaker`;
otherwise it is reported as untested, not passed.

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
