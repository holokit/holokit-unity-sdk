#!/usr/bin/env python3
"""Exercise the actual AVAssetWriter plugin; inspect every produced movie with ffprobe."""
import array, ctypes as c, json, math, pathlib, subprocess, sys, time
package = pathlib.Path(__file__).resolve().parents[3]
out = pathlib.Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
native = package/'Runtime/iOS/NativeCode'
library = out/'libRecorderTest.dylib'
# Host-only test artifact; never import a macOS plugin into the Unity project.
subprocess.run(['xcrun','clang++','-dynamiclib',str(native/'VideoRecorder.mm'), str(native/'HoloKitMixedMovie.mm'), str(native/'HoloKitRecordingAudio.mm'), str(native/'HoloKitAudioCapture.mm'), str(package/'Tests/Recorder/Native~/mixed_movie_bridge.mm'),
                '-o',str(library),'-fobjc-arc','-std=c++17','-mmacosx-version-min=12.0',
                '-I',str(native),*[arg for name in ['Foundation','AVFoundation','CoreMedia',
                'CoreVideo','Accelerate','AudioToolbox','Metal'] for arg in ['-framework',name]]], check=True)
lib = c.CDLL(str(library.resolve()))
_start = lib.HoloKitVideoRecorder_StartRecording
_start.argtypes = [c.c_char_p,c.c_int,c.c_int,c.c_int,c.c_float,c.c_int,c.c_int,c.c_int,c.c_double,c.c_float,c.c_float,c.c_int]
def start(*args, game_gain=1, microphone_gain=1, mixed_copy=0):
    return _start(*args, game_gain, microphone_gain, mixed_copy)
video = lib.HoloKitVideoRecorder_AppendVideoFrame; video.argtypes = [c.c_void_p,c.c_int,c.c_double]
audio = lib.HoloKit_RecordingAudioPush
audio.argtypes = [c.c_int,c.c_void_p,c.c_int,c.c_int,c.c_int,c.c_double]
clock = lib.HoloKit_AudioClockTime; clock.restype = c.c_double
stop_audio = lib.HoloKitVideoRecorder_StopAudio; stop_audio.argtypes = [c.c_double]
stop = lib.HoloKitVideoRecorder_EndRecording
finishing = lib.HoloKitVideoRecorder_IsFinishing
# Failure must not poison the next start.
assert start(b'/missing-directory/invalid.mp4',320,240,30,48000,2,1,0,clock()) != 0
results = []
for index,(width,height,channels,rate) in enumerate([(240,320,2,48000),(320,240,2,48000),(240,320,1,48000),(320,240,1,48000),(240,320,2,24000),(320,240,2,24000)]):
    path = out/f'native-{index}.mp4'
    origin = clock()
    assert start(str(path).encode(),width,height,30,rate,channels,1,0,origin) == 0
    assert start(str(out/'double-start.mp4').encode(),width,height,30,rate,channels,1,0,origin) != 0
    pixels = (c.c_ubyte*(width*height*4))(*([255,40,10,255]*(width*height)))
    began = time.monotonic()
    for frame in range(45):
        pcm = array.array('f', (0.2*math.sin(2*math.pi*300*(frame*(rate//30)+i//channels)/rate) for i in range((rate//30)*channels)))
        samples = (c.c_float*len(pcm)).from_buffer(pcm)
        assert video(pixels,len(pixels),frame/30) == 0
        assert audio(1,samples,len(pcm)//channels,channels,rate,origin+frame/30) == 0
        time.sleep(max(0,began+(frame+1)/30-time.monotonic()))
    stop_started=time.perf_counter()
    stop_audio(origin + 1.5)
    stop(); stop()
    stop_ms=(time.perf_counter()-stop_started)*1000
    if finishing():
        assert start(str(out/'premature-restart.mp4').encode(),width,height,30,rate,channels,1,0,origin) != 0
    deadline=time.monotonic()+15
    while finishing() and time.monotonic()<deadline: time.sleep(.05)
    assert not finishing(), 'finalization timeout'
    finish_ms=(time.perf_counter()-stop_started)*1000
    probe = json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-show_streams','-show_format','-of','json',str(path)]))
    v=next(s for s in probe['streams'] if s['codec_type']=='video')
    a=next(s for s in probe['streams'] if s['codec_type']=='audio')
    assert (v['width'],v['height'])==(width,height)
    assert int(v['nb_read_frames'])==45
    assert a['channels']==channels
    assert int(a['sample_rate'])==rate
    decoded = array.array('f')
    decoded.frombytes(subprocess.check_output(['ffmpeg','-v','error','-i',str(path),'-f','f32le','-acodec','pcm_f32le','-']))
    rms = math.sqrt(sum(x*x for x in decoded)/len(decoded))
    assert .08 < rms < .22, f'unexpected audio RMS: {rms}'
    rgb = subprocess.check_output(['ffmpeg','-v','error','-i',str(path),'-frames:v','1','-vf','scale=1:1','-f','rawvideo','-pix_fmt','rgb24','-'])
    assert rgb[0]>220 and rgb[1]<75 and rgb[2]<45, f'colour swapped or corrupted: {rgb}'
    assert abs(float(v['duration'])-float(a['duration']))<.2
    results.append({'path':str(path),'frames':45,'dimensions':[width,height],'channels':channels,'sample_rate':rate,'audio_rms':rms,'rgb':list(rgb),'video_duration':v['duration'],'audio_duration':a['duration'],'stop_call_ms':stop_ms,'finish_ms':finish_ms})
(out/'results.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))

# Independent tracks use the common origin even when their first callbacks differ.
# Delayed microphone pulses intentionally differ from game pulses by 70 ms.
# This tests the writer timeline, not physical camera/microphone latency or AEC.
multitrack = []
for mask in (0, 1, 2, 3):
    path = out/f'sources-{mask}.mp4'
    origin = clock()
    assert start(str(path).encode(),320,240,30,48000,2,mask,44100,origin) == 0
    began = time.monotonic()
    submitted = {1: 0, 2: 0}
    pulses = {1: (.30, .80, 1.30), 2: (.37, .87, 1.37)}
    for frame in range(45):
        pixels = (c.c_ubyte*(320*240*4))(*([255 if frame in (9,24,39) else 0,0,0,255]*(320*240)))
        assert video(pixels,len(pixels),frame/30) == 0
        for source, rate, channels, first in ((1,48000,2,.06),(2,44100,1,.16)):
            if not mask & source: continue
            # Keep first callbacks staggered and exercise packets crossing stop.
            while submitted[source]/rate + first < (frame+1)/30:
                at = submitted[source]/rate + first
                count = 512
                pcm = array.array('f')
                for i in range(count):
                    t = at + i/rate
                    on = any(p <= t < p+.055 for p in pulses[source])
                    for channel in range(channels):
                        frequency = (440 if channel == 0 else 660) if source == 1 else 997
                        pcm.append(.35*math.sin(2*math.pi*frequency*t) if on else 0)
                samples = (c.c_float*len(pcm)).from_buffer(pcm)
                assert audio(source,samples,count,channels,rate,origin+at) == 0
                submitted[source] += count
        time.sleep(max(0,began+(frame+1)/30-time.monotonic()))
    stop_audio(origin+1.5)
    stop()
    deadline=time.monotonic()+15
    while finishing() and time.monotonic()<deadline: time.sleep(.01)
    assert not finishing(), 'multitrack finalization timeout'
    assert lib.HoloKitVideoRecorder_AudioFailed() == 0, 'audio queue or encoder failed'
    probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-of','json',str(path)]))
    streams=[s for s in probe['streams'] if s['codec_type']=='audio']
    selected=[source for source in (1,2) if mask & source]
    assert len(streams)==len(selected)
    measured=[]
    for stream,source in zip(streams,selected):
        rate,channels=(48000,2) if source==1 else (44100,1)
        assert int(stream['sample_rate'])==rate and stream['channels']==channels
        decoded=array.array('f')
        # Preserve track edit offsets; decode onto t=0 with leading silence.
        decoded.frombytes(subprocess.check_output(['ffmpeg','-v','error','-copyts','-i',str(path),
            '-map',f"0:{stream['index']}",'-af','aresample=async=1:first_pts=0',
            '-f','f32le','-acodec','pcm_f32le','-']))
        left=decoded[::channels]
        detected=[]
        step=int(rate*.005)
        active_pulse=False
        for i in range(0,len(left)-step,step):
            loud=math.sqrt(sum(x*x for x in left[i:i+step])/step)>.06
            if loud and not active_pulse: detected.append(i/rate)
            active_pulse=loud
        assert len(detected)==3, detected
        errors=[a-b for a,b in zip(detected,pulses[source])]
        assert max(abs(x) for x in errors)<.012, (source,detected,errors)
        assert abs(errors[-1]-errors[0])<.006, ('relative pulse spacing changed',errors)
        assert float(stream['start_time'])>.015, 'first callback was incorrectly rebased to zero'
        measured.append({'source':source,'stream_start':stream['start_time'],'onsets':detected,'errors':errors})
    subprocess.run(['ffmpeg','-v','error','-xerror','-i',str(path),'-map','0','-f','null','-'],check=True)
    multitrack.append({'source_mask':mask,'tracks':measured})
(out/'multitrack-results.json').write_text(json.dumps(multitrack,indent=2))
print(json.dumps(multitrack,indent=2))

# Initial mute must apply before the first native microphone packet can arrive.
# Later gain changes apply at capture time, not 150 ms later at queue drain time.
path=out/'capture-gains.mp4'; origin=clock()
assert start(str(path).encode(),320,240,30,48000,2,3,48000,origin,game_gain=0,microphone_gain=.4,mixed_copy=1)==0
set_gain=lib.HoloKitVideoRecorder_SetAudioGain; set_gain.argtypes=[c.c_float,c.c_float]
began=time.monotonic()
for frame in range(45):
    if frame==15: set_gain(1,0)
    assert video(pixels,len(pixels),frame/30)==0
    for source,channels,frequency in ((1,2,440),(2,1,997)):
        pcm=array.array('f',(.2*math.sin(2*math.pi*frequency*(frame*1600+i//channels)/48000)
            for i in range(1600*channels)))
        samples=(c.c_float*len(pcm)).from_buffer(pcm)
        assert audio(source,samples,1600,channels,48000,origin+frame/30)==0
    time.sleep(max(0,began+(frame+1)/30-time.monotonic()))
stop_audio(origin+1.5);stop();deadline=time.monotonic()+15
while finishing() and time.monotonic()<deadline:time.sleep(.01)
assert not finishing() and lib.HoloKitVideoRecorder_AudioFailed()==0
gains=[]
for track,channels in ((0,2),(1,1)):
    samples=array.array('f')
    samples.frombytes(subprocess.check_output(['ffmpeg','-v','error','-copyts','-i',str(path),
        '-map',f'0:a:{track}','-af','aresample=async=1:first_pts=0','-f','f32le','-']))
    def rms(a,b):
        window=samples[int(a*48000)*channels:int(b*48000)*channels]
        return math.sqrt(sum(x*x for x in window)/len(window))
    before,after=rms(.10,.40),rms(.65,1.25)
    if track==0: assert before<1e-4 and .12<after<.16,(before,after)
    else: assert .045<before<.065 and after<1e-4,(before,after)
    gains.append({'track':track,'before_gain_change_rms':before,'after_gain_change_rms':after})
(out/'gain-results.json').write_text(json.dumps(gains,indent=2))
print(json.dumps(gains,indent=2))

# Optional sharing export: one AAC track, exact compressed video preservation.
sharing=path.with_suffix('.share.mp4')
info=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-of','json',str(sharing)]))
assert len([s for s in info['streams'] if s['codec_type']=='audio'])==1
video_hashes=[subprocess.check_output(['ffmpeg','-v','error','-i',str(f),'-map','0:v','-c','copy',
    '-f','hash','-hash','sha256','-']).decode().strip() for f in (path,sharing)]
assert video_hashes[0]==video_hashes[1], 'Sharing export changed the compressed video'
subprocess.run(['ffmpeg','-v','error','-xerror','-i',str(sharing),'-map','0','-f','null','-'],check=True)
samples=array.array('f');samples.frombytes(subprocess.check_output(['ffmpeg','-v','error','-copyts','-i',str(sharing),
    '-map','0:a:0','-af','aresample=async=1:first_pts=0','-f','f32le','-']))
def mixed_rms(a,b):
    window=samples[int(a*48000)*2:int(b*48000)*2]
    return math.sqrt(sum(x*x for x in window)/len(window))
early,late=mixed_rms(.10,.40),mixed_rms(.65,1.25)
assert .015<early<.035 and .060<late<.080,(early,late)
(out/'sharing-results.json').write_text(json.dumps({'path':str(sharing),'video_hash':video_hashes[0],
    'audio_tracks':1,'early_microphone_only_rms':early,'later_game_only_rms':late},indent=2))

# Async sharing failures and cancellation must preserve the source and existing
# destinations, complete once, and permit a subsequent successful export.
import hashlib
mix = lib.HoloKit_TestMix
mix.argtypes = [c.c_char_p,c.c_char_p,c.c_int]
original = out/'sources-3.mp4'
original_hash = hashlib.sha256(original.read_bytes()).hexdigest()
sharing_failures = []
for name, source, destination, cancel, expected in (
    ('missing-source', out/'missing.mp4', out/'missing.share.mp4', 0, -1),
    ('existing-destination', original, out/'preserve-existing.mp4', 0, -1),
    ('cancel', original, out/'cancel.share.mp4', 1, -1),
    ('recover-after-failure', original, out/'recovered.share.mp4', 0, 2),
):
    if name == 'existing-destination': destination.write_bytes(b'preserve this existing file')
    assert mix(str(source).encode(),str(destination).encode(),cancel) == 0
    deadline = time.monotonic()+15
    while lib.HoloKit_TestMixState() == 1 and time.monotonic()<deadline: time.sleep(.01)
    assert lib.HoloKit_TestMixState() == expected, name
    assert lib.HoloKit_TestMixCompletions() == 1, name
    assert hashlib.sha256(original.read_bytes()).hexdigest() == original_hash, name
    if name == 'existing-destination': assert destination.read_bytes() == b'preserve this existing file'
    elif expected == -1: assert not destination.exists(), name
    else:
        subprocess.run(['ffmpeg','-v','error','-xerror','-i',str(destination),'-f','null','-'],check=True)
    sharing_failures.append({'case':name,'state':expected,'completions':1,'source_unchanged':True})
(out/'sharing-failure-results.json').write_text(json.dumps(sharing_failures,indent=2))
print(json.dumps(sharing_failures,indent=2))
