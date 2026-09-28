#!/usr/bin/env bash
# Offline gesture evaluation on the Mac: Apple Vision joints from the probe's hand photos
# (8 rotations each) and the first-person hand video, classified by the former Unity rule and
# the ported Swift SDK rule (Tests/Hands/Tools~/evaluate_gestures.py).
# Needs ffmpeg and the probe stimuli (Tests/Probe/Tools~/fetch_stimuli.py).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
STIMULI="$ROOT/Tests/Probe/Tools~"
OUT="${TMPDIR:-/tmp}/holokit-gesture-eval"
mkdir -p "$OUT/rot" "$OUT/video"
swiftc -O "$ROOT/Tests/Hands/Tools~/vision_hand_joints.swift" -o "$OUT/joints"
python3 - "$STIMULI/stimuli" "$OUT/rot" <<'PY'
import sys
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
for name in ('open', 'pinch', 'hands', 'person'):
    image = Image.open(f'{src}/{name}.jpg').convert('RGB')
    for angle in range(0, 360, 45):
        image.rotate(angle, expand=True, fillcolor=(0, 0, 0)).save(f'{dst}/{name}_{angle:03d}.jpg', quality=92)
PY
ffmpeg -v error -y -i "$STIMULI/videos/hand-test.mp4" -vf "select='not(mod(n\,2))',scale=960:-2" -fps_mode vfr -q:v 3 "$OUT/video/f_%04d.jpg"
"$OUT/joints" "$OUT"/rot/*.jpg > "$OUT/rot.jsonl"
"$OUT/joints" "$OUT"/video/*.jpg > "$OUT/video.jsonl"
python3 "$ROOT/Tests/Hands/Tools~/evaluate_gestures.py" "$OUT/rot.jsonl" "$OUT/video.jsonl"
