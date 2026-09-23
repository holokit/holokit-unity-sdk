#!/usr/bin/env python3
"""Measure the SDK's physical flash/pulse fixture; keep failed evidence inspectable.

This checks source topology, pulse timing and speaker leakage attenuation.
AEC acceptance also needs an independent near-end voice preserved during playback.
"""
import argparse
import json
import math
from pathlib import Path
import subprocess
import numpy as np

parser = argparse.ArgumentParser()
parser.add_argument('--device', required=True)
parser.add_argument('--bundle-id', required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--run-id')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)

def copy(source, target):
    subprocess.run(['xcrun','devicectl','device','copy','from','--device',args.device,
        '--domain-type','appDataContainer','--domain-identifier',args.bundle_id,
        '--source',source,'--destination',str(target)],check=True,stdout=subprocess.DEVNULL)

def run(*cmd):
    return subprocess.check_output(cmd,stderr=subprocess.PIPE)

def rising(mask, times):
    return times[np.flatnonzero(mask & ~np.r_[False, mask[:-1]])]

report_path = args.output/'recorder-audio-results.json'
copy('Documents/recorder-audio-results.json', report_path)
report = json.loads(report_path.read_text())
errors = []
if args.run_id and report.get('runId') != args.run_id:
    errors.append('Report does not belong to this invocation')
if not report.get('completed') or report.get('failures'):
    errors.append('device fixture did not complete without failures')
if len(report.get('takes',[])) != 4:
    errors.append('expected environment, game-only, bypass and AEC takes')
measurements = []
# Collect all originals before expensive decoding, so the device can be reused
# once collection-complete.json exists. Every run still copies fresh files.
collected = set()
for take in report.get('takes', []):
    try:
        copy('Library/Caches/'+Path(take['path']).name, args.output/(take['name']+'.mp4'))
        collected.add(take['name'])
    except subprocess.CalledProcessError as error:
        errors.append(take['name']+': collection failed: '+str(error))
(args.output/'collection-complete.json').write_text(json.dumps({'runId':report.get('runId'),'originals':sorted(collected)}))
for take in report.get('takes',[]):
    path = args.output/(take['name']+'.mp4')
    if take['name'] not in collected: continue
    try:
        probe=json.loads(run('ffprobe','-v','error','-count_packets','-show_streams','-of','json',str(path)))
        video=next(s for s in probe['streams'] if s['codec_type']=='video')
        streams=[s for s in probe['streams'] if s['codec_type']=='audio']
        expected=2 if take['name'] in ('game-aec-microphone','game-bypassed-microphone') else 1
        if len(streams)!=expected: errors.append(take['name']+': wrong audio track count')
        if int(video['nb_read_packets'])!=take['frames']: errors.append(take['name']+': video frame count mismatch')
        pts=json.loads(run('ffprobe','-v','error','-select_streams','v:0','-show_packets',
            '-show_entries','packet=pts_time','-of','json',str(path)))
        times=np.sort(np.array([float(packet['pts_time']) for packet in pts['packets']]))
        brightness=np.frombuffer(run('ffmpeg','-v','error','-xerror','-err_detect','explode','-i',str(path),'-map','0:v:0','-an','-vf',
            'crop=64:64:(iw-64)/2:(ih-64)/2,scale=1:1','-fps_mode','passthrough',
            '-f','rawvideo','-pix_fmt','gray','pipe:1','-map','0:a','-vn','-f','null','/dev/null'),dtype=np.uint8)
        if len(brightness)!=len(times): raise ValueError('decoded flash/frame timestamp count mismatch')
        flashes=rising(brightness>160,times)
        if len(flashes)<5: errors.append(take['name']+': insufficient visible flashes')
        item={'name':take['name'],'path':str(path),'flash_times':flashes.tolist(),'audio':[]}
        for index,stream in enumerate(streams):
            rate=int(stream['sample_rate'])
            channels=int(stream['channels'])
            data=np.frombuffer(run('ffmpeg','-v','error','-copyts','-i',str(path),
                '-map',f"0:{stream['index']}",'-af','aresample=async=1:first_pts=0',
                '-f','f32le','-acodec','pcm_f32le','-'),dtype=np.float32).reshape(-1,channels)
            window=max(1,round(rate*.005))
            mono=data[:,0]
            chunks=mono[:len(mono)//window*window].reshape(-1,window)
            rms=np.sqrt(np.mean(chunks.astype(np.float64)**2,axis=1))
            threshold=max(.002,float(np.percentile(rms,99))*.3)
            pulses=rising(rms>threshold,np.arange(len(rms))*window/rate)
            offsets=[]
            # Match only nearby events, never shift an entire track to make it fit.
            for flash in flashes:
                if len(pulses):
                    nearest=pulses[np.argmin(abs(pulses-flash))]
                    if abs(nearest-flash)<.25: offsets.append(float(nearest-flash))
            source='microphone' if take['name']=='environment-speaker' or index==1 else 'game'
            result={'source':source,'rate':rate,'channels':channels,'start':float(stream['start_time']),
                'rms_peak':float(rms.max(initial=0)),'pulse_times':pulses.tolist(),'offsets':offsets}
            if source=='game':
                if len(offsets)<max(5,len(flashes)-1): errors.append(take['name']+': game pulses missing')
                elif max(abs(x) for x in offsets)>.050: errors.append(take['name']+': game/flash offset exceeds 50 ms')
                if len(offsets)>=5:
                    result['median_offset']=float(np.median(offsets))
                    count=min(5,len(offsets)//2)
                    result['drift']=float(np.median(offsets[-count:])-np.median(offsets[:count]))
                    if abs(result['drift'])>.020: errors.append(take['name']+': flash/pulse drift exceeds 20 ms')
            # Tone component energy near each flash for raw vs AEC comparison.
            amplitudes=[]
            for flash in flashes:
                begin=max(0,int((flash-.04)*rate)); end=min(len(mono),int((flash+.14)*rate))
                segment=mono[begin:end]
                if len(segment):
                    t=np.arange(len(segment))/rate
                    amplitudes.append(float(sum(abs(np.mean(segment*np.exp(-2j*np.pi*f*t)))**2 for f in (440,660))**.5))
            result['speaker_tone_amplitude']=float(np.median(amplitudes)) if amplitudes else 0
            item['audio'].append(result)
        sharing_path=take.get('sharingPath')
        if sharing_path and sharing_path!=take['path']:
            sharing=args.output/(take['name']+'.share.mp4')
            copy('Library/Caches/'+Path(sharing_path).name,sharing)
            shared=json.loads(run('ffprobe','-v','error','-show_streams','-of','json',str(sharing)))
            if len([stream for stream in shared['streams'] if stream['codec_type']=='audio'])!=1:
                errors.append(take['name']+': sharing copy does not have one audio track')
            hashes=[run('ffmpeg','-v','error','-i',str(movie),'-map','0:v','-c','copy',
                '-f','hash','-hash','sha256','-').decode().strip() for movie in (path,sharing)]
            if hashes[0]!=hashes[1]: errors.append(take['name']+': sharing copy changed compressed video')
            run('ffmpeg','-v','error','-xerror','-i',str(sharing),'-map','0:a','-f','null','-')
            item['sharing']={'file':str(sharing),'video_hash':hashes[1]}
        measurements.append(item)
    except (ValueError,KeyError,StopIteration,subprocess.CalledProcessError) as error:
        errors.append(take['name']+': '+str(error))
raw=next((m for m in measurements if m['name']=='game-bypassed-microphone'),None)
aec=next((m for m in measurements if m['name']=='game-aec-microphone'),None)
attenuation=None
speaker_delay=None
speaker_correlation=None
if raw and aec and len(aec['audio'])==2:
    def band_envelopes(path):
        envelopes=[]
        for index in (0,1):
            data=np.frombuffer(run('ffmpeg','-v','error','-copyts','-i',path,
                '-map',f'0:a:{index}','-af','aresample=async=1:first_pts=0',
                '-ar','8000','-ac','1','-f','f32le','-'),dtype=np.float32)
            frequency=np.fft.rfftfreq(len(data),1/8000)
            spectrum=np.fft.rfft(data)
            spectrum[(frequency<400)|(frequency>710)]=0
            filtered=np.fft.irfft(spectrum,n=len(data))
            length=len(filtered)//40*40
            envelopes.append(np.sqrt(np.mean(filtered[:length].reshape(-1,40)**2,axis=1)))
        return envelopes
    game,bypassed=band_envelopes(raw['path'])
    scores=[]
    for lag in range(100):
        count=min(len(game),len(bypassed)-lag)
        # Ignore startup and final drain, but retain original track timestamps.
        x,y=game[200:count-100],bypassed[200+lag:count-100+lag]
        correlation=float(np.corrcoef(x,y)[0,1]) if len(x) and x.std()>0 and y.std()>0 else 0
        scores.append((correlation,lag))
    speaker_correlation,lag=max(scores)
    speaker_delay=lag*.005
    if speaker_correlation<.8:
        errors.append('bypassed microphone does not contain a clear correlated speaker stimulus')
    else:
        def pulse_energy(envelopes):
            game,microphone=envelopes
            count=min(len(game),len(microphone)-lag)
            indexes=np.flatnonzero(game[:count]>.35*game.max())
            indexes=indexes[(indexes>200)&(indexes<count-100)]
            return float(np.mean(microphone[indexes+lag]**2))
        before=pulse_energy((game,bypassed)); after=pulse_energy(band_envelopes(aec['path']))
        attenuation=10*math.log10(max(before,1e-18)/max(after,1e-18))
        if attenuation<10: errors.append('speaker leakage attenuation below 10 dB')
# Searching an acoustic delay is ONLY for matching the AEC stimulus, never for
# shifting file timestamps or passing synchronization. The measured delay must
# be explained and the capture/playback latency independently verified.
summary={'errors':errors,'takes':measurements,'speaker_attenuation_db':attenuation,
    'bypassed_speaker_delay':speaker_delay,'bypassed_speaker_correlation':speaker_correlation,
    'physical_sync_accepted':False,'aec_accepted':False,
    'remaining':'Resolve measured speaker-path delay and confirm environment onset and near-end voice preservation; long recording and lifecycle fixtures are separate gates.'}
(args.output/'audio-measurements.json').write_text(json.dumps(summary,indent=2))
print(json.dumps({'errors':errors,'speaker_attenuation_db':attenuation,
    'bypassed_speaker_delay':speaker_delay,'bypassed_speaker_correlation':speaker_correlation,'takes':len(measurements)},indent=2))
if errors: raise SystemExit('Audio fixture failed; inspect audio-measurements.json')
