#!/usr/bin/env python3
"""Download the Wikimedia Commons person photo used by sdk_stimuli.html (human stencil).

Hand stimuli are the committed first-person images in first-person/.

The images are not committed; each keeps its Commons licence (see stimuli/CREDITS.txt).
Requires Pillow. Usage: python3 fetch_stimuli.py
"""
import json
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


if __name__ == '__main__':
    main()
