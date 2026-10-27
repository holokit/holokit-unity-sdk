#!/usr/bin/env python3
"""Download the Wikimedia Commons person photo used by sdk_stimuli.html (human stencil).

Hand stimuli are the committed first-person images in first-person/.

The images are not committed; each keeps its Commons licence (see stimuli/CREDITS.txt).
Requires Pillow. Usage: python3 fetch_stimuli.py
"""
import json
import subprocess
import pathlib
import urllib.parse
import urllib.request

from PIL import Image

OUT = pathlib.Path(__file__).resolve().parent / 'stimuli'
USER_AGENT = {'User-Agent': 'HoloKitProbe/1.0 (https://github.com/holokit/holokit-unity-sdk)'}
# name: (Commons file title, crop box as fractions of the 1920-px-wide thumbnail or None)
IMAGES = {
    'open': ('File:Stop Coronavirus COVID-19.jpg', (0.333, 0.258, 0.693, 0.883)),
    'pinch': ('File:Scuba diver giving the OK sign.jpg', (0.127, 0.068, 0.507, 0.631)),
    'hands': ("File:A man's hands, palms down, opened out and spread onto black Wellcome V0029446.jpg", None),
    'person': ('File:Man raises hand.jpg', None),
}


def api(params):
    url = 'https://commons.wikimedia.org/w/api.php?' + urllib.parse.urlencode(params)
    return json.load(urllib.request.urlopen(urllib.request.Request(url, headers=USER_AGENT)))


# Real hand videos (Pexels License; downloaded, never committed). Each test clip is its sources in order,
# each (Pexels id, (start s, duration s) or None for the whole video); video_ground_truth.swift runs
# Vision on every frame of the clip.
VIDEOS = pathlib.Path(__file__).resolve().parent / 'videos'
TEST_VIDEOS = {
    # Hands making Pinched / Five / other shapes while they move, for the handvideo and gesturevideo
    # suites. Both are first-person-like: the arm enters from the side, the back of the hand faces the
    # camera, and the hand is about as large as one at arm's length (the earlier hand-test clips were
    # wide shots whose hands looked 1-2 m away).
    'gesture-test': [(10204151, (0, 36)), (10210107, None)],
}


def pexels(video):
    path = VIDEOS / f'pexels-{video}.mp4'
    if not path.exists():
        request = urllib.request.Request(f'https://www.pexels.com/download/video/{video}/', headers={'User-Agent': 'Mozilla/5.0'})
        path.write_bytes(urllib.request.urlopen(request).read())
    return path


def build_video(name, sources):
    VIDEOS.mkdir(exist_ok=True)
    clip, truth = VIDEOS / f'{name}.mp4', VIDEOS / f'{name}.truth.json'
    if not sources or (clip.exists() and truth.exists()):
        return
    inputs = []
    for video, span in sources:
        inputs += (['-ss', str(span[0]), '-t', str(span[1])] if span else []) + ['-i', str(pexels(video))]
    scale = 'scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2,fps=30,setsar=1'
    graph = ';'.join(f'[{i}:v]{scale}[v{i}]' for i in range(len(sources)))
    graph += ';' + ''.join(f'[v{i}]' for i in range(len(sources))) + f'concat=n={len(sources)}:v=1:a=0[v]'
    subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', *inputs,
                    '-filter_complex', graph, '-map', '[v]', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '20',
                    '-movflags', '+faststart', str(clip)], check=True)
    with open(truth, 'w') as out:
        subprocess.run(['swift', str(VIDEOS.parent / 'video_ground_truth.swift'), str(clip)], stdout=out, check=True)


def build_videos():
    for name, sources in TEST_VIDEOS.items():
        build_video(name, sources)
    used = sorted({video for sources in TEST_VIDEOS.values() for video, _ in sources})
    (VIDEOS / 'CREDITS.txt').write_text(''.join(f'pexels-{v}.mp4: https://www.pexels.com/video/{v}/ (Pexels License)\n'
                                                for v in used))


def main():
    OUT.mkdir(exist_ok=True)
    credits = []
    for name, (title, box) in IMAGES.items():
        page = next(iter(api({'action': 'query', 'format': 'json', 'titles': title, 'prop': 'imageinfo',
                              'iiprop': 'url|extmetadata', 'iiurlwidth': 1920})['query']['pages'].values()))
        info = page['imageinfo'][0]
        meta = info['extmetadata']
        raw = OUT / f'{name}-source.jpg'
        raw.write_bytes(urllib.request.urlopen(urllib.request.Request(info['thumburl'], headers=USER_AGENT)).read())
        image = Image.open(raw).convert('RGB')
        if box:
            w, h = image.size
            image = image.crop((int(box[0] * w), int(box[1] * h), int(box[2] * w), int(box[3] * h)))
        image.save(OUT / f'{name}.jpg', quality=92)
        raw.unlink()
        credits.append(f"{name}.jpg: {title} | {meta.get('LicenseShortName', {}).get('value')} | {info['descriptionurl']}")
        print(name, image.size)
    (OUT / 'CREDITS.txt').write_text('\n'.join(credits) + '\n')
    build_videos()


if __name__ == '__main__':
    main()
