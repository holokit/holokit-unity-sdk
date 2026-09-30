"""Gesture classifiers on the same Apple Vision joints: old (Unity SDK before the port), swift
(the Swift SDK's rule, pinch at 0.12 image units) and new (HandGestureClassifier: the Swift rule
with pinch relative to hand size). Each is a line-by-line copy of its C# or Swift code."""
import json, math, sys, collections

W, CMC, MP, IP, TT, IM, IPP, ID, IT, MM, MPP, MD, MTT, RM, RP, RD, RT, LM, LP, LD, LT = range(21)
FINGERS = [[CMC, MP, IP, TT], [IM, IPP, ID, IT], [MM, MPP, MD, MTT], [RM, RP, RD, RT], [LM, LP, LD, LT]]
dist = lambda a, b: math.hypot(a[0] - b[0], a[1] - b[1])

def old(h):
    # HandGestureRecognitionManager before 1d922a1: no confidence; Five = y rises tip over DIP over PIP over MCP.
    if dist(h[TT], h[IT]) < 0.12:
        return 'Pinched'
    up = lambda f: h[f[3]][1] > h[f[2]][1] > h[f[1]][1] > h[f[0]][1]
    return 'Five' if all(up(f) for f in FINGERS) else 'None'

def five(h, ok):
    for f in FINGERS:
        if not all(ok(j) for j in f):
            return 'None'
        length = sum(dist(h[f[i]], h[f[i + 1]]) for i in range(3))
        if length <= 0.025 or dist(h[f[0]], h[f[3]]) / length < 0.82:
            return 'None'
    return 'Five'

def swift(h):
    # Swift SDK GestureRecognizer.
    ok = lambda j: h[j][2] >= 0.35
    if ok(TT) and ok(IT) and dist(h[TT], h[IT]) < 0.12:
        return 'Pinched'
    return five(h, ok)

def hand_size(h, ok):
    # HandGestureClassifier.HandSize: longest of palm, index and middle bone lengths.
    sizes = [dist(h[W], h[MM])] if ok(W) and ok(MM) else []
    for f in FINGERS[1:3]:
        if all(ok(j) for j in f):
            sizes.append(sum(dist(h[f[i]], h[f[i + 1]]) for i in range(3)))
    return max(sizes, default=0)

def new(h):
    # HandGestureClassifier: pinch below 0.25 of the hand's size (0.12 image units if unknown).
    ok = lambda j: h[j][2] >= 0.35
    if ok(TT) and ok(IT):
        size = hand_size(h, ok)
        if (dist(h[TT], h[IT]) / size < 0.25) if size > 0 else dist(h[TT], h[IT]) < 0.12:
            return 'Pinched'
    return five(h, ok)

LABEL = {'open': 'Five', 'pinch': 'Pinched', 'hands': 'Five', 'person': 'None'}

def photos(path):
    rows = []
    for line in open(path):
        d = json.loads(line)
        name = d['file'].split('/')[-1][:-4]
        kind, angle = name.split('_')
        for h in d['hands']:
            rows.append((kind, int(angle), LABEL[kind], old(h), swift(h), new(h)))
    print('== Photos: every detected hand, 8 rotations of 4 photos (Vision missed some rotations for both)')
    print(f"{'photo':8}{'angle':>6}  {'truth':8}{'old':9}{'swift':9}{'new':9}")
    for r in rows:
        print(f'{r[0]:8}{r[1]:>6}  {r[2]:8}{r[3]:9}{r[4]:9}{r[5]:9}')
    for who, i in (('old', 3), ('swift', 4), ('new', 5)):
        right = sum(r[i] == r[2] for r in rows)
        print(f'{who}: {right}/{len(rows)} correct')

class Filter:  # HandGestureFilter: four matching observations change the gesture
    def __init__(self): self.gesture, self.candidate, self.evidence = 'None', 'None', 0
    def observe(self, g):
        if g == self.candidate: self.evidence += 1
        else: self.candidate, self.evidence = g, 1
        if self.evidence >= 4 and self.gesture != g:
            self.gesture = g
            return True
        return False

def video(path, fps):
    frames = [json.loads(l) for l in open(path)]
    rules = (('old', old), ('swift', swift), ('new', new))
    counts = {who: collections.Counter() for who, _ in rules}
    changes = {who: 0 for who, _ in rules}
    filters = {who: Filter() for who, _ in rules}
    disagree = []
    hands = 0
    for i, d in enumerate(frames):
        if not d['hands']:
            continue
        hands += 1
        h = d['hands'][0]
        for who, fn in rules:
            g = fn(h)
            counts[who][g] += 1
            changes[who] += filters[who].observe(g)
        if old(h) != new(h):
            disagree.append((i, old(h), new(h), d['file']))
    minutes = len(frames) / fps / 60
    print(f'\n== Video: {len(frames)} frames at {fps} fps ({minutes:.1f} min), first hand in {hands} frames')
    for who, _ in rules:
        c = counts[who]
        print(f"{who}: Five {c['Five']}  Pinched {c['Pinched']}  None {c['None']}  "
              f"gesture changes after the 4-frame hold: {changes[who]} ({changes[who] / minutes:.0f}/min)")
    kinds = collections.Counter((o, n) for _, o, n, _ in disagree)
    print(f'disagreements: {len(disagree)} frames: ' + ', '.join(f'{o}->{n} x{c}' for (o, n), c in kinds.most_common()))
    json.dump(disagree, open(path.replace('.jsonl', '-disagree.json'), 'w'))

photos(sys.argv[1])
video(sys.argv[2], 15)
