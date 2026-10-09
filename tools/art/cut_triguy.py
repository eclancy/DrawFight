# -*- coding: utf-8 -*-
"""
Cuts Triguy - drawn by Elim - out of Elim's own drawings. Run with the only Python on this machine:

    python tools/art/cut_triguy.py

Like cut_circy.py, this is the hand-cut step before the M3 importer exists, done as a script so
it can be re-run: the parts are GENERATED from fighters/triguy/source/, the master copy, and are
never edited by hand. Nothing here draws, cleans, thickens or smooths a line. Pixels are only
cropped, masked apart, rotated and trimmed - the same things the rig does to a part every frame.

Emits:
    fighters/triguy/parts/*.png   the body and the four limbs, in canonical orientation
    fighters/triguy/poses/*.png   the taunts, the crying pose and the effects, trimmed
    fighters/triguy/rig.json      bone tree, offsets, pivots, and where each pose stands

HOW THE MAIN DRAWING IS CUT. Triguy is a white triangle with a black top hat and stick limbs, on
a transparent canvas, drawn side-on facing right:
  - the BODY is the white fill, grown outward far enough to take in its outline and the face
    drawn on it, plus the hat - the one big stroke above it. He has no separate head: the
    triangle is his head and body both, as Circy's ball is.
  - every other stroke is a limb. The two that reach lowest are the legs (they end in feet), the
    other two the arms; the further left of each pair is the back one, since he faces right.
  - each limb's joint is its top end, where it meets the triangle. It is rotated about that joint
    until its far end hangs straight down (the canonical orientation), then split at half its
    length into upper and lower pieces that overlap a little at the knee or elbow - the knee put
    where the stroke actually crosses halfway, as for Circy.

The other drawings are front or three-quarter views shown whole: his three taunt poses, the
crying pose for his puddle, and three effects - the spike that comes up out of the puddle, the
trampoline, and the jagged outline that goes round him while he is invincible.
"""

from __future__ import division, print_function

import json
import math
import os

from PIL import Image, ImageFilter

import cut_circy as circy
import linework

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
ROOT = os.path.join(REPO, 'fighters', 'triguy')
SOURCE = os.path.join(ROOT, 'source')

MAIN = 'triguy.png'

# Whole drawings of him, shown as they are while he holds a pose. Each is scaled so the figure
# stands as tall as the rig does (see FighterRig.PoseScale), whatever size Elim drew it at.
BODY_POSES = [
    ('taunt_pose', 'taunt_pose.png'),
    ('taunt_surprised', 'taunt_surprised.png'),
    ('taunt_pog', 'taunt_pog.png'),
    ('crying', 'crying.png'),
]

# Effects are sized by the game to fit the move; they are only trimmed. The trampoline stands
# with its mat at the anchor - where his feet land - rather than at its middle.
EFFECTS = [
    ('spike', 'spike.png', 'centre'),
    ('trampoline', 'trampoline.png', 'mat'),
    ('outline', 'outline.png', 'centre'),
]

OUTLINE_GROW = 16      # px added around the white fill to take in the triangle's outline
STRAY_PIXELS = 400     # a stroke smaller than this is a stray mark, given to the nearest limb
BODY_CORNER_PIXELS = 3000  # a piece this small touching the body is a corner of its outline
JOINT_OVERLAP = 10     # px each half of a split limb extends past the joint

# How many pixels each side every line of ink is widened by in the generated parts and poses
# (tools/art/linework.py). He is drawn on a bigger canvas than Circy and shrunk further, so it
# takes more to bring his line to about 2.5px wide at match size. Eric's call, 2026-10-04. 0
# gives Elim's line exactly as drawn.
LINE_BOOST = 12


def is_white(p):
    r, g, b, a = p
    return a > 128 and r > 200 and g > 200 and b > 200


def white_mask(img):
    """The white fill, grown by OUTLINE_GROW so the outline round it comes too."""
    mask = Image.new('L', img.size, 0)
    src = img.load()
    dst = mask.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            if is_white(src[x, y]):
                dst[x, y] = 255
    for _ in range(OUTLINE_GROW // 4):
        mask = mask.filter(ImageFilter.MaxFilter(9))
    return mask


def cut_main(parts_dir):
    img = Image.open(os.path.join(SOURCE, MAIN)).convert('RGBA')
    mask = white_mask(img)
    mpx = mask.load()
    px = img.load()
    w, h = img.size

    def in_body(x, y):
        return mpx[x, y] > 0

    body_pts = [(x, y) for y in range(h) for x in range(w) if px[x, y][3] > 0 and in_body(x, y)]

    strokes = circy.components(img, lambda x, y, p: p[3] > 0 and not in_body(x, y))
    strokes.sort(key=len, reverse=True)

    # The sharp corners of the triangle - its point, and the corner at the bottom left - stick
    # out past the grown white fill as small separate pieces. Anything small touching the body
    # is the body's own outline, not a stray from a limb.
    near = mask.filter(ImageFilter.MaxFilter(9)).load()
    corners = [s for s in strokes if len(s) < BODY_CORNER_PIXELS and any(near[x, y] > 0 for (x, y) in s[::5])]
    for corner in corners:
        body_pts.extend(corner)
    strokes = [s for s in strokes if not any(s is c for c in corners)]

    big = [s for s in strokes if len(s) >= STRAY_PIXELS]
    strays = [s for s in strokes if len(s) < STRAY_PIXELS]

    # The hat is the big stroke highest up; the rest are limbs.
    hat = min(big, key=lambda s: circy.bbox(s)[1])
    limbs = [s for s in big if s is not hat]
    if len(limbs) != 4:
        raise SystemExit('expected 4 limbs in %s, found %d' % (MAIN, len(limbs)))
    body_pts.extend(hat)

    for stray in strays:
        sx, sy = stray[0]
        nearest = min(limbs, key=lambda l: min((p[0] - sx) ** 2 + (p[1] - sy) ** 2 for p in l[::25]))
        nearest.extend(stray)

    described = []
    for limb in limbs:
        joint = min(limb, key=lambda p: (p[1], p[0]))
        tip = max(limb, key=lambda p: (p[0] - joint[0]) ** 2 + (p[1] - joint[1]) ** 2)
        lowest = max(p[1] for p in limb)
        described.append({'points': limb, 'joint': joint, 'tip': tip, 'lowest': lowest})

    # Legs reach the floor; arms stop short of it.
    described.sort(key=lambda d: d['lowest'])
    arms, legs = described[:2], described[2:]
    arms.sort(key=lambda d: d['joint'][0])
    legs.sort(key=lambda d: d['joint'][0])
    back_arm, front_arm = arms
    back_leg, front_leg = legs

    hip = ((back_leg['joint'][0] + front_leg['joint'][0]) / 2.0,
           (back_leg['joint'][1] + front_leg['joint'][1]) / 2.0)

    parts = {}

    def canonical_limb(d, name_upper, name_lower):
        piece, origin = circy.extract(img, d['points'])
        jx, jy = d['joint'][0] - origin[0], d['joint'][1] - origin[1]
        tx, ty = d['tip'][0] - d['joint'][0], d['tip'][1] - d['joint'][1]
        length = math.hypot(tx, ty)

        side = int(length * 2 + 60)
        canvas = Image.new('RGBA', (side, side), (0, 0, 0, 0))
        canvas.paste(piece, (int(side / 2 - jx), int(side / 2 - jy)))
        angle = math.degrees(math.atan2(ty, tx))
        canvas = canvas.rotate(angle - 90.0, resample=Image.BICUBIC, center=(side / 2, side / 2))

        mid = side / 2 + length / 2.0
        upper = canvas.crop((0, 0, side, int(mid + JOINT_OVERLAP)))
        lower = canvas.crop((0, int(mid - JOINT_OVERLAP), side, side))
        knee_x = circy.knee_on_stroke(canvas, int(mid), side / 2)

        upper, ubox = circy.trim(upper)
        lower, lbox = circy.trim(lower)
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

    arms_cut = [canonical_limb(back_arm, 'ArmBack_Upper', 'ArmBack_Lower'),
                canonical_limb(front_arm, 'ArmFront_Upper', 'ArmFront_Lower')]
    legs_cut = [canonical_limb(back_leg, 'LegBack_Upper', 'LegBack_Lower'),
                canonical_limb(front_leg, 'LegFront_Upper', 'LegFront_Lower')]
    arm_len = [c[0] for c in arms_cut]
    leg_len = [c[0] for c in legs_cut]
    elbow_dx = [c[1] for c in arms_cut]
    knee_dx = [c[1] for c in legs_cut]

    body, body_origin = circy.extract(img, body_pts)
    body, bpad = linework.thicken(body, LINE_BOOST)
    body.save(os.path.join(parts_dir, 'Torso.png'))
    parts['Torso'] = {'texture': 'parts/Torso.png',
                      'pivot': [round(hip[0] - body_origin[0] + bpad, 2), round(hip[1] - body_origin[1] + bpad, 2)]}

    def rel(point):
        return [round(point[0] - hip[0], 1), round(point[1] - hip[1], 1)]

    top = min(p[1] for p in body_pts)
    crown = hip[1] - top
    bones = [
        {'name': 'Hip', 'parent': None, 'offset': [0, 0], 'part': None},
        {'name': 'LegBack_Upper', 'parent': 'Hip', 'offset': rel(back_leg['joint']), 'part': 'LegBack_Upper'},
        {'name': 'LegBack_Lower', 'parent': 'LegBack_Upper', 'offset': [round(knee_dx[0], 1), round(leg_len[0] / 2, 1)], 'part': 'LegBack_Lower'},
        {'name': 'LegFront_Upper', 'parent': 'Hip', 'offset': rel(front_leg['joint']), 'part': 'LegFront_Upper'},
        {'name': 'LegFront_Lower', 'parent': 'LegFront_Upper', 'offset': [round(knee_dx[1], 1), round(leg_len[1] / 2, 1)], 'part': 'LegFront_Lower'},
        {'name': 'Torso', 'parent': 'Hip', 'offset': [0, 0], 'part': 'Torso'},
        {'name': 'ArmBack_Upper', 'parent': 'Torso', 'offset': rel(back_arm['joint']), 'part': 'ArmBack_Upper'},
        {'name': 'ArmBack_Lower', 'parent': 'ArmBack_Upper', 'offset': [round(elbow_dx[0], 1), round(arm_len[0] / 2, 1)], 'part': 'ArmBack_Lower'},
        {'name': 'Head', 'parent': 'Torso', 'offset': [0, round(-crown, 1)], 'part': None},
        {'name': 'ArmFront_Upper', 'parent': 'Torso', 'offset': rel(front_arm['joint']), 'part': 'ArmFront_Upper'},
        {'name': 'ArmFront_Lower', 'parent': 'ArmFront_Upper', 'offset': [round(elbow_dx[1], 1), round(arm_len[1] / 2, 1)], 'part': 'ArmFront_Lower'},
    ]

    feet = max(max(p[1] for p in legs[0]['points']), max(p[1] for p in legs[1]['points']))
    body_box = circy.bbox(body_pts)
    print('body %dx%d  arms %d/%d  legs %d/%d  figure %d tall' % (
        body_box[2] - body_box[0], body_box[3] - body_box[1],
        arm_len[0], arm_len[1], leg_len[0], leg_len[1], feet - top))
    return bones, parts, {'canonicalHeight': round(crown, 1),
                          'legLength': round(sum(leg_len) / 2.0, 1),
                          'figureHeight': feet - top}


def cut_poses(poses_dir):
    poses = {}

    for name, filename in BODY_POSES:
        img = Image.open(os.path.join(SOURCE, filename)).convert('RGBA')
        trimmed, box = circy.trim(img)
        drawn_height = trimmed.size[1]
        trimmed, pad = linework.thicken(trimmed, LINE_BOOST)
        trimmed.save(os.path.join(poses_dir, name + '.png'))
        poses[name] = {
            'texture': 'poses/%s.png' % name,
            # Stands on its lowest pixel, centred on the drawing.
            'anchor': [round(trimmed.size[0] / 2.0, 1), drawn_height + pad],
            'figureHeight': drawn_height,
        }
        print('pose %-16s %4dx%-4d' % (name, trimmed.size[0], trimmed.size[1]))

    for name, filename, anchor in EFFECTS:
        img = Image.open(os.path.join(SOURCE, filename)).convert('RGBA')
        trimmed, _ = circy.trim(img)
        # The top of the blue mat, found on the drawing as drawn: the first row, from the top, with
        # blue in it. (Found after thickening, the wider outline has covered the top of the blue.)
        tpx = trimmed.load()
        mat = next((y for y in range(trimmed.size[1])
                    if any(tpx[x, y][3] > 128 and tpx[x, y][2] > 200 and tpx[x, y][0] < 80
                           for x in range(0, trimmed.size[0], 3))), 0)
        trimmed, pad = linework.thicken(trimmed, LINE_BOOST)
        trimmed.save(os.path.join(poses_dir, name + '.png'))
        if anchor == 'mat':
            point = [round(trimmed.size[0] / 2.0, 1), mat + pad]
        else:
            point = [round(trimmed.size[0] / 2.0, 1), round(trimmed.size[1] / 2.0, 1)]
        poses[name] = {'texture': 'poses/%s.png' % name, 'anchor': point}
        print('fx   %-16s %4dx%-4d anchor %s' % (name, trimmed.size[0], trimmed.size[1], point))

    return poses


def main():
    parts_dir = os.path.join(ROOT, 'parts')
    poses_dir = os.path.join(ROOT, 'poses')
    for d in (parts_dir, poses_dir):
        if not os.path.isdir(d):
            os.makedirs(d)

    bones, parts, dims = cut_main(parts_dir)
    poses = cut_poses(poses_dir)

    rig = {
        'name': 'Triguy',
        'artist': 'Elim',
        'canonicalHeight': dims['canonicalHeight'],
        'legLength': dims['legLength'],
        'figureHeight': dims['figureHeight'],
        # Thin black limbs, as Circy's: darkening the far ones would only lose them.
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
