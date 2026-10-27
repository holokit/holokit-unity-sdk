#!/usr/bin/env python3
"""Run the HoloKit feature probe on a connected iPhone and drive its stimuli from this Mac.

The phone camera must face a Safari window on this Mac. The runner copies the one-shot mode
file into the app container, launches the app with its console attached, switches the Safari
stimulus page when the probe asks for it, then collects results and screenshots.

Before camera suites a short preflight run checks that the camera sees all four corners of the
stimulus window and that the phone is not thermally Serious; it stops with what to fix otherwise.
Failures caused by the environment are reported as untested with the reason, not as SDK failures:
suites during which the phone was touched (the probe's overlay blocks and counts touches), and
rate checks while the phone was Serious or hotter (ARKit drops to 30 Hz).

It never changes the system volume. The 440 Hz tone for the microphone check is played only
with --allow-speaker; otherwise that check is reported as untested.

Usage:
  python3 run_feature_probe.py --device UDID --bundle-id ID --output DIR [--suites all] [--allow-speaker]
"""
import argparse
import json
import math
import pathlib
import queue
import threading
import struct
import subprocess
import tempfile
import time
import uuid
import wave

import numpy as np
from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
PAGE = HERE / 'sdk_stimuli.html'
PAGE_URL = PAGE.as_uri()  # with this run's query string once open_safari has run

# Suites whose checks need the camera to see the Safari stimulus window (preflight first).
CAMERA_SUITES = {'orientation', 'hand', 'handmotion', 'handvideo', 'gesturevideo', 'gesture', 'gaze', 'pinchtoplace'}
# Checks that measure rates or timing; at the Serious thermal state ARKit drops to 30 Hz, so their
# failures then are untested, not SDK failures.
RATE_CHECKS = {'A6.native-frames', 'B0.vision-runs', 'M1.update-rate', 'M2.latency', 'M3.continuity', 'L5.frame-rate',
               'V1.detection', 'V3.latency', 'W1.gesture-agreement', 'W2.pinch-events'}
THERMAL = ['Nominal', 'Fair', 'Serious', 'Critical']
SERIOUS = 2


def devicectl(*args, check=True):
    return subprocess.run(['xcrun', 'devicectl', *args], capture_output=True, text=True, check=check)


def set_stimulus(name):
    url = f'{PAGE_URL}#{name}'
    subprocess.run(['osascript', '-e', f'tell application "Safari" to set URL of current tab of front window to "{url}"'],
                   capture_output=True)


def video_start_delay(received, timeout=5.0):
    """Seconds from receiving the probe's stimulus line to the video's first playing frame."""
    deadline = time.time() + timeout
    while time.time() < deadline:
        title = subprocess.run(['osascript', '-e', 'tell application "Safari" to get name of current tab of front window'],
                               capture_output=True, text=True).stdout.strip()
        if title.startswith('playing:'):
            return int(title.split(':')[1]) / 1000 - received
        time.sleep(0.02)
    return None


def stop_speaker(process):
    import os, signal
    try:
        os.killpg(process.pid, signal.SIGTERM)   # speech loop runs in its own process group
    except (ProcessLookupError, PermissionError):
        process.terminate()


def open_safari(run_id):
    # Only brings Safari forward; the window stays where the user placed it (facing the phone). A new
    # query string loads the current page: a hash change alone keeps an older copy whose stimuli differ.
    global PAGE_URL
    PAGE_URL = f'{PAGE.as_uri()}?run={run_id}'
    subprocess.run(['osascript', '-e', 'tell application "Safari" to activate'], capture_output=True)
    set_stimulus('blank')


def write_tone(path, seconds=8.0, frequency=440.0, rate=44100):
    with wave.open(str(path), 'w') as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(rate)
        count = int(seconds * rate)
        out.writeframes(b''.join(struct.pack('<h', int(26000 * min(1, i / 441, (count - i) / 441) *
                                                        math.sin(2 * math.pi * frequency * i / rate)))
                                 for i in range(count)))


def run(args):
    out = pathlib.Path(args.output)
    out.mkdir(parents=True, exist_ok=True)
    if not (HERE / 'stimuli' / 'person.jpg').exists() or not all((HERE / 'videos' / f'{v}.truth.json').exists()
                                                                for v in ('hand-test', 'gesture-test')):
        subprocess.run(['python3', str(HERE / 'fetch_stimuli.py')], check=True)
    run_id = uuid.uuid4().hex[:12]
    mode = tempfile.NamedTemporaryFile('w', suffix='.txt', delete=False)
    mode.write(f'{args.suites}\n{run_id}\n' + ''.join(f'{line}\n' for line in args.set))
    mode.close()
    devicectl('device', 'copy', 'to', '--device', args.device, '--domain-type', 'appDataContainer',
              '--domain-identifier', args.bundle_id, '--source', mode.name, '--destination', 'Documents/sdk-test-mode.txt')
    tone = out / 'tone440.wav'
    write_tone(tone)
    open_safari(run_id)

    log = open(out / 'device.log', 'w')
    speaker = None
    stimuli = []
    timing = {}
    completed = False
    process = subprocess.Popen(['xcrun', 'devicectl', 'device', 'process', 'launch', '--device', args.device,
                                '--terminate-existing', '--console', args.bundle_id],
                               stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1)
    deadline = time.time() + args.timeout
    lines = queue.Queue()
    threading.Thread(target=lambda: [lines.put(l) for l in process.stdout] + [lines.put(None)], daemon=True).start()
    try:
        while time.time() < deadline:
            try:
                line = lines.get(timeout=1)
            except queue.Empty:
                continue
            if line is None:
                break
            log.write(line)
            log.flush()
            if '[SDK-STIM]' in line:
                name = line.split('[SDK-STIM]')[1].split()[0]
                stimuli.append((time.time(), name))
                if speaker:
                    stop_speaker(speaker)
                    speaker = None
                received = time.time()
                set_stimulus(name)
                if name in ('handvideo', 'gesturevideo'):
                    timing[name] = video_start_delay(received)
                if name == 'tone-440' and args.allow_speaker:
                    speaker = subprocess.Popen(['afplay', str(tone)])
                if name == 'speech' and args.allow_speaker:
                    # Continuous speech until the next stimulus (Mac voice, current volume).
                    speaker = subprocess.Popen(['bash', '-c', 'while true; do say "HoloKit microphone test. One, two, three, four, five."; done'],
                                               start_new_session=True)
            if '[SDK-TEST]' in line and ('PASS' in line or 'FAIL' in line or 'SUITE' in line):
                print(line.strip()[line.find('[SDK-TEST]'):], flush=True)
            if '[SDK-TEST] COMPLETE' in line:
                completed = True
                break
    finally:
        if speaker:
            stop_speaker(speaker)
        time.sleep(2)
        process.terminate()
        set_stimulus('done')
        log.close()

    movies = [f'sdk-movie-stereo-{n}.mp4' for n in ('2k', '4k')]
    for name in ['sdk-probe-results.json', 'sdk-handvideo.csv', 'sdk-gesturevideo.csv'] + [f'sdk-{n}.png' for n in args.screenshots] + movies:
        devicectl('device', 'copy', 'from', '--device', args.device, '--domain-type', 'appDataContainer',
                  '--domain-identifier', args.bundle_id, '--source', f'Documents/{name}',
                  '--destination', str(out / name), check=False)
    (out / 'stimulus-timing.json').write_text(json.dumps(timing))
    return summarize(out, run_id, completed, args)


def mark_centroids(path):
    rgb = np.asarray(Image.open(path).convert('RGB')).astype(int)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    masks = {'red': (r > 140) & (g < 90) & (b < 90), 'blue': (b > 140) & (r < 90) & (g < 120)}
    found = {}
    for key, mask in masks.items():
        ys, xs = np.nonzero(mask)
        if len(xs) >= 150:
            found[key] = (xs.mean(), ys.mean())
    return found


def orientation_poses(out):
    poses = {}
    log = out / 'device.log'
    for line in log.read_text(errors='replace').splitlines() if log.exists() else []:
        if '[SDK-ORIENT]' not in line:
            continue
        fields = dict(part.split('=', 1) for part in line.split('[SDK-ORIENT]')[1].split() if '=' in part)
        vec = lambda key: np.array([float(v) for v in fields.get(key, '0,0,0').split(',')])
        poses[fields['requested']] = {'effective': fields.get('effective'), 'pos': vec('pos'), 'fwd': vec('fwd')}
    return poses


def orientation_check(out):
    """A correct AR background rotates with the interface; the red->blue direction must follow it.

    Screenshots taken after the phone physically moved, or orientations iOS refused, are excluded.
    """
    poses = orientation_poses(out)
    reference = poses.get('Portrait')
    angles, skipped = {}, []
    for name in ['Portrait', 'LandscapeLeft', 'LandscapeRight', 'PortraitUpsideDown']:
        path = out / f'sdk-orient-{name}.png'
        pose = poses.get(name)
        if not path.exists() or pose is None:
            continue
        if pose['effective'] != name:
            skipped.append(f'{name}: iOS kept {pose["effective"]}')
            continue
        if reference is not None:
            moved = np.linalg.norm(pose['pos'] - reference['pos'])
            turned = math.degrees(math.acos(max(-1.0, min(1.0, float(np.dot(pose['fwd'], reference['fwd']))))))
            if moved > 0.02 or turned > 2:
                skipped.append(f'{name}: phone moved {moved * 100:.1f}cm/{turned:.1f}deg')
                continue
        marks = mark_centroids(path)
        if 'red' in marks and 'blue' in marks:
            (rx, ry), (bx, by) = marks['red'], marks['blue']
            angles[name] = math.degrees(math.atan2(by - ry, bx - rx))
        else:
            skipped.append(f'{name}: fiducial not visible')
    note = ('; excluded: ' + ', '.join(skipped)) if skipped else ''
    if 'Portrait' not in angles or not ({'LandscapeLeft', 'LandscapeRight'} & set(angles)):
        return None, f'not evaluable{note}'
    rel = {k: ((v - angles['Portrait'] + 180) % 360) - 180 for k, v in angles.items()}
    ok = all(abs(abs(rel[k]) - 90) < 15 for k in ('LandscapeLeft', 'LandscapeRight') if k in rel)
    if 'LandscapeLeft' in rel and 'LandscapeRight' in rel:
        ok &= abs(abs(rel['LandscapeLeft'] - rel['LandscapeRight']) - 180) < 20
    return ok, 'relative rotation vs Portrait: ' + ', '.join(f'{k}={v:.0f}deg' for k, v in rel.items()) + note


def video_frames(t, lag, fps, count):
    """Source-video frame index for each phone row at this phone-minus-video lag."""
    idx = np.round((t - lag) * fps).astype(int)
    return idx, (idx >= 0) & (idx < count)


def video_lag(t, n, x, y, gt_n, gt_x, gt_y, fps):
    """(R^2, lag): the lag whose affine fit of source wrist to phone wrist explains the phone best."""
    best = (-1.0, 0.0)
    for lag in np.arange(0, 2.5, 0.01):
        idx, valid = video_frames(t, lag, fps, len(gt_n))
        m = valid.copy()
        m[valid] &= (gt_n[idx[valid]] == 1) & (n[valid] >= 1)
        if m.sum() < 50:
            continue
        A = np.column_stack([gt_x[idx[m]], gt_y[idx[m]], np.ones(m.sum())])
        target = np.column_stack([x[m], y[m]])
        coef, *_ = np.linalg.lstsq(A, target, rcond=None)
        residual = ((A @ coef - target) ** 2).sum()
        total = ((target - target.mean(axis=0)) ** 2).sum()
        r2 = 1 - residual / total if total > 0 else 0
        if r2 > best[0]:
            best = (r2, lag)
    return best


# HandGestureClassifier and HandGestureFilter (Runtime/iOS/HandGestureClassifier.cs), applied to Vision's
# joints on the source video for gesture ground truth. Joints are [x, y, confidence] in JointName order.
NONE, PINCHED, FIVE = 0, 1, 2
GESTURE_NAMES = {NONE: 'None', PINCHED: 'Pinched', FIVE: 'Five'}
PINCH_RATIO, PINCH_THRESHOLD, MIN_CONFIDENCE, MIN_STRAIGHTNESS, MIN_FINGER_LENGTH = 0.25, 0.12, 0.35, 0.82, 0.025
FINGERS = [[1, 2, 3, 4], [5, 6, 7, 8], [9, 10, 11, 12], [13, 14, 15, 16], [17, 18, 19, 20]]


def _joint(joints, i):
    x, y, confidence = joints[i]
    return (x, y) if confidence >= MIN_CONFIDENCE else None


def _finger_length(joints, finger):
    length, previous = 0.0, None
    for i in finger:
        point = _joint(joints, i)
        if point is None:
            return 0.0
        if previous is not None:
            length += math.dist(previous, point)
        previous = point
    return length


def hand_size(joints):
    wrist, knuckle = _joint(joints, 0), _joint(joints, 9)
    size = math.dist(wrist, knuckle) if wrist and knuckle else 0.0
    return max(size, _finger_length(joints, FINGERS[1]), _finger_length(joints, FINGERS[2]))


def classify_gesture(joints):
    thumb, tip = _joint(joints, 4), _joint(joints, 8)
    if thumb and tip:
        size, distance = hand_size(joints), math.dist(thumb, tip)
        if (distance / size < PINCH_RATIO) if size > 0 else (distance < PINCH_THRESHOLD):
            return PINCHED
    for finger in FINGERS:
        length = _finger_length(joints, finger)
        if length <= MIN_FINGER_LENGTH:
            return NONE
        if math.dist(_joint(joints, finger[0]), _joint(joints, finger[-1])) / length < MIN_STRAIGHTNESS:
            return NONE
    return FIVE


def filtered_gestures(frames, rate=20.0, required=4, grace=0.2):
    """The SDK's gesture per source frame: Vision every 1/rate s (FrameInterval 3 at 60 Hz) on the
    first hand through HandGestureFilter. -1 where the source shows two hands (the phone's first
    hand is then ambiguous)."""
    times = np.array([f['t'] for f in frames])
    out = np.full(len(frames), NONE)
    gesture, candidate, evidence, last_seen, i = NONE, NONE, 0, None, 0
    for now in np.arange(times[0], times[-1] + 1e-9, 1 / rate):
        hands = frames[int(np.argmin(np.abs(times - now)))]['hands']
        if len(hands) == 1 and 'joints' in hands[0]:
            observed = classify_gesture(hands[0]['joints'])
            last_seen = now
            evidence = evidence + 1 if observed == candidate else 1
            candidate = observed
            if evidence >= required:
                gesture = observed
        elif not hands and (last_seen is None or now - last_seen >= grace):
            gesture, candidate, evidence, last_seen = NONE, NONE, 0, None
        while i < len(frames) and times[i] < now + 1 / rate:
            out[i] = -1 if len(frames[i]['hands']) > 1 else gesture
            i += 1
    return out


def episodes(times, values, target, minimum=0.2):
    """[(start, end)] of runs where values == target lasting at least `minimum` seconds."""
    runs, start = [], None
    for i, v in enumerate(values):
        if v == target and start is None:
            start = times[i]
        if (v != target or i == len(values) - 1) and start is not None:
            end = times[i]
            if end - start >= minimum:
                runs.append((start, end))
            start = None
    return runs


def handvideo_checks(out):
    """Compare phone tracking of the real hand video with Vision on the source frames."""
    rows_path, truth_path = out / 'sdk-handvideo.csv', HERE / 'videos' / 'hand-test.truth.json'
    if not rows_path.exists() or not truth_path.exists():
        return []
    truth = json.loads(truth_path.read_text())
    fps, frames = truth['fps'], truth['frames']
    gt_n = np.array([len(f['hands']) for f in frames])
    first = [f['hands'][0] if f['hands'] else {'x': np.nan, 'y': np.nan, 'pinch': np.nan} for f in frames]
    gt_x, gt_y, gt_p = (np.array([h[k] for h in first]) for k in ('x', 'y', 'pinch'))
    rows = [line.split(',') for line in rows_path.read_text().splitlines()[1:] if line]
    t = np.array([float(r[0]) for r in rows])
    n = np.array([int(r[1]) for r in rows])
    both = np.array([int(r[2]) for r in rows])
    num = lambda i: np.array([float(r[i]) if r[i] else np.nan for r in rows])
    x, y, p = num(3), num(4), num(5)

    r2, lag = video_lag(t, n, x, y, gt_n, gt_x, gt_y, fps)
    timing_path = out / 'stimulus-timing.json'
    switch = json.loads(timing_path.read_text()).get('handvideo') if timing_path.exists() else None
    idx, valid = video_frames(t, lag, fps, len(frames))
    gi = idx[valid]
    recall = float(np.mean(n[valid][gt_n[gi] >= 1] >= 1)) if np.any(gt_n[gi] >= 1) else 0.0
    two = gt_n[gi] >= 2
    two_agree = float(np.mean(both[valid][two] >= 2)) if two.any() else float('nan')
    pm = (gt_n[gi] == 1) & (n[valid] >= 1) & ~np.isnan(p[valid]) & ~np.isnan(gt_p[gi])
    pinch_r = float(np.corrcoef(p[valid][pm], gt_p[gi][pm])[0, 1]) if pm.sum() > 20 else float('nan')
    return [
        {'id': 'V1.detection', 'pass': bool(recall >= 0.9), 'detail': f'recall={recall:.2f} over {valid.sum()} phone frames'},
        {'id': 'V2.trajectory', 'pass': bool(r2 >= 0.8), 'detail': f'affine-fit R2={r2:.3f} (wrist, phone vs source video)'},
        {'id': 'V3.latency', 'pass': None if switch is None else bool(lag - switch <= 0.25),
         'detail': (f'display-to-pose latency={(lag - switch) * 1000:.0f} ms (alignment lag {lag * 1000:.0f} ms '
                    f'minus stimulus switch {switch * 1000:.0f} ms; includes log transport)') if switch is not None
                   else f'alignment lag={lag * 1000:.0f} ms; stimulus switch delay not measured'},
        {'id': 'V4.pinch-distance', 'pass': bool(pinch_r >= 0.7), 'detail': f'pinch distance correlation r={pinch_r:.2f} (n={int(pm.sum())})'},
        {'id': 'V5.two-hands', 'pass': bool(two_agree >= 0.6), 'detail': f'two-hand agreement={two_agree:.2f} on {int(two.sum())} two-hand frames'},
    ]


def gesturevideo_checks(out):
    """Gestures on real moving hands: the phone's HandGestureRecognitionManager against the SDK's
    classifier and filter applied to Vision on the source frames (gesture-test.mp4)."""
    rows_path, truth_path = out / 'sdk-gesturevideo.csv', HERE / 'videos' / 'gesture-test.truth.json'
    if not rows_path.exists() or not truth_path.exists():
        return []
    truth = json.loads(truth_path.read_text())
    fps, frames = truth['fps'], truth['frames']
    gt_t = np.array([f['t'] for f in frames])
    gt_n = np.array([len(f['hands']) for f in frames])
    first = [f['hands'][0] if f['hands'] else {'x': np.nan, 'y': np.nan} for f in frames]
    gt_x, gt_y = (np.array([h[k] for h in first]) for k in ('x', 'y'))
    gt_g = filtered_gestures(frames)
    rows = [line.split(',') for line in rows_path.read_text().splitlines()[1:] if line]
    t = np.array([float(r[0]) for r in rows])
    n = np.array([int(r[1]) for r in rows])
    g = np.array([int(r[2]) for r in rows])
    num = lambda i: np.array([float(r[i]) if r[i] else np.nan for r in rows])
    r2, lag = video_lag(t, n, num(3), num(4), gt_n, gt_x, gt_y, fps)
    idx, valid = video_frames(t, lag, fps, len(frames))

    # Agreement where the source gesture is settled: one hand and the same gesture 0.3 s either side.
    w = int(round(0.3 * fps))
    settled = np.array([w <= i < len(gt_g) - w and gt_g[i] >= 0 and bool(np.all(gt_g[i - w:i + w + 1] == gt_g[i]))
                        for i in range(len(gt_g))])
    m = valid.copy()
    m[valid] &= settled[idx[valid]]
    truth_g, phone_g = gt_g[idx[m]], g[m]
    agreement = float(np.mean(truth_g == phone_g)) if m.any() else float('nan')
    classes = ', '.join(f'{GESTURE_NAMES[k]} {np.mean(phone_g[truth_g == k] == k):.2f} (n={int((truth_g == k).sum())})'
                        for k in (NONE, PINCHED, FIVE) if (truth_g == k).any())

    # Pinch events, in source-video time.
    slack = 0.3
    truth_pinches = episodes(gt_t, gt_g, PINCHED)
    phone_pinches = episodes(t[valid] - lag, g[valid], PINCHED)
    overlaps = lambda a, b: a[0] <= b[1] + slack and b[0] <= a[1] + slack
    found = sum(any(overlaps(a, b) for b in phone_pinches) for a in truth_pinches)
    correct = sum(any(overlaps(b, a) for a in truth_pinches) for b in phone_pinches)
    recall = found / len(truth_pinches) if truth_pinches else float('nan')
    precision = correct / len(phone_pinches) if phone_pinches else 0.0
    return [
        {'id': 'W1.gesture-agreement', 'pass': None if m.sum() < 100 or r2 < 0.8 else bool(agreement >= 0.8),
         'detail': f'agreement={agreement:.2f} over {int(m.sum())} settled frames ({classes}); '
                   f'alignment R2={r2:.3f} lag={lag * 1000:.0f} ms'},
        {'id': 'W2.pinch-events', 'pass': None if len(truth_pinches) < 3 or r2 < 0.8 else bool(recall >= 0.8 and precision >= 0.8),
         'detail': f'recall={found}/{len(truth_pinches)} precision={correct}/{len(phone_pinches)} (slack {slack:.1f} s)'},
    ]


def movie_checks(out):
    """Stereo takes: 16:9 at 2K (2560x1440) or 4K (3840x2160), with frames, as the Swift SDK records."""
    results = []
    for name, size in (('2k', (2560, 1440)), ('4k', (3840, 2160))):
        path = out / f'sdk-movie-stereo-{name}.mp4'
        if not path.exists():
            continue
        probe = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-count_frames', '-show_entries',
                                'stream=width,height,avg_frame_rate,nb_read_frames,duration', '-of', 'json', str(path)],
                               capture_output=True, text=True)
        try:
            stream = json.loads(probe.stdout)['streams'][0]
        except (ValueError, KeyError, IndexError):
            results.append({'id': f'M1.stereo-{name}', 'pass': False, 'detail': f'ffprobe failed: {probe.stderr.strip()}'})
            continue
        num, den = (int(v) for v in stream.get('avg_frame_rate', '0/1').split('/'))
        fps = num / den if den else 0
        frames = int(stream.get('nb_read_frames', 0))
        ok = (stream['width'], stream['height']) == size and frames > 0
        results.append({'id': f'M1.stereo-{name}', 'pass': ok,
                        'detail': f"{stream['width']}x{stream['height']} {fps:.1f} fps frames={frames} duration={stream.get('duration')}s"})
    return results


def frame_corners(path):
    """Which of the aim stimulus's four corner marks the screenshot shows (at least 150 pixels each)."""
    rgb = np.asarray(Image.open(path).convert('RGB')).astype(int)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    masks = {'top-left green': (g > 120) & (r < 90) & (b < 90), 'top-right magenta': (r > 140) & (b > 140) & (g < 90),
             'bottom-left cyan': (g > 140) & (b > 140) & (r < 90), 'bottom-right yellow': (r > 140) & (g > 140) & (b < 90)}
    return {name: int(mask.sum()) >= 150 for name, mask in masks.items()}


def preflight(args):
    """Before a long run: the camera must see the whole stimulus window and the phone must not be hot.
    Returns the problems, each with what to do."""
    out = pathlib.Path(args.output) / 'preflight'
    run(argparse.Namespace(**{**vars(args), 'suites': 'aim', 'output': str(out), 'screenshots': ['aim'], 'timeout': 120}))
    problems = []
    shot = out / 'sdk-aim.png'
    if not shot.exists():
        problems.append(f'no aim screenshot (see {out / "device.log"})')
    else:
        missing = [name for name, seen in frame_corners(shot).items() if not seen]
        if missing:
            problems.append(f'the screenshot does not show the whole stimulus window (missing: {", ".join(missing)}); '
                            f'aim the camera so all four coloured corners of the Safari window are in view (screenshot: {shot})')
    results = json.loads((out / 'sdk-probe-results.json').read_text()).get('results', []) if (out / 'sdk-probe-results.json').exists() else []
    thermal = max((r.get('thermal', 0) for r in results), default=0)
    if thermal >= SERIOUS:
        problems.append(f'the phone is thermally {THERMAL[thermal]} (ARKit runs at 30 Hz); let it cool to Fair or Nominal')
    return problems


def summarize(out, run_id, completed, args):
    path = out / 'sdk-probe-results.json'
    report = json.loads(path.read_text()) if path.exists() else {'results': []}
    results = report.get('results', [])
    problems = []
    if report.get('runId') != run_id:
        problems.append(f'stale or missing result file (runId={report.get("runId")}, expected {run_id})')
    if not completed or not report.get('completed'):
        problems.append('probe did not complete')
    if any(r['id'] == 'A4.orientation-captured' for r in results):
        ok, detail = orientation_check(out)
        results.append({'id': 'A4.orientation-mac', 'pass': ok, 'detail': detail, 'suite': 'orientation'})
    # Only this run's video suites; the device keeps older CSVs in Documents.
    for suite, checks in (('handvideo', handvideo_checks), ('gesturevideo', gesturevideo_checks), ('stereobar', movie_checks)):
        if args.suites == 'all' or suite in args.suites.split(','):
            results.extend({**r, 'suite': suite} for r in checks(out))
    # Environment, not the SDK: report these failures as untested, with the reason.
    touched = set(report.get('touchedSuites', []))
    hot = {r.get('suite') for r in results if r.get('thermal', 0) >= SERIOUS}
    if touched:
        problems.append(f'the phone was touched during: {", ".join(sorted(touched))}')
    for r in results:
        reason = None
        if r['id'] in ('G2.tone-440', 'G3.default-gain') and not args.allow_speaker:
            reason = 'speaker not allowed'
        elif r['pass'] is False and r.get('suite') in touched:
            reason = 'phone touched during this suite'
        elif r['pass'] is False and r['id'] in RATE_CHECKS and r.get('suite') in hot:
            reason = 'phone thermally Serious or worse, ARKit at 30 Hz'
        if reason:
            r['pass'] = None
            r['detail'] = f'UNTESTED ({reason}); ' + r['detail']
    failed = [r for r in results if r['pass'] is False]
    summary = {'runId': run_id, 'completed': completed and report.get('completed', False), 'problems': problems,
               'passed': sum(r['pass'] is True for r in results), 'failed': len(failed),
               'untested': sum(r['pass'] is None for r in results), 'results': results}
    (out / 'summary.json').write_text(json.dumps(summary, indent=2))
    print(json.dumps({k: v for k, v in summary.items() if k != 'results'}, indent=2))
    for r in failed:
        print('FAIL', r['id'], r['detail'])
    return 0 if not failed and not problems else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--device', required=True)
    parser.add_argument('--bundle-id', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--suites', default='all')
    parser.add_argument('--set', action='append', default=[], help='extra mode lines, e.g. scene.hand=MyScene')
    parser.add_argument('--allow-speaker', action='store_true', help='play the 440 Hz tone on the current volume')
    parser.add_argument('--timeout', type=float, default=900)
    parser.add_argument('--no-preflight', action='store_true', help='skip the aim and thermal check before camera suites')
    parser.add_argument('--screenshots', nargs='*', default=[
        'stereo', 'mono', 'ui-Stereo', 'ui-Mono', 'ui-recording', 'stereobar', 'stereobar-recording-2k',
        'stereobar-recording-4k', 'hand-one', 'orient-Portrait', 'orient-LandscapeLeft',
        'orient-LandscapeRight', 'orient-PortraitUpsideDown'])
    args = parser.parse_args()
    suites = set(args.suites.split(',')) if args.suites != 'all' else CAMERA_SUITES
    if suites & CAMERA_SUITES and not args.no_preflight:
        problems = preflight(args)
        if problems:
            for problem in problems:
                print('PREFLIGHT:', problem)
            raise SystemExit(2)
        print('PREFLIGHT: stimulus window fully in view, phone not hot')
    raise SystemExit(run(args))


if __name__ == '__main__':
    main()
