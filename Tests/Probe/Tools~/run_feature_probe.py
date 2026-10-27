#!/usr/bin/env python3
"""Run the HoloKit feature probe on a connected iPhone and drive its stimuli from this Mac.

The phone camera must face a Safari window on this Mac. The runner copies the one-shot mode
file into the app container, launches the app with its console attached, switches the Safari
stimulus page when the probe asks for it, then collects results and screenshots.

Before camera suites a short preflight run checks that the camera sees all four corners of the
stimulus window, that its hands look as large as a real hand at arm's length (the view of a head-worn
HoloKit) and that Vision finds one, and that the phone is not thermally Serious; it stops with what to
fix otherwise.
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
import os
import pathlib
import queue
import re
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
# A head-worn HoloKit sees a real hand (wrist to middle fingertip about 18.5 cm) at arm's length,
# 0.25-0.65 m from its camera. Every hand stimulus spans HAND_HEIGHT of the window's height
# (sdk_stimuli.html), so the window's size in the camera says how far away its hands look.
HAND_LENGTH_M, ARM_REACH_M, HAND_HEIGHT = 0.185, 0.65, 0.85
CORNER_SIZE = 0.14  # the aim corner marks are 14vmin squares
# The videos are zoomed VIDEO_ZOOM times in a crop VIDEO_CROP_WIDTH window-heights wide (16:9 clips).
VIDEO_ZOOM, VIDEO_CROP_WIDTH = 1.4, 1.6


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


def stimulus_cover(windows):
    """(fraction, owners): how much of the Safari stimulus window (Safari's largest normal window) the
    windows in front of it cover, on a 32 x 20 grid of points."""
    safari = [(i, w) for i, w in enumerate(windows) if w['owner'] == 'Safari' and w['layer'] == 0]
    if not safari:
        return 1.0, ['Safari window not on screen']
    index, s = max(safari, key=lambda iw: iw[1]['w'] * iw[1]['h'])
    gx, gy = np.meshgrid(s['x'] + (np.arange(32) + 0.5) * s['w'] / 32, s['y'] + (np.arange(20) + 0.5) * s['h'] / 20)
    covered, owners = np.zeros(gx.shape, bool), []
    for w in windows[:index]:
        inside = (gx >= w['x']) & (gx < w['x'] + w['w']) & (gy >= w['y']) & (gy < w['y'] + w['h'])
        if w['alpha'] >= 0.1 and inside.any():
            covered |= inside
            owners.append(w['owner'] or w['title'])
    return float(covered.mean()), owners


class StimulusWatch:
    """Polls the window stack about once a second during a run: another app brought in front of the
    Safari stimulus (someone using the Mac, a notification) hides it from the phone."""

    def __init__(self):
        self.samples, self.stop = [], threading.Event()
        self.binary = pathlib.Path(tempfile.gettempdir()) / 'holokit-probe-windows'
        built = subprocess.run(['swiftc', '-O', str(HERE / 'windows.swift'), '-o', str(self.binary)], capture_output=True)
        if built.returncode == 0:
            threading.Thread(target=self.poll, daemon=True).start()
        else:
            print('WARNING: cannot watch the stimulus window:', built.stderr.decode(errors='replace')[:200], flush=True)

    def poll(self):
        while not self.stop.wait(1.0):
            try:
                listed = subprocess.run([str(self.binary)], capture_output=True, text=True, timeout=5)
                self.samples.append((time.time(), *stimulus_cover(json.loads(listed.stdout))))
            except (subprocess.TimeoutExpired, json.JSONDecodeError, KeyError):
                continue

    def covered(self, start, end, threshold=0.02):
        """Apps that covered more than `threshold` of the stimulus between start and end."""
        return sorted({o for t, fraction, owners in self.samples if start <= t < end and fraction > threshold for o in owners})


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
    if not (HERE / 'stimuli' / 'person.jpg').exists() or not (HERE / 'videos' / 'gesture-test.truth.json').exists():
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
    completed, suite, interruption, suites = False, None, None, []
    watch = StimulusWatch()
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
            if '[SDK-TEST] SUITE' in line:
                suite = line.split('[SDK-TEST] SUITE')[1].strip()
                suites.append((time.time(), suite))
            # The probe cannot go on in the background; waiting for the timeout would only hide why.
            if 'applicationDidEnterBackground' in line:
                interruption = (f'the app went to the background during suite {suite}: the phone was locked, swiped '
                                f'to Home, or a call or another app came to the front; later suites were not run')
                print('STOPPED:', interruption, flush=True)
                break
            if '[SDK-TEST] COMPLETE' in line:
                completed = True
                break
    finally:
        watch.stop.set()
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
    ends = [start for start, _ in suites[1:]] + [time.time()]
    covered = {name: owners for (start, name), end in zip(suites, ends)
               if name in CAMERA_SUITES and (owners := watch.covered(start, end))}
    return summarize(out, run_id, completed, args, interruption, covered)


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
    """(R^2, lag, coef): the lag whose affine fit of source wrist to phone wrist explains the phone
    best; coef maps [source x, source y, 1] to the phone's image point."""
    best = (-1.0, 0.0, None)
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
            best = (r2, lag, coef)
    return best


def foreign_hands(x, y, coef, margin=0.1):
    """Phone rows whose hand lies outside the crop of the video the window shows, mapped back to the
    source through the alignment: a real hand in the camera's view, e.g. one at the Mac's keyboard."""
    outside = np.zeros(len(x), bool)
    if coef is None or abs(np.linalg.det(coef[:2])) < 1e-9:
        return outside
    seen = ~np.isnan(x) & ~np.isnan(y)
    source = (np.column_stack([x[seen], y[seen]]) - coef[2]) @ np.linalg.inv(coef[:2])
    half = np.array([VIDEO_CROP_WIDTH / 2 / (VIDEO_ZOOM * 16 / 9), 0.5 / VIDEO_ZOOM]) * (1 + margin)
    outside[seen] = (np.abs(source - 0.5) > half).any(axis=1)
    return outside


def real_hand_note(foreign, hands):
    """(untested, note) for a video suite: a real hand tracked in more than a tenth of the phone's
    hand frames leaves too little to judge the SDK by."""
    share = foreign.sum() / max(1, hands.sum())
    note = f'; {int(foreign.sum())} phone frames tracked a hand outside the video (a real hand in view) and are left out'
    return share > 0.1, note if foreign.any() else ''


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


def crop_truth(frames):
    """Source-frame ground truth as the window shows it: hands wholly inside the zoomed crop stay,
    hands wholly outside it go. Also returns, per frame, whether a hand is cut by the crop's edge;
    neither answer is right for those frames, so the checks leave them out."""
    half_x, half_y = VIDEO_CROP_WIDTH / 2 / (VIDEO_ZOOM * 16 / 9), 0.5 / VIDEO_ZOOM
    shown, cut = [], []
    for frame in frames:
        kept, partial = [], False
        for hand in frame['hands']:
            inside = [abs(x - 0.5) <= half_x and abs(y - 0.5) <= half_y
                      for x, y, confidence in hand['joints'] if confidence >= MIN_CONFIDENCE]
            if inside and all(inside):
                kept.append(hand)
            elif any(inside):
                partial = True
        shown.append({**frame, 'hands': kept})
        cut.append(partial)
    return shown, np.array(cut)


def handvideo_checks(out):
    """Compare phone tracking of the real hand video with Vision on the source frames."""
    rows_path, truth_path = out / 'sdk-handvideo.csv', HERE / 'videos' / 'gesture-test.truth.json'
    if not rows_path.exists() or not truth_path.exists():
        return []
    truth = json.loads(truth_path.read_text())
    fps, (frames, cut) = truth['fps'], crop_truth(truth['frames'])
    gt_n = np.array([len(f['hands']) for f in frames])
    first = [f['hands'][0] if f['hands'] else {'x': np.nan, 'y': np.nan, 'pinch': np.nan} for f in frames]
    gt_x, gt_y, gt_p = (np.array([h[k] for h in first]) for k in ('x', 'y', 'pinch'))
    rows = [line.split(',') for line in rows_path.read_text().splitlines()[1:] if line]
    t = np.array([float(r[0]) for r in rows])
    n = np.array([int(r[1]) for r in rows])
    both = np.array([int(r[2]) for r in rows])
    num = lambda i: np.array([float(r[i]) if r[i] else np.nan for r in rows])
    x, y, p = num(3), num(4), num(5)

    r2, lag, coef = video_lag(t, n, x, y, gt_n, gt_x, gt_y, fps)
    foreign = foreign_hands(x, y, coef) if r2 >= 0.8 else np.zeros(len(t), bool)
    interfered, note = real_hand_note(foreign, n >= 1)
    timing_path = out / 'stimulus-timing.json'
    switch = json.loads(timing_path.read_text()).get('handvideo') if timing_path.exists() else None
    idx, valid = video_frames(t, lag, fps, len(frames))
    valid[valid] &= ~cut[idx[valid]]
    valid &= ~foreign
    gi = idx[valid]
    recall = float(np.mean(n[valid][gt_n[gi] >= 1] >= 1)) if np.any(gt_n[gi] >= 1) else 0.0
    two = gt_n[gi] >= 2
    two_agree = float(np.mean(both[valid][two] >= 2)) if two.any() else float('nan')
    pm = (gt_n[gi] == 1) & (n[valid] >= 1) & ~np.isnan(p[valid]) & ~np.isnan(gt_p[gi])
    pinch_r = float(np.corrcoef(p[valid][pm], gt_p[gi][pm])[0, 1]) if pm.sum() > 20 else float('nan')
    checks = [
        {'id': 'V1.detection', 'pass': bool(recall >= 0.9),
         'detail': f'recall={recall:.2f} on {int((gt_n[gi] >= 1).sum())} phone frames with a whole hand in view '
                   f'({valid.sum()} phone frames; frames with a hand cut by the crop left out){note}'},
        {'id': 'V2.trajectory', 'pass': bool(r2 >= 0.8), 'detail': f'affine-fit R2={r2:.3f} (wrist, phone vs source video)'},
        {'id': 'V3.latency', 'pass': None if switch is None or r2 < 0.8 else bool(lag - switch <= 0.25),
         'detail': (f'display-to-pose latency={(lag - switch) * 1000:.0f} ms (alignment lag {lag * 1000:.0f} ms '
                    f'minus stimulus switch {switch * 1000:.0f} ms; includes log transport)') if switch is not None
                   else f'alignment lag={lag * 1000:.0f} ms; stimulus switch delay not measured'},
        {'id': 'V4.pinch-distance', 'pass': bool(pinch_r >= 0.7), 'detail': f'pinch distance correlation r={pinch_r:.2f} (n={int(pm.sum())})'},
        # The arm's-length clips rarely show two hands; B3 covers two hands with a photo.
        {'id': 'V5.two-hands', 'pass': None if two.sum() < 50 else bool(two_agree >= 0.6),
         'detail': f'two-hand agreement={two_agree:.2f} on {int(two.sum())} two-hand frames'},
    ]
    return [{**c, 'pass': None, 'detail': 'UNTESTED (a real hand in view); ' + c['detail']} if interfered else c
            for c in checks]


def gesturevideo_checks(out):
    """Gestures on real moving hands: the phone's HandGestureRecognitionManager against the SDK's
    classifier and filter applied to Vision on the source frames (gesture-test.mp4)."""
    rows_path, truth_path = out / 'sdk-gesturevideo.csv', HERE / 'videos' / 'gesture-test.truth.json'
    if not rows_path.exists() or not truth_path.exists():
        return []
    truth = json.loads(truth_path.read_text())
    fps, (frames, cut) = truth['fps'], crop_truth(truth['frames'])
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
    x, y = num(3), num(4)
    r2, lag, coef = video_lag(t, n, x, y, gt_n, gt_x, gt_y, fps)
    foreign = foreign_hands(x, y, coef) if r2 >= 0.8 else np.zeros(len(t), bool)
    interfered, note = real_hand_note(foreign, n >= 1)
    idx, valid = video_frames(t, lag, fps, len(frames))
    valid &= ~foreign

    # Agreement where the source gesture is settled: one hand and the same gesture 0.3 s either side,
    # with no hand cut by the crop's edge in that time.
    w = int(round(0.3 * fps))
    settled = np.array([w <= i < len(gt_g) - w and gt_g[i] >= 0 and bool(np.all(gt_g[i - w:i + w + 1] == gt_g[i]))
                        and not cut[i - w:i + w + 1].any() for i in range(len(gt_g))])
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
    checks = [
        {'id': 'W1.gesture-agreement', 'pass': None if m.sum() < 100 or r2 < 0.8 else bool(agreement >= 0.8),
         'detail': f'agreement={agreement:.2f} over {int(m.sum())} settled frames ({classes}); '
                   f'alignment R2={r2:.3f} lag={lag * 1000:.0f} ms{note}'},
        {'id': 'W2.pinch-events', 'pass': None if len(truth_pinches) < 3 or r2 < 0.8 else bool(recall >= 0.8 and precision >= 0.8),
         'detail': f'recall={found}/{len(truth_pinches)} precision={correct}/{len(phone_pinches)} (slack {slack:.1f} s)'},
    ]
    return [{**c, 'pass': None, 'detail': 'UNTESTED (a real hand in view); ' + c['detail']} if interfered else c
            for c in checks]


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


CORNER_MARKS = ['top-left green', 'top-right magenta', 'bottom-left cyan', 'bottom-right yellow']


def _patches(mask, block=8):
    """[(pixels, centre x, centre y, width, height)] of the mask's connected patches, found on a
    block x block grid."""
    h, w = mask.shape
    grid = mask[:h - h % block, :w - w % block].reshape(h // block, block, w // block, block).sum(axis=(1, 3))
    filled = grid >= block * block // 2
    seen = np.zeros_like(filled)
    patches = []
    for y0, x0 in zip(*np.nonzero(filled)):
        if seen[y0, x0]:
            continue
        seen[y0, x0] = True
        stack, cells = [(y0, x0)], []
        while stack:
            y, x = stack.pop()
            cells.append((y, x))
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < filled.shape[0] and 0 <= nx < filled.shape[1] and filled[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        ys, xs = np.array(cells).T
        patches.append((len(cells) * block * block, (xs.mean() + 0.5) * block, (ys.mean() + 0.5) * block,
                        (np.ptp(xs) + 1) * block, (np.ptp(ys) + 1) * block))
    return patches


def corner_marks(path, blank_path):
    """Centre of each of the aim stimulus's four corner marks, or None. Only pixels that changed from
    the blank-page screenshot taken from the same pose count, so the room, wallpaper and virtual
    objects do not; a mark is a square, solid patch of its hue among them (at least 150 pixels, at most
    a fifth of the image wide). The four must sit in their own corners relative to each other."""
    rgb = np.asarray(Image.open(path).convert('RGB')).astype(float) / 255
    blank = np.asarray(Image.open(blank_path).convert('RGB').resize(Image.open(path).size)).astype(float) / 255
    changed = np.abs(rgb - blank).max(axis=2) > 0.2
    high, low = rgb.max(axis=2), rgb.min(axis=2)
    chroma = high - low
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    with np.errstate(divide='ignore', invalid='ignore'):
        hue = np.where(high == r, (g - b) / chroma % 6, np.where(high == g, (b - r) / chroma + 2, (r - g) / chroma + 4)) * 60
    vivid = changed & (chroma > 0.25)
    marks = {}
    for name, centre in zip(CORNER_MARKS, [120, 300, 180, 60]):  # green, magenta, cyan, yellow
        mask = vivid & (np.abs((hue - centre + 180) % 360 - 180) < 30)
        squares = [p for p in _patches(mask) if p[0] >= 150 and 1 / 3 <= p[3] / p[4] <= 3
                   and p[0] >= 0.4 * p[3] * p[4] and p[3] <= 0.2 * mask.shape[1]]
        best = max(squares, default=None)
        marks[name] = (float(best[1]), float(best[2])) if best else None
    tl, tr, bl, br = (marks[name] for name in CORNER_MARKS)
    pairs = [(tl, tr, 0), (bl, br, 0), (tl, bl, 1), (tr, br, 1)]  # first must be left of / above second
    if any(first and second and first[axis] >= second[axis] for first, second, axis in pairs):
        return dict.fromkeys(CORNER_MARKS)
    return marks


def window_cover(blank_path, marks, block=16):
    """(fraction, where): how much of the stimulus window something covers in the blank-page
    screenshot (a notification, another window), and where its middle is. The blank page is one
    colour, so blocks far from the window's median colour are covered; only blocks inside the
    quadrilateral through the four mark centres count."""
    rgb = np.asarray(Image.open(blank_path).convert('RGB')).astype(float) / 255
    rows, cols = rgb.shape[0] // block, rgb.shape[1] // block
    means = rgb[:rows * block, :cols * block].reshape(rows, block, cols, block, 3).mean(axis=(1, 3))
    y, x = (np.mgrid[0:rows, 0:cols] + 0.5) * block
    quad = [marks[name] for name in ('top-left green', 'top-right magenta', 'bottom-right yellow', 'bottom-left cyan')]
    inside = np.ones((rows, cols), bool)
    for (x0, y0), (x1, y1) in zip(quad, quad[1:] + quad[:1]):  # clockwise on screen: inside is to the right
        inside &= (x1 - x0) * (y - y0) - (y1 - y0) * (x - x0) >= 0
    if not inside.any():
        return 0.0, ''
    deviation = np.abs(means - np.median(means[inside], axis=0)).max(axis=-1)
    covered = inside & (deviation > 0.3)
    if not covered.any():
        return 0.0, ''
    left, right = min(p[0] for p in quad), max(p[0] for p in quad)
    top, bottom = min(p[1] for p in quad), max(p[1] for p in quad)
    u, v = (x[covered].mean() - left) / (right - left), (y[covered].mean() - top) / (bottom - top)
    where = f'{["top", "middle", "bottom"][min(int(v * 3), 2)]} {["left", "middle", "right"][min(int(u * 3), 2)]}'
    return covered.sum() / inside.sum(), where


def hand_distance(marks, image_height, vertical_fov):
    """How far from the camera a real hand would be to look as large as the stimulus hands (metres),
    from the window's height between the corner marks (corner_marks) and the screenshot's height and
    vertical field of view; None without a mark on the left or right side."""
    tl, tr, bl, br = (marks[name] for name in CORNER_MARKS)
    spans = [bottom[1] - top[1] for top, bottom in ((tl, bl), (tr, br)) if top and bottom]
    if not spans:
        return None
    window = np.mean(spans) / (1 - CORNER_SIZE)  # mark centres sit half a mark (7vh) inside each edge
    focal = image_height / 2 / math.tan(math.radians(vertical_fov) / 2)
    return HAND_LENGTH_M * focal / (HAND_HEIGHT * window)


def preflight(args):
    """Before a long run: the camera must see the whole stimulus window, its hands must look like a
    hand within arm's reach and be found by Vision, and the phone must not be hot. Returns the
    problems, each with what to do."""
    out = pathlib.Path(args.output) / 'preflight'
    run(argparse.Namespace(**{**vars(args), 'suites': 'aim', 'output': str(out), 'screenshots': ['aim', 'aim-blank'],
                              'timeout': 120}))
    problems = []
    shot, blank = out / 'sdk-aim.png', out / 'sdk-aim-blank.png'
    if not shot.exists() or not blank.exists():
        problems.append(f'no aim screenshots (see {out / "device.log"})')
    else:
        marks = corner_marks(shot, blank)
        missing = [name for name, centre in marks.items() if centre is None]
        if len(missing) == 4:
            problems.append(f'no corner of the stimulus window is visible: is the Mac screen locked or asleep, or '
                            f'Safari not in front of the camera? (screenshot: {shot})')
        elif missing:
            problems.append(f'the screenshot does not show the whole stimulus window (missing: {", ".join(missing)}); '
                            f'aim the camera so all four coloured corners of the Safari window are in view (screenshot: {shot})')
        else:
            cover, where = window_cover(blank, marks)
            if cover > 0.01:
                problems.append(f'something covers {cover:.0%} of the stimulus window ({where}), e.g. a notification or '
                                f'another window: dismiss it and turn on Do Not Disturb for the run (screenshot: {blank})')
    results = json.loads((out / 'sdk-probe-results.json').read_text()).get('results', []) if (out / 'sdk-probe-results.json').exists() else []
    captured = next((r for r in results if r['id'] == 'aim.captured'), {'detail': ''})
    fov = re.search(r'verticalFov=([\d.]+)', captured['detail'])
    distance = hand_distance(marks, Image.open(shot).size[1], float(fov[1])) if fov and float(fov[1]) > 0 and not problems else None
    if distance is not None:
        print(f'PREFLIGHT: the stimulus hands look like a real hand {distance:.2f} m away')
    if distance is not None and distance > ARM_REACH_M:
        problems.append(f'the stimulus hands look like a real hand {distance:.2f} m away, beyond arm\'s reach (a head-worn '
                        f'HoloKit sees hands 0.25-{ARM_REACH_M} m away): move the phone to about {0.45 / distance:.0%} '
                        f'of its distance from the screen, keeping all four corners in view')
    hand = next((r for r in results if r['id'] == 'aim.hand'), None)
    if hand is not None and not hand['pass'] and not problems:
        seen = f'although they look like a hand {distance:.2f} m away' if distance is not None else 'with the hand distance unknown'
        problems.append(f'Vision does not find the hand photo reliably ({hand["detail"]}) {seen}: check for glare, '
                        f'blur or something covering the middle of the window (screenshot: {shot})')
    thermal = max((r.get('thermal', 0) for r in results), default=0)
    if thermal >= SERIOUS:
        problems.append(f'the phone is thermally {THERMAL[thermal]} (ARKit runs at 30 Hz); let it cool to Fair or Nominal')
    return problems


def summarize(out, run_id, completed, args, interruption=None, covered=None):
    path = out / 'sdk-probe-results.json'
    report = json.loads(path.read_text()) if path.exists() else {'results': []}
    results = report.get('results', [])
    problems = []
    if report.get('runId') != run_id:
        problems.append(f'stale or missing result file (runId={report.get("runId")}, expected {run_id})')
    if not completed or not report.get('completed'):
        problems.append(interruption or 'probe did not complete')
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
    covered = covered or {}
    if covered:
        problems.append('another window covered the stimulus during: ' +
                        ', '.join(f'{name} ({", ".join(owners)})' for name, owners in sorted(covered.items())))
    for r in results:
        reason = None
        if r['id'] in ('G2.tone-440', 'G3.default-gain') and not args.allow_speaker:
            reason = 'speaker not allowed'
        elif r['pass'] is False and r.get('suite') in touched:
            reason = 'phone touched during this suite'
        elif r['pass'] is False and r.get('suite') in covered:
            reason = f'stimulus covered by {", ".join(covered[r["suite"]])}'
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
        'orient-LandscapeRight', 'orient-PortraitUpsideDown', 'handvideo-10s', 'handvideo-30s', 'gesturevideo-10s',
        'gesturevideo-30s'])
    args = parser.parse_args()
    # Keep the Mac's display awake for the whole run: a locked or sleeping screen hides the stimuli.
    subprocess.Popen(['caffeinate', '-dimsu', '-w', str(os.getpid())])
    suites = set(args.suites.split(',')) if args.suites != 'all' else CAMERA_SUITES
    if suites & CAMERA_SUITES and not args.no_preflight:
        problems = preflight(args)
        if problems:
            for problem in problems:
                print('PREFLIGHT:', problem)
            raise SystemExit(2)
        print('PREFLIGHT: stimulus window fully in view, hands within arm\'s reach, phone not hot')
    raise SystemExit(run(args))


if __name__ == '__main__':
    main()
