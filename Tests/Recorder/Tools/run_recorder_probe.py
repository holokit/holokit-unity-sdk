#!/usr/bin/env python3
"""Run the installed SDK regression player and collect/validate its movies."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import time
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('--device', required=True)
parser.add_argument('--bundle-id', required=True)
parser.add_argument('--mode', choices=['stop', 'background', 'memory', 'audio', 'audio-long', 'audio-low-latency', 'audio-external', 'audio-external-camera', 'audio-external-reference'], default='stop')
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--external-roi', help='Camera image region containing the computer screen: normalized x,y,width,height')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
mode_file = args.output/'recorder-test-mode.txt'
run_id = uuid.uuid4().hex
mode_file.write_text(args.mode + "\n" + run_id)
subprocess.run(['xcrun', 'devicectl', 'device', 'copy', 'to', '--device', args.device,
                '--domain-type', 'appDataContainer', '--domain-identifier', args.bundle_id,
                '--source', str(mode_file), '--destination', 'Documents/recorder-test-mode.txt'], check=True)
launch = ['xcrun', 'devicectl', 'device', 'process', 'launch', '--device', args.device]
log = args.output/'device.log'
background_valid = True
if args.mode.startswith('audio-external'):
    # This interactive fixture must remain visible after the recording ends.
    # A console-attached devicectl forwards termination signals to the app, so
    # launch without --console and poll this invocation's persisted report.
    subprocess.run(launch + ['--terminate-existing', args.bundle_id], check=True)
    report_path = args.output/'recorder-audio-results.json'
    deadline = time.monotonic() + 180
    ready = False
    while True:
        result = subprocess.run(['xcrun', 'devicectl', 'device', 'copy', 'from',
            '--device', args.device, '--domain-type', 'appDataContainer',
            '--domain-identifier', args.bundle_id,
            '--source', 'Documents/recorder-audio-results.json',
            '--destination', str(report_path)], capture_output=True, timeout=15)
        try:
            report = json.loads(report_path.read_text()) if result.returncode == 0 else {}
        except json.JSONDecodeError:
            report = {}  # A copy can overlap the probe's report write; retry.
        if report.get('runId') == run_id:
            if report.get('mode') != args.mode:
                raise RuntimeError('Installed probe does not report the requested mode; rebuild/reinstall the test player')
            if args.mode == 'audio-external-reference' and report.get('takes') and \
                    report['takes'][0].get('videoBackend') != 'AVCaptureMovieFileOutput':
                raise RuntimeError('Reference mode did not use the native capture backend')
            if report.get('interrupted'):
                print('EXTERNAL-INTERRUPTED: collecting partial evidence; app was not restarted.', flush=True)
                break
            if report.get('takes') and not report.get('completed') and not ready:
                print('EXTERNAL-READY: phone recording active; start the browser fixture now.', flush=True)
                ready = True
            if report.get('completed'):
                print('EXTERNAL-COMPLETE: recording finished; app remains open.', flush=True)
                break
        if time.monotonic() >= deadline:
            raise TimeoutError('External fixture did not complete; app was left open for inspection')
        time.sleep(1)
else:
    with log.open('w') as output:
        process = subprocess.Popen(launch + ['--console', '--terminate-existing', args.bundle_id],
                                   stdout=output, stderr=subprocess.STDOUT)
        try:
            if args.mode == 'background':
                deadline = time.monotonic() + 60
                while '[STOP-TEST] BACKGROUND-READY' not in log.read_text(errors='replace'):
                    if process.poll() is not None:
                        raise RuntimeError('Player exited before background test; see device.log')
                    if time.monotonic() >= deadline:
                        raise TimeoutError('Player did not reach background test; see device.log')
                    time.sleep(.5)
                subprocess.run(launch + ['com.apple.Preferences'], check=True)
                time.sleep(3)
                # Prove finalization while Unity is suspended, not only after resume.
                path = re.findall(r'\[Recorder\] START .* path=([^\r\n]+)', log.read_text(errors='replace'))[-1]
                movie = args.output/'background-before-resume.mp4'
                check = {'valid': False}
                try:
                    subprocess.run(['xcrun', 'devicectl', 'device', 'copy', 'from', '--device', args.device,
                                    '--domain-type', 'appDataContainer', '--domain-identifier', args.bundle_id,
                                    '--source', 'Library/Caches/'+Path(path).name,
                                    '--destination', str(movie)], check=True, capture_output=True)
                    probe = subprocess.run(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)],
                                           capture_output=True)
                    check = {'valid': probe.returncode == 0, 'bytes': movie.stat().st_size,
                             'sha256': hashlib.sha256(movie.read_bytes()).hexdigest(),
                             'error': probe.stderr.decode(errors='replace')}
                except subprocess.CalledProcessError as error:
                    check['error'] = str(error)
                background_valid = check['valid']
                (args.output/'background-before-resume.json').write_text(json.dumps(check, indent=2))
                # Resume the existing process; do not restart its recording state.
                subprocess.run(launch + [args.bundle_id], check=True)
            process.wait(timeout=1500 if args.mode == 'audio-long' else 300)
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait(timeout=10)

# Collect even after an abnormal player exit. The report's completion flag and
# file checks determine success; partial evidence remains available for debugging.
validation = subprocess.call([
    sys.executable, str(Path(__file__).with_name('recorder_external_artifacts.py' if args.mode.startswith('audio-external') else 'recorder_audio_artifacts.py' if args.mode.startswith('audio') else 'recorder_stop_artifacts.py')),
    '--device', args.device, '--bundle-id', args.bundle_id, '--output', str(args.output), '--run-id', run_id]
    + (['--roi', args.external_roi] if args.mode.startswith('audio-external') and args.external_roi else []))
if args.mode == 'background' and background_valid:
    background_valid = hashlib.sha256((args.output/'000-background.mp4').read_bytes()).hexdigest() == check['sha256']
if not background_valid:
    print('Background movie was not finalized before resume; see background-before-resume.json', file=sys.stderr)
raise SystemExit(validation or (0 if background_valid else 1))
