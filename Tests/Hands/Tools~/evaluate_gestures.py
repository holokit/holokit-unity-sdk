"""Old (Unity SDK before the port) vs new (Swift SDK, ported) gesture classifiers on the same
Apple Vision joints. Both rules are line-by-line copies of the C# code."""
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

def new(h):
    # HandGestureClassifier (Swift GestureRecognizer).
    ok = lambda j: h[j][2] >= 0.35
    if ok(TT) and ok(IT) and dist(h[TT], h[IT]) < 0.12:
        return 'Pinched'
    for f in FINGERS:
        if not all(ok(j) for j in f):
            return 'None'
        length = sum(dist(h[f[i]], h[f[i + 1]]) for i in range(3))
        if length <= 0.025 or dist(h[f[0]], h[f[3]]) / length < 0.82:
            return 'None'
    return 'Five'

LABEL = {'open': 'Five', 'pinch': 'Pinched', 'hands': 'Five', 'person': 'None'}

def photos(path):
    rows = []
    for line in open(path):
        d = json.loads(line)
        name = d['file'].split('/')[-1][:-4]
        kind, angle = name.split('_')
        for h in d['hands']:
            rows.append((kind, int(angle), LABEL[kind], old(h), new(h)))
    print('== Photos: every detected hand, 8 rotations of 4 photos (Vision missed some rotations for both)')
    print(f"{'photo':8}{'angle':>6}  {'truth':8}{'old':9}{'new':9}")
    for r in rows:
        print(f'{r[0]:8}{r[1]:>6}  {r[2]:8}{r[3]:9}{r[4]:9}')
    for who, i in (('old', 3), ('new', 4)):
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
    counts = {'old': collections.Counter(), 'new': collections.Counter()}
    changes = {'old': 0, 'new': 0}
    filters = {'old': Filter(), 'new': Filter()}
    disagree = []
    hands = 0
    for i, d in enumerate(frames):
        if not d['hands']:
            continue
        hands += 1
        h = d['hands'][0]
        for who, fn in (('old', old), ('new', new)):
            g = fn(h)
            counts[who][g] += 1
            changes[who] += filters[who].observe(g)
        if old(h) != new(h):
            disagree.append((i, old(h), new(h), d['file']))
    minutes = len(frames) / fps / 60
    print(f'\n== Video: {len(frames)} frames at {fps} fps ({minutes:.1f} min), first hand in {hands} frames')
    for who in ('old', 'new'):
        c = counts[who]
        print(f"{who}: Five {c['Five']}  Pinched {c['Pinched']}  None {c['None']}  "
              f"gesture changes after the 4-frame hold: {changes[who]} ({changes[who] / minutes:.0f}/min)")
    kinds = collections.Counter((o, n) for _, o, n, _ in disagree)
    print(f'disagreements: {len(disagree)} frames: ' + ', '.join(f'{o}->{n} x{c}' for (o, n), c in kinds.most_common()))
    json.dump(disagree, open(path.replace('.jsonl', '-disagree.json'), 'w'))

photos(sys.argv[1])
video(sys.argv[2], 15)
