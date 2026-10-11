# -*- coding: utf-8 -*-
"""Eyes and mouths, lifted out of a fighter's own drawing so the game can move them.

Nothing is redrawn. Each feature is taken out of the part it was drawn on pixel for pixel and
hung back exactly where it was, as its own extra (rig.json "face"), so at rest a fighter looks
precisely as drawn. The game then only squashes, tilts, nudges and stretches those marks - a
blink is an eye squashed flat, a shout a mouth stretched open (FighterRig.SetFace). The gap a
feature leaves in the part under it is filled with what is round it: the flat colour of the
face, or the drawing's own texture borrowed from beside it. Filling in what is missing is
allowed; replacing what is there is not (CLAUDE.md, rule 1).

Called by each cut script at the end, after it has written the part and rig.json, so a re-cut
always starts from the drawing and gets its face back:

    faces.apply(ROOT, 'Torso', 'Torso', [
        dict(name='EyeLeft', kind='eye', box=(330, 150, 400, 480), rule='ink', fill='flat', tilt=1),
        ...
    ])

A feature is found inside its box by `rule`:
    'ink'    dark marker or pencil lines
    'colour' strongly coloured marks (red, green, yellow) and the pencil line round them
    'glow'   bright orange and yellow marks (a glowing eye) and the dark ink in and round them
    'teeth'  pale, unsaturated marks (teeth) and the dark ink between them
Only marks that lie wholly inside the box - or that cross the point(s) in `seeds` - are taken,
so an outline that happens to pass through the box stays on the body.

`fill` is 'flat' (the face's own colour, measured round the feature) or 'texture' (the
drawing's own pixels from beside the feature, for a pencilled face). `pivot` is 'centre' (eyes,
brows) or 'top' (mouths open downwards). `tilt` is which way an eye or brow turns when the face
is angry: 1 to lower its front or inner end on the right of the drawing, -1 on the left.
`stretch` is how far a mouth may open, as a fraction of its own height - less where the
drawing's edge is close under it.
"""

import json
import os

from PIL import Image


def luma(p):
    return 0.3 * p[0] + 0.59 * p[1] + 0.11 * p[2]


def saturation(p):
    hi, lo = max(p[0], p[1], p[2]), min(p[0], p[1], p[2])
    return 0.0 if hi == 0 else (hi - lo) / float(hi)


def is_ink(p):
    return p[3] > 60 and luma(p) < 120


def is_colour(p):
    return p[3] > 60 and saturation(p) > 0.38 and luma(p) > 50


def is_colour_or_line(p):
    return p[3] > 60 and ((saturation(p) > 0.38 and luma(p) > 50) or luma(p) < 125)


def is_glow(p):
    return p[3] > 60 and ((p[0] > 200 and p[1] > 135 and p[2] < 170) or luma(p) < 65)


def is_teeth(p):
    return p[3] > 60 and ((luma(p) > 150 and saturation(p) < 0.3) or luma(p) < 65)


RULES = {'ink': is_ink, 'colour': is_colour_or_line, 'glow': is_glow, 'teeth': is_teeth}


def find(img, box, rule, seeds, grow):
    """The feature's pixels in the box: its marks, grown a little to take their soft edges."""
    px = img.load()
    x0, y0, x1, y1 = box
    test = RULES[rule]
    hit = set((x, y) for y in range(y0, y1) for x in range(x0, x1) if test(px[x, y]))

    # Connected marks; keep those wholly inside the box, or under a seed.
    keep = set()
    seen = set()
    seed_set = set(tuple(s) for s in seeds or [])
    for start in hit:
        if start in seen:
            continue
        group, stack, touches = [], [start], False
        seen.add(start)
        while stack:
            x, y = stack.pop()
            group.append((x, y))
            if x in (x0, x1 - 1) or y in (y0, y1 - 1):
                touches = True
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if n in hit and n not in seen:
                    seen.add(n)
                    stack.append(n)
        seeded = any(near(g, seed_set) for g in group) if seed_set else False
        if seeded or not touches:
            keep.update(group)

    # Grown by a few pixels for the anti-aliased edge, and holes closed, inside the box only.
    for _ in range(grow):
        edge = set()
        for x, y in keep:
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if x0 <= n[0] < x1 and y0 <= n[1] < y1 and n not in keep and px[n][3] > 0:
                    edge.add(n)
        keep |= edge
    return keep


def near(p, seeds, reach=6):
    return any(abs(p[0] - s[0]) <= reach and abs(p[1] - s[1]) <= reach for s in seeds)


def median_colour(img, box, mask):
    px = img.load()
    x0, y0, x1, y1 = box
    samples = [px[x, y] for y in range(y0, y1) for x in range(x0, x1)
               if (x, y) not in mask and px[x, y][3] > 200]
    if not samples:
        return (255, 255, 255, 255)
    return tuple(sorted(s[c] for s in samples)[len(samples) // 2] for c in range(4))


def fill_gap(img, box, mask, how, avoid):
    """
    What goes where the feature was. 'flat': the face's own colour, measured round it. 'texture':
    grown in from the edges, ring by ring, each pixel the average of the drawing already round it
    with a little hashed grain, so a pencilled or hatched face carries on under the feature
    rather than showing a flat patch.
    """
    px = img.load()
    if how != 'texture':
        flat = median_colour(img, box, avoid)
        for x, y in mask:
            px[x, y] = (flat[0], flat[1], flat[2], 255)
        return

    w, h = img.size
    todo = set(mask)
    ring = 0
    while todo:
        ring += 1
        done = {}
        for x, y in todo:
            total, count = [0, 0, 0], 0
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    n = (x + dx, y + dy)
                    if (dx or dy) and 0 <= n[0] < w and 0 <= n[1] < h and n not in todo and px[n][3] > 200:
                        p = px[n]
                        total[0] += p[0]
                        total[1] += p[1]
                        total[2] += p[2]
                        count += 1
            if count >= 2 or (count and ring > 40):
                grain = ((x * 73856093) ^ (y * 19349663)) % 15 - 7
                done[(x, y)] = tuple(max(0, min(255, total[c] // count + grain)) for c in range(3))
        if not done:
            break
        for (x, y), c in done.items():
            px[x, y] = (c[0], c[1], c[2], 255)
        todo -= set(done)


def apply(root, part, bone, features):
    rig_path = os.path.join(root, 'rig.json')
    with open(rig_path, 'rb') as fh:
        rig = json.loads(fh.read().decode('utf-8'))
    entry = rig['parts'][part]
    path = os.path.join(root, entry['texture'])
    img = Image.open(path).convert('RGBA')
    original = img.copy()
    opx = original.load()
    pivot = entry['pivot']

    masks = [find(original, f['box'], f['rule'], f.get('seeds'), f.get('grow', 2)) for f in features]
    everything = set()
    for m in masks:
        everything |= m

    extras = [e for e in rig.get('extras', []) if not e.get('face')]
    for f, mask in zip(features, masks):
        if not mask:
            raise SystemExit('faces: nothing found for %s in %s' % (f['name'], f['box']))
        xs = [p[0] for p in mask]
        ys = [p[1] for p in mask]
        left, top, right, bottom = min(xs), min(ys), max(xs) + 1, max(ys) + 1
        piece = Image.new('RGBA', (right - left, bottom - top), (0, 0, 0, 0))
        ppx = piece.load()
        for x, y in mask:
            ppx[x - left, y - top] = opx[x, y]
        name = 'Face' + f['name']
        piece.save(os.path.join(root, 'parts', name + '.png'))
        cx = (left + right) * 0.5
        cy = top if f.get('pivot', 'centre') == 'top' else (top + bottom) * 0.5
        rig['parts'][name] = {'texture': 'parts/%s.png' % name, 'pivot': [round(cx - left, 1), round(cy - top, 1)]}
        extras.append({
            'name': 'face' + f['name'], 'bone': bone, 'part': name,
            'offset': [round(cx - pivot[0], 1), round(cy - pivot[1], 1)],
            'always': True, 'onPart': True, 'face': f['kind'], 'tilt': f.get('tilt', 1),
            'stretch': f.get('stretch', 1.1),
        })
        fill_gap(img, f['box'], mask, f.get('fill', 'flat'), everything)

    img.save(path)
    rig['extras'] = extras
    with open(rig_path, 'wb') as fh:
        fh.write(json.dumps(rig, indent=2, sort_keys=True).encode('utf-8'))
    print('faces: %s - %s' % (rig.get('name', root), ', '.join(f['name'] for f in features)))
