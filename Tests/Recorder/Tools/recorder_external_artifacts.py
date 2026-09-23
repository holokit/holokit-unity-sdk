#!/usr/bin/env python3
"""Measure camera-visible screen flashes against 1200 Hz external mic pulses.

Use external_stimulus.html with a visible browser and local computer speakers.
This is a software-clocked physical fixture: display presentation is not
photodiode-calibrated. Never rebase one track to match the other.
"""
import argparse
import json
from pathlib import Path
import subprocess
import numpy as np
from recorder_signal import tone_onsets

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--device')
parser.add_argument('--bundle-id')
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--run-id')
parser.add_argument('--input', type=Path, help='Reanalyze an already collected movie')
parser.add_argument('--roi', default='.2,.2,.6,.6', help='Normalized x,y,width,height; select the computer screen')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)

def run(*command):
    return subprocess.check_output(command, stderr=subprocess.PIPE)

def copy(source, destination):
    if not args.device or not args.bundle_id:
        parser.error('--device and --bundle-id are required unless --input is supplied')
    run('xcrun','devicectl','device','copy','from','--device',args.device,
        '--domain-type','appDataContainer','--domain-identifier',args.bundle_id,
        '--source',source,'--destination',str(destination))

def rising(mask, times):
    # A pulse already present in the first sample has no observable onset.
    return times[np.flatnonzero(mask & ~np.r_[mask[0],mask[:-1]])]

errors=[]
backend=None
device_run_id=None
if args.input:
    path=args.input
else:
    report_path=args.output/'recorder-audio-results.json'
    copy('Documents/recorder-audio-results.json',report_path)
    report=json.loads(report_path.read_text())
    if args.run_id and report.get('runId')!=args.run_id: errors.append('report invocation mismatch')
    if not report.get('completed') or report.get('failures') or report.get('interrupted'):
        errors.append('device probe failed, interrupted, or incomplete')
    if len(report.get('takes',[]))!=1 or report['takes'][0]['name']!='environment-external':
        raise SystemExit('Expected the external camera/microphone take')
    backend=report['takes'][0].get('videoBackend')
    device_run_id=report.get('runId')
    path=args.output/'environment-external.mp4'
    copy('Library/Caches/'+Path(report['takes'][0]['path']).name,path)
probe=json.loads(run('ffprobe','-v','error','-show_streams','-of','json',str(path)))
video=next(s for s in probe['streams'] if s['codec_type']=='video')
audio=[s for s in probe['streams'] if s['codec_type']=='audio']
if len(audio)!=1: errors.append('expected exactly one environment microphone track')
pts=json.loads(run('ffprobe','-v','error','-select_streams','v:0','-show_packets',
    '-show_entries','packet=pts_time','-of','json',str(path)))
times=np.sort([float(p['pts_time']) for p in pts['packets']])
x,y,w,h=map(float,args.roi.split(','))
if min(x,y)<0 or min(w,h)<=0 or x+w>1 or y+h>1: parser.error('ROI outside image')
frames=np.frombuffer(run('ffmpeg','-v','error','-xerror','-i',str(path),'-map','0:v:0','-an',
    '-vf',f'crop=iw*{w}:ih*{h}:iw*{x}:ih*{y},scale=1:1','-fps_mode','passthrough',
    '-pix_fmt','gray','-f','rawvideo','-'),dtype=np.uint8)
if len(frames)!=len(times): raise SystemExit('Decoded frame/timestamp count mismatch')
low,high=np.percentile(frames,[20,99])
if high-low<30: errors.append('insufficient screen contrast; inspect framing')
flashes=rising(frames>(low+high)/2,times)
# Original media timestamps are kept, including encoder priming and leading gaps.
rate=16000
pcm=np.frombuffer(run('ffmpeg','-v','error','-xerror','-copyts','-i',str(path),'-map','0:a:0',
    '-af','aresample=async=1:first_pts=0','-ar',str(rate),'-ac','1','-f','f32le','-'),dtype=np.float32)
pulses,audio_times,energy=tone_onsets(pcm,rate)
# Boundary ringing may split a pulse. Coalesce within one 300 ms fixture pulse.
pulses=np.array([t for i,t in enumerate(pulses) if i==0 or t-pulses[i-1]>.35])
offsets=[]
matched_flashes=[]
for flash in flashes:
    if len(pulses):
        near=pulses[np.argmin(abs(pulses-flash))]
        if abs(near-flash)<.4:
            offsets.append(float(near-flash)); matched_flashes.append(float(flash))
if len(flashes)<10 or len(offsets)<max(10,len(flashes)-1): errors.append('insufficient matching physical flash/pulse events')
if len(flashes)>1 and np.max(abs(np.diff(flashes)-1.3))>.06:
    errors.append('screen flash cadence differs from fixture; inspect ROI and dropped frames')
median=float(np.median(offsets)) if offsets else None
drift=float(np.median(offsets[-3:])-np.median(offsets[:3])) if len(offsets)>=6 else None
if offsets and max(abs(v) for v in offsets)>.05: errors.append('physical camera/microphone offset exceeds 50 ms')
if drift is not None and abs(drift)>.02: errors.append('short-run physical offset drift exceeds 20 ms')
summary={'errors':errors,'file':str(path),'roi':[x,y,w,h],'contrast':float(high-low),
    'video_backend':backend,'device_run_id':device_run_id,
    'audio_detection':'20 ms coherent tone windows; 5 ms hop; 4x neighboring-bin noise; 40 ms minimum support',
    'video_start':video.get('start_time'),'audio_start':audio[0].get('start_time'),
    'flash_times':flashes.tolist(),'pulse_times':pulses.tolist(),'matched_flash_times':matched_flashes,
    'audio_minus_camera_seconds':offsets,'median_offset_seconds':median,'drift_seconds':drift,
    'fixture_gate_passed':not errors,'calibrated_physical_sync_accepted':False,
    'limitation':'Computer audio output clock is browser-estimated; display presentation and acoustic propagation are not instrument-calibrated.'}
(args.output/'external-measurements.json').write_text(json.dumps(summary,indent=2))
np.savez_compressed(args.output/'external-signals.npz',video_pts=times,brightness=frames,
    audio_times=audio_times,audio_envelope=energy)
print(json.dumps({k:summary[k] for k in ('errors','median_offset_seconds','drift_seconds','contrast')},indent=2))
if errors: raise SystemExit('Physical synchronization fixture failed; inspect framing and signals')
