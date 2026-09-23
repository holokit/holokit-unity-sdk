#!/usr/bin/env python3
"""Run the HoloKit feature probe on a connected iPhone and drive its stimuli from this Mac.

The phone camera must face a Safari window on this Mac. The runner copies the one-shot mode
file into the app container, launches the app with its console attached, switches the Safari
stimulus page when the probe asks for it, then collects results and screenshots.

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


def devicectl(*args, check=True):
    return subprocess.run(['xcrun', 'devicectl', *args], capture_output=True, text=True, check=check)


def set_stimulus(name):
    url = f'{PAGE.as_uri()}#{name}'
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


def open_safari():
    # Only brings Safari forward; the window stays where the user placed it (facing the phone).
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
    if not (HERE / 'stimuli' / 'person.jpg').exists() or not (HERE / 'videos' / 'hand-test.truth.json').exists():
        subprocess.run(['python3', str(HERE / 'fetch_stimuli.py')], check=True)
    run_id = uuid.uuid4().hex[:12]
    mode = tempfile.NamedTemporaryFile('w', suffix='.txt', delete=False)
    mode.write(f'{args.suites}\n{run_id}\n' + ''.join(f'{line}\n' for line in args.set))
    mode.close()
    devicectl('device', 'copy', 'to', '--device', args.device, '--domain-type', 'appDataContainer',
              '--domain-identifier', args.bundle_id, '--source', mode.name, '--destination', 'Documents/sdk-test-mode.txt')
    tone = out / 'tone440.wav'
    write_tone(tone)
    open_safari()

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
                    speaker.terminate()
                    speaker = None
                received = time.time()
                set_stimulus(name)
                if name == 'handvideo':
                    timing['handvideo'] = video_start_delay(received)
                if name == 'tone-440' and args.allow_speaker:
                    speaker = subprocess.Popen(['afplay', str(tone)])
            if '[SDK-TEST]' in line and ('PASS' in line or 'FAIL' in line or 'SUITE' in line):
                print(line.strip()[line.find('[SDK-TEST]'):], flush=True)
            if '[SDK-TEST] COMPLETE' in line:
                completed = True
                break
    finally:
        if speaker:
            speaker.terminate()
        time.sleep(2)
        process.terminate()
        set_stimulus('done')
        log.close()

    for name in ['sdk-probe-results.json', 'sdk-handvideo.csv'] + [f'sdk-{n}.png' for n in args.screenshots]:
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

    def at_lag(lag):
        idx = np.round((t - lag) * fps).astype(int)
        valid = (idx >= 0) & (idx < len(frames))
        return idx, valid

    best = (-1.0, 0.0)
    for lag in np.arange(0, 2.5, 0.01):
        idx, valid = at_lag(lag)
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
    r2, lag = best
    timing_path = out / 'stimulus-timing.json'
    switch = json.loads(timing_path.read_text()).get('handvideo') if timing_path.exists() else None
    idx, valid = at_lag(lag)
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
        results.append({'id': 'A4.orientation-mac', 'pass': ok, 'detail': detail})
    results.extend(handvideo_checks(out))
    if not args.allow_speaker:
        for r in results:
            if r['id'] == 'G2.tone-440':
                r['pass'] = None
                r['detail'] = 'UNTESTED (speaker not allowed); ' + r['detail']
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
    parser.add_argument('--screenshots', nargs='*', default=[
        'stereo', 'mono', 'ui-Stereo', 'ui-Mono', 'ui-recording', 'orient-Portrait', 'orient-LandscapeLeft',
        'orient-LandscapeRight', 'orient-PortraitUpsideDown'])
    raise SystemExit(run(parser.parse_args()))


if __name__ == '__main__':
    main()
