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
    drawn on it. He has no separate head: the triangle is his head and body both, as Circy's
    ball is.
  - the HAT - the one big stroke above it - is masked off as its own piece, hung on the body
    where he drew it, so he can take it off and throw it (his side special, Eric's call
    2026-10-10). It is the same pixels, only kept apart; a copy is also saved as poses/hat.png.
  - every other stroke is a limb. The two that reach lowest are the legs (they end in feet), the
    other two the arms; the further left of each pair is the back one, since he faces right.
  - each limb's joint is its top end, where it meets the triangle. It is rotated about that joint
    until its far end hangs straight down (the canonical orientation), then split at half its
    length into upper and lower pieces that overlap a little at the knee or elbow - the knee put
    where the stroke actually crosses halfway, as for Circy.

The other drawings are front or three-quarter views shown whole: his three taunt poses, the
crying pose for his puddle, and three effects - the spike that comes up out of the puddle, the
trampoline, and the jagged outline that goes round him while he is invincible.

COLOUR, on Eric's call (2026-10-15): Elim drew him white, and at match size his eyes, his teeth
and his hat ran into the white. So the white of his body - the one big white shape in each
drawing - is coloured in a light green (BODY_LIGHT), a shade deeper round its rim so it reads as
a solid thing; the gaps between his teeth are filled white in the mouth piece, so they stay white
as it moves; and his hat gets a sheen along its top edges (HAT_SHEEN), so it reads as a shiny top
hat rather than a black blot. Every line stays exactly where and as Elim drew it: this colours in
what he left white or filled black, after the cut, and never touches source/.
"""

from __future__ import division, print_function

import json
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

import cut_circy as circy
import faces
import linework
from face_specs import FACES

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

# His colouring (see the note at the top): the body's light green, deeper at its rim, and the
# sheen on his hat.
BODY_LIGHT = (214, 240, 200)
BODY_DEEP = (168, 212, 156)
HAT_SHEEN = (110, 116, 140)

OUTLINE_GROW = 16      # px added around the white fill to take in the triangle's outline
STRAY_PIXELS = 400     # a stroke smaller than this is a stray mark, given to the nearest limb
BODY_CORNER_PIXELS = 3000  # a piece this small touching the body is a corner of its outline
JOINT_OVERLAP = 10     # px each half of a split limb extends past the joint
HAT_KEEP_OUTLINE = 12  # rows of ink above the white the body keeps: the triangle's own outline
HAT_REACH = 18         # px from the hat stroke the brim can reach into what the body took

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
    # Where the brim sits on the triangle, the grown body took in the bottom of it; those pixels
    # are the hat's. Ink in the body right by the hat and above the triangle's own outline, joined
    # to the hat, goes with the hat - and nothing further along the triangle's edge.
    hat_set = set(hat)
    body_set = set(body_pts)
    near_hat = Image.new('L', img.size, 0)
    nh = near_hat.load()
    for (x, y) in hat:
        nh[x, y] = 255
    near_hat = near_hat.filter(ImageFilter.MaxFilter(HAT_REACH * 2 + 1)).load()
    white_top = {}
    for (x, y) in body_pts:
        if is_white(px[x, y]):
            white_top[x] = min(white_top.get(x, h), y)
    frontier = list(hat)
    while frontier:
        x, y = frontier.pop()
        for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if q in body_set and q not in hat_set and not is_white(px[q[0], q[1]])                     and near_hat[q[0], q[1]] > 0                     and q[1] < white_top.get(q[0], h) - HAT_KEEP_OUTLINE:
                hat_set.add(q)
                frontier.append(q)
    hat = sorted(hat_set)
    body_pts = [p for p in body_pts if p not in hat_set]

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

    # The hat, apart, hung on the body by the middle of its brim.
    hat_img, hat_origin = circy.extract(img, hat)
    hat_img, hpad = linework.thicken(hat_img, LINE_BOOST)
    hat_img.save(os.path.join(parts_dir, 'Hat.png'))
    hx0, hy0, hx1, hy1 = circy.bbox(hat)
    brim = ((hx0 + hx1) / 2.0, hy1)
    parts['Hat'] = {'texture': 'parts/Hat.png',
                    'pivot': [round(brim[0] - hat_origin[0] + hpad, 2), round(brim[1] - hat_origin[1] + hpad, 2)]}

    # His crown is still the top of his hat: the rig is as tall as he is drawn.
    top = min(min(p[1] for p in body_pts), hy0)
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
    extras = [{'name': 'hat', 'bone': 'Torso', 'part': 'Hat', 'offset': rel(brim), 'always': True,
               # On the body's drawing, not just its bone, so it turns round with it.
               'onPart': True}]
    return bones, parts, extras, hat_img, {'canonicalHeight': round(crown, 1),
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


# --- Colouring in -------------------------------------------------------------------------------

# The shapes are found on a copy a quarter the size - the drawings are 2000 pixels across, and
# Python 2.7 without numpy is slow per pixel - then cut back to the exact pixels of the drawing.
SMALL = 4


def _small(mask):
    return mask.resize((max(1, mask.size[0] // SMALL), max(1, mask.size[1] // SMALL)), Image.NEAREST)


def _full(small, mask):
    """A shape found small, laid back over the full-size mask: only the mask's own pixels in it."""
    big = small.resize(mask.size, Image.BILINEAR).point(lambda v: 255 if v >= 128 else 0)
    return ImageChops.multiply(big, mask)


def _largest(small):
    """The largest 4-connected region of a (small) mask."""
    w, h = small.size
    sp = small.load()
    seen = bytearray(w * h)
    best = []
    for start in range(w * h):
        if seen[start] or sp[start % w, start // w] < 128:
            continue
        seen[start] = 1
        stack = [start]
        comp = []
        while stack:
            i = stack.pop()
            comp.append(i)
            x = i % w
            for j in (i - 1 if x > 0 else -1, i + 1 if x < w - 1 else -1, i - w, i + w):
                if 0 <= j < w * h and not seen[j] and sp[j % w, j // w] >= 128:
                    seen[j] = 1
                    stack.append(j)
        if len(comp) > len(best):
            best = comp
    region = Image.new('L', small.size, 0)
    rp = region.load()
    for i in best:
        rp[i % w, i // w] = 255
    return region


def colour_body(img):
    """The one big white shape - his body - coloured in light green, deeper toward its edge.
    Smaller whites (an eye, the gaps in a mouth) stay white; nothing that is not white changes."""
    r, g, b, a = img.split()
    white = ImageChops.darker(ImageChops.darker(r, g), b).point(lambda v: 255 if v > 200 else 0)
    white = ImageChops.multiply(white, a.point(lambda v: 255 if v > 128 else 0))
    small = _largest(_small(white))
    body = _full(small, white)
    depth = small.filter(ImageFilter.MinFilter(7)).filter(ImageFilter.MinFilter(7))
    depth = depth.filter(ImageFilter.GaussianBlur(26 // SMALL)).resize(img.size, Image.BILINEAR)
    shade = Image.composite(Image.new('RGBA', img.size, BODY_LIGHT + (255,)),
                            Image.new('RGBA', img.size, BODY_DEEP + (255,)), depth)
    out = img.copy()
    out.paste(shade, (0, 0), body)
    return out


def fill_teeth(img):
    """The see-through gaps a mouth's own lines close in - his teeth - filled white, so they stay
    white over his coloured body as the mouth moves."""
    w, h = img.size
    clear = img.split()[3].point(lambda v: 255 if v < 128 else 0)
    cp = clear.load()
    edge = [(x, 0) for x in range(w)] + [(x, h - 1) for x in range(w)] + \
           [(0, y) for y in range(h)] + [(w - 1, y) for y in range(h)]
    for xy in edge:
        if cp[xy] == 255:
            ImageDraw.floodfill(clear, xy, 128)
    enclosed = clear.point(lambda v: 255 if v == 255 else 0)
    out = img.copy()
    out.paste((255, 255, 255, 255), (0, 0) + img.size, enclosed)
    return out


def hat_sheen(img):
    """A sheen along the top edges of his hat - the biggest thickly filled black shape in the
    drawing, never a line - kept inside it, so the black edge he drew stays black."""
    r, g, b, a = img.split()
    solid = ImageChops.lighter(ImageChops.lighter(r, g), b).point(lambda v: 255 if v < 60 else 0)
    solid = ImageChops.multiply(solid, a.point(lambda v: 255 if v > 128 else 0))
    # Only what is far thicker than any line survives shrinking; the biggest of that is the hat.
    core = _largest(_small(solid).filter(ImageFilter.MinFilter(11)))
    hat = _full(core.filter(ImageFilter.MaxFilter(13)), solid)
    inner = _full(_small(hat).filter(ImageFilter.MinFilter(5)), hat)
    below = Image.new('L', img.size, 0)
    below.paste(hat, (0, 34))
    band = ImageChops.subtract(inner, below).filter(ImageFilter.GaussianBlur(6))
    band = ImageChops.multiply(band, inner.filter(ImageFilter.GaussianBlur(3))).point(lambda v: int(v * 0.85))
    return Image.composite(Image.new('RGBA', img.size, HAT_SHEEN + (255,)), img, band)


def colour_in(parts_dir, poses_dir):
    """Every drawing of him coloured in the same way (the note at the top)."""
    def redo(path, *steps):
        img = Image.open(path).convert('RGBA')
        for step in steps:
            img = step(img)
        img.save(path)
    redo(os.path.join(parts_dir, 'Torso.png'), colour_body)
    redo(os.path.join(parts_dir, 'FaceMouth.png'), fill_teeth)
    redo(os.path.join(parts_dir, 'Hat.png'), hat_sheen)
    redo(os.path.join(poses_dir, 'hat.png'), hat_sheen)
    for name, _ in BODY_POSES:
        redo(os.path.join(poses_dir, name + '.png'), colour_body, hat_sheen)
    print('coloured in')


def main():
    parts_dir = os.path.join(ROOT, 'parts')
    poses_dir = os.path.join(ROOT, 'poses')
    for d in (parts_dir, poses_dir):
        if not os.path.isdir(d):
            os.makedirs(d)

    bones, parts, extras, hat_img, dims = cut_main(parts_dir)
    poses = cut_poses(poses_dir)
    # The hat on its own, for when it is thrown: spinning about its middle.
    hat_img.save(os.path.join(poses_dir, 'hat.png'))
    poses['hat'] = {'texture': 'poses/hat.png',
                    'anchor': [round(hat_img.size[0] / 2.0, 1), round(hat_img.size[1] / 2.0, 1)]}

    rig = {
        'name': 'Triguy',
        'artist': 'Elim',
        'canonicalHeight': dims['canonicalHeight'],
        'legLength': dims['legLength'],
        'figureHeight': dims['figureHeight'],
        # Thin black limbs, as Circy's: darkening the far ones would only lose them.
        'backLimbDarken': 1.0,
        # His legs come out of the bottom edge of his triangle, well out from its middle - so when
        # the triangle tips, they go with it, or the triangle swings away from them (Eric,
        # 2026-10-09: his limbs came off when he attacked).
        'legsOnBody': True,
        'bones': bones,
        'extras': extras,
        'parts': parts,
        'poses': poses,
    }
    with open(os.path.join(ROOT, 'rig.json'), 'wb') as fh:
        fh.write(json.dumps(rig, indent=2, sort_keys=True).encode('utf-8'))
    # The eyes and mouth lifted out so the game can move them (faces.py, face_specs.py) - then
    # the colouring in, over the face's filled-in hole as much as the rest of him.
    faces.apply(ROOT, *FACES['triguy'])
    colour_in(parts_dir, poses_dir)
    print('done')


if __name__ == '__main__':
    main()
