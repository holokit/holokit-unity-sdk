#!/usr/bin/env python3
"""Copy stop-probe results from iPhone, decode each movie, and summarize latency.
Before launching the recovery test build, copy a file containing stop to
Documents/recorder-test-mode.txt in its app data container.
"""
import argparse
import json
import math
from pathlib import Path
import statistics
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--device', required=True)
parser.add_argument('--bundle-id', required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--run-id')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)

def copy(source, destination):
    subprocess.run(['xcrun', 'devicectl', 'device', 'copy', 'from', '--device', args.device,
                    '--domain-type', 'appDataContainer', '--domain-identifier', args.bundle_id,
                    '--source', source, '--destination', str(destination)], check=True, stdout=subprocess.DEVNULL)

report_path = args.output/'recorder-stop-results.json'
copy('Documents/recorder-stop-results.json', report_path)
report = json.loads(report_path.read_text())
checks = []
errors = []
if args.run_id and report.get('runId') != args.run_id:
    errors.append('Report does not belong to this invocation')
if not report.get('completed', False):
    errors.append('Device run did not finish; partial results cannot pass regression')
for index, result in enumerate(report['results']):
    if result['scenario'] == 'empty':
        continue  # No submitted video; a playable file is not expected.
    path = args.output/f'{index:03d}-{result["scenario"]}.mp4'
    copy('Library/Caches/'+Path(result['path']).name, path)
    try:
        probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-count_frames',
                            '-show_streams', '-of', 'json', str(path)], stderr=subprocess.PIPE))
    except subprocess.CalledProcessError as error:
        errors.append(f'{path.name}: invalid movie: {error.stderr.decode(errors="replace").strip()}')
        continue
    video = next((s for s in probe['streams'] if s['codec_type'] == 'video'), None)
    audio = next((s for s in probe['streams'] if s['codec_type'] == 'audio'), None)
    if video is None or audio is None:
        errors.append(f'{path.name}: missing video or audio track')
        continue
    if int(video['nb_read_frames']) != result['frames']:
        errors.append(f'{path.name}: frame count mismatch')
    v_end = float(video.get('start_time', 0)) + float(video['duration'])
    a_end = float(audio.get('start_time', 0)) + float(audio['duration'])
    if abs(v_end-a_end) >= .2:
        errors.append(f'{path.name}: A/V end delta {abs(v_end-a_end):.6f}s')
    decode = subprocess.run(['ffmpeg', '-v', 'error', '-xerror', '-err_detect', 'explode', '-i', str(path),
                    '-enc_time_base:v', '1:600', '-fps_mode', 'passthrough', '-f', 'null', '-'], capture_output=True)
    if decode.returncode:
        errors.append(f'{path.name}: decode failed: {decode.stderr.decode(errors="replace").strip()}')
    checks.append({'file': str(path), 'frames': result['frames'], 'av_end_delta': abs(v_end-a_end),
                   'dimensions': [video['width'], video['height']], 'decoded': decode.returncode == 0})
summary = {'errors': errors, 'device_failures': report['failures'],
           'completed': report.get('completed', False), 'mode': report.get('mode'),
           'device_model': report.get('deviceModel'), 'operating_system': report.get('operatingSystem'),
           'movies_checked': len(checks), 'latency': [], 'movies': checks}
for four_k in (False, True):
    for sync in (True, False):
        samples = [r for r in report['results'] if r['scenario'] == 'benchmark' and r['fourK'] == four_k and r['sync'] == sync]
        row = {'fourK': four_k, 'sync': sync, 'count': len(samples)}
        for metric in ('stopMs', 'finishMs', 'maxFrameMs'):
            values = sorted(r[metric] for r in samples)
            if values:
                row[metric] = {'median': statistics.median(values), 'p95': values[math.ceil(len(values)*.95)-1], 'max': max(values)}
        summary['latency'].append(row)
(args.output/'validated-summary.json').write_text(json.dumps(summary, indent=2))
print(json.dumps({k: v for k, v in summary.items() if k != 'movies'}, indent=2))
if report['failures'] or errors:
    raise SystemExit('Validation failed; see validated-summary.json and console log')
