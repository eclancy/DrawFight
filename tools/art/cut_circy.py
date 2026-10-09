# -*- coding: utf-8 -*-
"""
Cuts Circy - drawn by Elim - out of Elim's own drawings. Run with the only Python on this machine:

    python tools/art/cut_circy.py

This is the hand-cut step the roadmap asks for before the M3 importer exists, done as a script
so it can be re-run: the parts are GENERATED from fighters/circy/source/, which is the master
copy, and are never edited by hand. Nothing here draws, cleans, thickens or smooths a line.
Pixels are only ever cropped, masked apart, rotated and trimmed - the same things the rig does
to a part every frame.

Emits:
    fighters/circy/parts/*.png   the ball and the four limbs, in canonical orientation
    fighters/circy/poses/*.png   Elim's whole-pose and effect drawings, trimmed of empty canvas
    fighters/circy/rig.json      bone tree, offsets, pivots, and where each pose stands

HOW THE MAIN DRAWING IS CUT. Circy is a ball with stick limbs, drawn on a transparent canvas,
which makes the cut mechanical rather than a matter of taste:
  - the BALL is the yellow, grown outward far enough to take in its outline. It becomes the
    Torso part, face and all. He has no separate head. (Not an ellipse: Elim's ball is squarer
    at the top, and an ellipse sliced its corners off.)
  - every other opaque pixel belongs to a limb. Connected strokes are grouped, stray marks are
    given to the nearest stroke, and the four big strokes are the arms and legs.
  - each limb's joint is the point nearest the ball. It is rotated about that joint until its far
    end hangs straight down (the canonical orientation - see fighters/README.md), then split at
    half its length into upper and lower pieces that overlap a little at the knee or elbow.

The drawing faces the viewer rather than facing right. For a round face that mirrors cleanly
that costs nothing, so it is used as drawn: the limbs on the right of the page become the
"front" limbs and the ones on the left the "back" limbs.
"""

from __future__ import division, print_function

import json
import math
import os
from collections import deque

from PIL import Image, ImageFilter

import linework

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
ROOT = os.path.join(REPO, 'fighters', 'circy')
SOURCE = os.path.join(ROOT, 'source')

MAIN = 'circytcircle.png'

# Whole drawings shown as they are, when he holds a pose. "ball" poses are scaled so his ball
# matches the rig's ball, so nothing pops when the game swaps between the two. A pose with
# "scale_like" keeps the size Elim drew it at RELATIVE to another pose - the shrinking frame is
# meant to be tiny next to the one before it.
#
# His drawings of himself standing, tall and small are not cut: he stretches as the puppet now,
# in a T-pose, so the change is in his own legs (Eric's call, 2026-10-04). They stay in source/.
BODY_POSES = [
    ('lookout', 'circytcirclebombframe1.png', {}),
    ('shrinking', 'bombpart2shrinking.png', {'scale_like': 'lookout'}),
    ('bomb', 'circytcirclebombframe3.png', {}),
    ('bash', 'circytcircleshoulderbash.png', {}),
]

# Effects are sized by the game to fit the move, so they only need trimming.
EFFECTS = [
    ('boom', 'circytcirclebombframe4BOOM.png'),
    ('laser', 'lasorbeam.png'),
]

# One page with three separate drawings on it: the rope, the gun and the hook.
HOOK_SHEET = 'grapplinghook.png'

OUTLINE_GROW = 16      # px added around the yellow to take in the ball's outline
STRAY_PIXELS = 400     # a stroke smaller than this is a stray mark, given to the nearest limb
JOINT_OVERLAP = 10     # px each half of a split limb extends past the joint

# How many pixels each side every line of ink is widened by in the generated parts and poses
# (tools/art/linework.py), so his ~9px line comes out about 2.5px wide at match size instead of
# under one. Eric's call, 2026-10-04. 0 gives Elim's line exactly as drawn.
LINE_BOOST = 8


# --------------------------------------------------------------------------- helpers

def is_yellow(p):
    r, g, b, a = p
    return a > 128 and r > 170 and g > 150 and b < 150


def yellow_box(img):
    px = img.load()
    w, h = img.size
    xs, ys = [], []
    for y in range(0, h, 2):
        for x in range(0, w, 2):
            if is_yellow(px[x, y]):
                xs.append(x)
                ys.append(y)
    if not xs:
        return None
    return min(xs), min(ys), max(xs), max(ys)


def components(img, mask_fn):
    """8-connected groups of pixels that pass mask_fn, as lists of (x, y)."""
    px = img.load()
    w, h = img.size
    seen = set()
    groups = []
    for y in range(h):
        for x in range(w):
            if (x, y) in seen or not mask_fn(x, y, px[x, y]):
                continue
            group = []
            queue = deque([(x, y)])
            seen.add((x, y))
            while queue:
                cx, cy = queue.popleft()
                group.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen \
                                and mask_fn(nx, ny, px[nx, ny]):
                            seen.add((nx, ny))
                            queue.append((nx, ny))
            groups.append(group)
    return groups


def bbox(points):
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def extract(img, points):
    """Only these pixels, on a transparent canvas the size of their bounding box."""
    x0, y0, x1, y1 = bbox(points)
    out = Image.new('RGBA', (x1 - x0, y1 - y0), (0, 0, 0, 0))
    src = img.load()
    dst = out.load()
    for (x, y) in points:
        dst[x - x0, y - y0] = src[x, y]
    return out, (x0, y0)


def trim(img):
    box = img.getbbox()
    return img.crop(box), box


# --------------------------------------------------------------------------- the main drawing

def ball_mask(img):
    """The yellow, grown by OUTLINE_GROW so the outline around it comes too."""
    mask = Image.new('L', img.size, 0)
    src = img.load()
    dst = mask.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            if is_yellow(src[x, y]):
                dst[x, y] = 255
    for _ in range(OUTLINE_GROW // 4):
        mask = mask.filter(ImageFilter.MaxFilter(9))
    return mask


def cut_main(parts_dir):
    img = Image.open(os.path.join(SOURCE, MAIN)).convert('RGBA')
    x0, y0, x1, y1 = yellow_box(img)
    mask = ball_mask(img)
    mpx = mask.load()
    bx0, by0, bx1, by1 = mask.getbbox()
    cx, cy = (bx0 + bx1) / 2.0, (by0 + by1) / 2.0
    ry = (by1 - by0) / 2.0

    def in_ball(x, y):
        return mpx[x, y] > 0

    # The ball: every opaque pixel inside the grown yellow, face included.
    ball_pts = []
    px = img.load()
    for y in range(by0, by1):
        for x in range(bx0, bx1):
            if px[x, y][3] > 0 and in_ball(x, y):
                ball_pts.append((x, y))
    ball, ball_origin = extract(img, ball_pts)

    # Everything else is limbs.
    strokes = components(img, lambda x, y, p: p[3] > 0 and not in_ball(x, y))
    strokes.sort(key=len, reverse=True)
    limbs = [s for s in strokes if len(s) >= STRAY_PIXELS]
    strays = [s for s in strokes if len(s) < STRAY_PIXELS]
    if len(limbs) != 4:
        raise SystemExit('expected 4 limbs in %s, found %d' % (MAIN, len(limbs)))

    for stray in strays:
        sx, sy = stray[0]
        nearest = min(limbs, key=lambda l: min((p[0] - sx) ** 2 + (p[1] - sy) ** 2 for p in l[::25]))
        nearest.extend(stray)

    hip = (cx, cy + ry)

    described = []
    for limb in limbs:
        joint = min(limb, key=lambda p: (p[0] - cx) ** 2 + (p[1] - cy) ** 2)
        tip = max(limb, key=lambda p: (p[0] - joint[0]) ** 2 + (p[1] - joint[1]) ** 2)
        described.append({'points': limb, 'joint': joint, 'tip': tip})

    # Legs hang from the bottom of the ball; arms come out of its sides.
    described.sort(key=lambda d: d['joint'][1])
    arms, legs = described[:2], described[2:]
    arms.sort(key=lambda d: d['joint'][0])
    legs.sort(key=lambda d: d['joint'][0])

    parts = {}
    bones_extra = {}

    def canonical_limb(d, name_upper, name_lower):
        piece, origin = extract(img, d['points'])
        jx, jy = d['joint'][0] - origin[0], d['joint'][1] - origin[1]
        tx, ty = d['tip'][0] - d['joint'][0], d['tip'][1] - d['joint'][1]
        length = math.hypot(tx, ty)

        # Put the joint at the centre of a canvas big enough to spin the limb in, then rotate
        # it about that centre until the far end points straight down.
        side = int(length * 2 + 60)
        canvas = Image.new('RGBA', (side, side), (0, 0, 0, 0))
        canvas.paste(piece, (int(side / 2 - jx), int(side / 2 - jy)))
        angle = math.degrees(math.atan2(ty, tx))
        canvas = canvas.rotate(angle - 90.0, resample=Image.BICUBIC, center=(side / 2, side / 2))

        mid = side / 2 + length / 2.0
        upper = canvas.crop((0, 0, side, int(mid + JOINT_OVERLAP)))
        lower = canvas.crop((0, int(mid - JOINT_OVERLAP), side, side))

        # The knee (or elbow) goes ON his line, halfway down. A limb he drew bowed - his front
        # leg curves out - is nowhere near the straight line from hip to foot by its middle, and a
        # knee put on that line turns the lower half about empty paper, so it pulls away from the
        # upper half the moment the knee bends. Find where the stroke actually crosses halfway.
        knee_x = knee_on_stroke(canvas, int(mid), side / 2)

        upper, ubox = trim(upper)
        lower, lbox = trim(lower)
        upper, upad = linework.thicken(upper, LINE_BOOST)
        lower, lpad = linework.thicken(lower, LINE_BOOST)
        upper.save(os.path.join(parts_dir, name_upper + '.png'))
        lower.save(os.path.join(parts_dir, name_lower + '.png'))

        parts[name_upper] = {'texture': 'parts/%s.png' % name_upper,
                             'pivot': [round(side / 2 - ubox[0] + upad, 2), round(side / 2 - ubox[1] + upad, 2)]}
        lower_top = int(mid - JOINT_OVERLAP)
        parts[name_lower] = {'texture': 'parts/%s.png' % name_lower,
                             'pivot': [round(knee_x - lbox[0] + lpad, 2), round(mid - lower_top - lbox[1] + lpad, 2)]}
        return length, knee_x - side / 2

    back_arm, front_arm = arms
    back_leg, front_leg = legs
    arms_cut = [canonical_limb(back_arm, 'ArmBack_Upper', 'ArmBack_Lower'),
                canonical_limb(front_arm, 'ArmFront_Upper', 'ArmFront_Lower')]
    legs_cut = [canonical_limb(back_leg, 'LegBack_Upper', 'LegBack_Lower'),
                canonical_limb(front_leg, 'LegFront_Upper', 'LegFront_Lower')]
    arm_len = [c[0] for c in arms_cut]
    leg_len = [c[0] for c in legs_cut]
    elbow_dx = [c[1] for c in arms_cut]
    knee_dx = [c[1] for c in legs_cut]

    ball, bpad = linework.thicken(ball, LINE_BOOST)
    ball.save(os.path.join(parts_dir, 'Torso.png'))
    parts['Torso'] = {'texture': 'parts/Torso.png',
                      'pivot': [round(hip[0] - ball_origin[0] + bpad, 2), round(hip[1] - ball_origin[1] + bpad, 2)]}

    def rel(point):
        return [round(point[0] - hip[0], 1), round(point[1] - hip[1], 1)]

    bones = [
        {'name': 'Hip', 'parent': None, 'offset': [0, 0], 'part': None},
        {'name': 'LegBack_Upper', 'parent': 'Hip', 'offset': rel(back_leg['joint']), 'part': 'LegBack_Upper'},
        {'name': 'LegBack_Lower', 'parent': 'LegBack_Upper', 'offset': [round(knee_dx[0], 1), round(leg_len[0] / 2, 1)], 'part': 'LegBack_Lower'},
        {'name': 'LegFront_Upper', 'parent': 'Hip', 'offset': rel(front_leg['joint']), 'part': 'LegFront_Upper'},
        {'name': 'LegFront_Lower', 'parent': 'LegFront_Upper', 'offset': [round(knee_dx[1], 1), round(leg_len[1] / 2, 1)], 'part': 'LegFront_Lower'},
        {'name': 'Torso', 'parent': 'Hip', 'offset': [0, 0], 'part': 'Torso'},
        {'name': 'ArmBack_Upper', 'parent': 'Torso', 'offset': rel(back_arm['joint']), 'part': 'ArmBack_Upper'},
        {'name': 'ArmBack_Lower', 'parent': 'ArmBack_Upper', 'offset': [round(elbow_dx[0], 1), round(arm_len[0] / 2, 1)], 'part': 'ArmBack_Lower'},
        {'name': 'Head', 'parent': 'Torso', 'offset': [0, round(-2 * ry, 1)], 'part': None},
        {'name': 'ArmFront_Upper', 'parent': 'Torso', 'offset': rel(front_arm['joint']), 'part': 'ArmFront_Upper'},
        {'name': 'ArmFront_Lower', 'parent': 'ArmFront_Upper', 'offset': [round(elbow_dx[1], 1), round(arm_len[1] / 2, 1)], 'part': 'ArmFront_Lower'},
    ]

    print('ball %dx%d  arms %d/%d  legs %d/%d' % (
        x1 - x0, y1 - y0, arm_len[0], arm_len[1], leg_len[0], leg_len[1]))
    return bones, parts, {'ballWidth': x1 - x0, 'canonicalHeight': round(2 * ry, 1),
                          'legLength': round(sum(leg_len) / 2.0, 1)}


# --------------------------------------------------------------------------- poses and effects

def cut_poses(poses_dir):
    poses = {}

    for name, filename, extra in BODY_POSES:
        img = Image.open(os.path.join(SOURCE, filename)).convert('RGBA')
        trimmed, box = trim(img)
        yb = yellow_box(img)
        drawn_height = trimmed.size[1]
        trimmed, pad = linework.thicken(trimmed, LINE_BOOST)
        trimmed.save(os.path.join(poses_dir, name + '.png'))
        entry = {
            'texture': 'poses/%s.png' % name,
            # Stands on its lowest pixel, centred under the ball.
            'anchor': [round((yb[0] + yb[2]) / 2.0 - box[0] + pad, 1), drawn_height + pad],
            'ballWidth': yb[2] - yb[0],
        }
        entry.update({('scaleLike' if k == 'scale_like' else k): v for k, v in extra.items()})
        poses[name] = entry
        print('pose %-10s %4dx%-4d ball %d' % (name, trimmed.size[0], trimmed.size[1], entry['ballWidth']))

    for name, filename in EFFECTS:
        img = Image.open(os.path.join(SOURCE, filename)).convert('RGBA')
        trimmed, _ = trim(img)
        trimmed, _ = linework.thicken(trimmed, LINE_BOOST)
        trimmed.save(os.path.join(poses_dir, name + '.png'))
        poses[name] = {'texture': 'poses/%s.png' % name,
                       'anchor': [trimmed.size[0] / 2.0, trimmed.size[1] / 2.0]}
        print('fx   %-10s %4dx%-4d' % (name, trimmed.size[0], trimmed.size[1]))

    # The hook page: three drawings, told apart by where they sit on it. The rope is the long
    # one across the top, the gun is the left of the two below it, the hook the right.
    sheet = Image.open(os.path.join(SOURCE, HOOK_SHEET)).convert('RGBA')
    pieces = components(sheet, lambda x, y, p: p[3] > 0)
    big = sorted([c for c in pieces if len(c) >= STRAY_PIXELS], key=len, reverse=True)[:3]
    for c in pieces:
        if len(c) >= STRAY_PIXELS or not big:
            continue
        sx, sy = c[0]
        nearest = min(big, key=lambda b: min((p[0] - sx) ** 2 + (p[1] - sy) ** 2 for p in b[::25]))
        nearest.extend(c)

    rope = min(big, key=lambda c: bbox(c)[1])
    rest = sorted([c for c in big if c is not rope], key=lambda c: bbox(c)[0])
    for name, points in (('rope', rope), ('gun', rest[0]), ('hook', rest[1])):
        piece, _ = extract(sheet, points)
        piece, _ = linework.thicken(piece, LINE_BOOST)
        piece.save(os.path.join(poses_dir, name + '.png'))
        poses[name] = {'texture': 'poses/%s.png' % name,
                       'anchor': [piece.size[0] / 2.0, piece.size[1] / 2.0]}
        print('fx   %-10s %4dx%-4d' % (name, piece.size[0], piece.size[1]))

    return poses



def knee_on_stroke(canvas, row, centre_x):
    """Where the drawn stroke crosses `row` of a limb hanging straight down: the middle of the
    run of ink nearest the hip-to-foot line. Looks a few rows either side if that row happens
    to fall in a gap in the line; falls back to the line itself if there is no ink at all."""
    alpha = canvas.split()[3].load()
    w = canvas.size[0]
    for d in range(0, 24):
        for r in (row - d, row + d):
            xs = [x for x in range(w) if alpha[x, r] > 128]
            if not xs:
                continue
            runs, start = [], xs[0]
            for a, b in zip(xs, xs[1:] + [None]):
                if b is None or b != a + 1:
                    runs.append((start, a))
                    start = b
            best = min(runs, key=lambda run: abs((run[0] + run[1]) / 2.0 - centre_x))
            return (best[0] + best[1]) / 2.0
    return centre_x

def main():
    parts_dir = os.path.join(ROOT, 'parts')
    poses_dir = os.path.join(ROOT, 'poses')
    for d in (parts_dir, poses_dir):
        if not os.path.isdir(d):
            os.makedirs(d)

    bones, parts, dims = cut_main(parts_dir)
    poses = cut_poses(poses_dir)

    rig = {
        'name': 'Circy',
        'artist': 'Elim',
        'canonicalHeight': dims['canonicalHeight'],
        'legLength': dims['legLength'],
        'ballWidth': dims['ballWidth'],
        # Elim drew thin black limbs; darkening the far ones would only lose them.
        'backLimbDarken': 1.0,
        'bones': bones,
        'parts': parts,
        'poses': poses,
    }
    with open(os.path.join(ROOT, 'rig.json'), 'wb') as fh:
        fh.write(json.dumps(rig, indent=2, sort_keys=True).encode('utf-8'))
    print('done')


if __name__ == '__main__':
    main()
