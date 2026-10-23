# -*- coding: utf-8 -*-
"""
Cuts Triguy - drawn by Elim - out of Elim's own drawings. Run with the only Python on this machine:

    python tools/art/cut_triguy.py

Like cut_circy.py, this is the hand-cut step before the M3 importer exists, done as a script so
it can be re-run: the parts are GENERATED from fighters/triguy/source/, the master copy, and are
never edited by hand. Nothing here draws, cleans, or smooths a line. Pixels are only cropped,
masked apart, rotated and trimmed - the same things the rig does to a part every frame - and,
on Eric's word, every line widened in place (LINE_BOOST) and the white coloured in (below).

Emits:
    fighters/triguy/parts/*.png   his body, the hat, the face's three marks, and the four limbs in
                                  canonical orientation
    fighters/triguy/poses/*.png   the taunts, the crying pose, the effects and the thrown hat
    fighters/triguy/rig.json      bone tree, offsets, pivots, where each pose stands, and how his
                                  face turns as he bows into his point attack ("tipped")

HE STANDS AS HE IS DRAWN STANDING (Eric's call, 2026-10-23). Elim drew him standing still -
idle.png, "the way he looks when nobody is pressing anything" - after "triguys idle is STILL his
stab" (the rig was his stab drawing, triguy.png, stood on end). In it he is tall: a white triangle
with his face, point up, and under it the rest of his body, drawn as an outline that comes round
from the triangle's front corner to meet its back at a point - with a black top hat over the point,
an arm out of each side, and two short legs out of the bottom point. On a transparent canvas, side
on, facing right. cut_idle takes it apart:
  - the BODY is the triangle's white fill grown out over its outline, and the lower body's lines.
    The paper inside the lower body is filled in white, as the triangle is (rule 1: fill in what is
    missing). He has no separate head.
  - the FACE - the two eyes and the smile drawn inside the triangle - is lifted off it, each mark
    whole, and the paper under them filled in white. They are hung back on the body as extras the
    game moves (FighterRig.SetFace) - lifted here, not by faces.py, so they can turn back level as
    he bows over.
  - the HAT - the one big stroke above it - is masked off as its own piece, so he can take it off
    and throw it (his side special, Eric's call 2026-10-10). It is the same pixels, only kept
    apart; a copy is also saved as poses/hat.png.
  - the back arm is a stroke on its own. The front arm, the lower body and the legs are one stroke
    where they meet, and are taken apart along the lines Elim drew: the back line of the body and
    the back leg are the leftmost line in each row; the front of the body and the front arm come off
    the bottom corner side by side and part; and where the front of the body comes round to meet
    the back is his hip, with the legs below it.
  - each limb's joint is its top end, where it meets the body. It is rotated about that joint until
    its far end hangs straight down (the canonical orientation), then split at half its length into
    upper and lower pieces that overlap a little at the knee or elbow - the knee put where the
    stroke actually crosses halfway, as for Circy.
Nothing is redrawn: pieces are only masked apart, turned and moved.

HIS POINT ATTACK (AttackAnim.PointThrust: the dash attack, the forward smash, the last jab, a
throw) bows him forward from the hips until his point is out level in front, as triguy.png has it.
His face turns back level as he goes over (rig.json "tipped", from TIP_FROM degrees to STAB_TIP).

The other drawings are front or three-quarter views shown whole: his three taunt poses, the
crying pose for his puddle, and three effects - the spike that comes up out of the puddle, the
trampoline, and the jagged outline that goes round him while he is invincible.

COLOUR, on Eric's call: Elim drew him white, and white he stays (2026-10-16 - for a day he was
light green, 2026-10-15). But blank paper reads as a hole, not a body, so the white of his body -
the one big white shape in each drawing - is coloured in with a white pencil, the same coloured-
pencil recipe as the stand-ins (drawn.pencil_coverage): strokes of white with a pale grey tooth
showing between them, a shade deeper round its rim so it reads as a solid thing. The gaps between
his teeth are filled clean white in the mouth piece, so they stay white as it moves; and his hat
gets a sheen along its top edges (HAT_SHEEN), so it reads as a shiny top hat rather than a black
blot. Every line stays exactly where and as Elim drew it: this colours in
what he left white or filled black, after the cut, and never touches source/.
"""

from __future__ import division, print_function

import json
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

import cut_circy as circy
import drawn
import linework

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
ROOT = os.path.join(REPO, 'fighters', 'triguy')
SOURCE = os.path.join(ROOT, 'source')

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

# His colouring (see the note at the top): white pencil over a pale grey tooth, deeper at the
# rim, and the sheen on his hat.
BODY_WHITE = (253, 253, 250)
BODY_TOOTH = (188, 193, 208)
BODY_RIM = (178, 182, 198)
HAT_SHEEN = (110, 116, 140)

# He is drawn several times as big as the stand-ins and shrunk further, so the pencil is laid down
# small and scaled up: its strokes come out as wide at match size as theirs.
PENCIL_SCALE = 6

OUTLINE_GROW = 16      # px added around the white fill to take in the triangle's outline
STRAY_PIXELS = 400     # a stroke smaller than this is a stray mark, given to the nearest limb
BODY_CORNER_PIXELS = 3000  # a piece this small touching the body is a corner of its outline
JOINT_OVERLAP = 10     # px each half of a split limb extends past the joint

# How many pixels each side every line of ink is widened by in the generated parts and poses
# (tools/art/linework.py). He is drawn on a bigger canvas than Circy and shrunk further, so it
# takes more to bring his line to about 2.5px wide at match size. Eric's call, 2026-10-04. 0
# gives Elim's line exactly as drawn.
LINE_BOOST = 12

FACE_MIN = 1000      # px: a mark inside the body at least this big is part of his face
MOUTH_STRETCH = 0.12  # how far his mouth opens: the drawing has little room under it
TIP_FROM = 40.0      # degrees forward his bow starts turning his face back level (the rig's "tipped")


def groups(points):
    """The 8-connected groups among a list of points."""
    left = set(points)
    out = []
    while left:
        start = left.pop()
        group, queue = [start], [start]
        while queue:
            x, y = queue.pop()
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    q = (x + dx, y + dy)
                    if q in left:
                        left.remove(q)
                        group.append(q)
                        queue.append(q)
        out.append(group)
    return out


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


def limb_parts(img, d, name_upper, name_lower, parts, parts_dir):
    """One limb stroke, rotated about its joint until its far end hangs straight down, split at half
    its length into an upper and a lower piece that overlap a little at the knee or elbow - the knee
    where the stroke actually crosses halfway, as for Circy. Returns its length and where the knee is
    across from the joint."""
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


# --- Standing, from his drawing of himself standing ------------------------------------------------
# Elim drew him standing still (idle.png, 2026-10-22, sent for "the way he looks when nobody is
# pressing anything"): point up, the hat over the point, the face in the triangle, the back arm out
# of its side and hanging, the front arm and the front leg out of the bottom corner, and two very
# long legs. Standing, he is cut from that - every piece where Elim drew it - rather than stood up
# from the stab (Eric, 2026-10-23). The stab is still triguy.png: tipped over, the pieces slide to
# their places there (cut_main measures them), scaled to this triangle.

IDLE = 'idle.png'
IDLE_BASE_CUT = 16   # px below the white the bottom's line runs: ink above it is the body's, below it a limb's
IDLE_LEG_WIDTH = 24  # px: the straight back leg's line, taken from the left of each row where the bent leg meets it
IDLE_FOOT_ROWS = 30  # px up from the bottom of the back leg that are its foot, taken whole
IDLE_PART_ROWS = 140  # px down from the bottom corner the front arm and the front of his body run side by side
STAB_TIP = 79.2      # degrees he bows over in his point attack (FighterAnimations.PointThrustStrike, as played)


def cut_idle(parts_dir):
    """The standing rig, from idle.png: body, face, hat and limbs where he drew them."""
    img = Image.open(os.path.join(SOURCE, IDLE)).convert('RGBA')
    mask = white_mask(img)
    mpx = mask.load()
    px = img.load()
    w, h = img.size

    body_pts = [(x, y) for y in range(h) for x in range(w) if px[x, y][3] > 0 and mpx[x, y] > 0]
    base_y = max(p[1] for p in body_pts)
    strokes = circy.components(img, lambda x, y, p: p[3] > 0 and not mpx[x, y] > 0)
    strokes.sort(key=len, reverse=True)

    # Bits of the triangle's own outline outside the grown white - its point, slivers of its side -
    # are the body's.
    near = mask.filter(ImageFilter.MaxFilter(9)).load()
    corners = [s for s in strokes if len(s) < BODY_CORNER_PIXELS and any(near[x, y] > 0 for (x, y) in s[::5])]
    for corner in corners:
        body_pts.extend(corner)
    strokes = [s for s in strokes if not any(s is c for c in corners)]
    big = [s for s in strokes if len(s) >= STRAY_PIXELS]
    strays = [s for s in strokes if len(s) < STRAY_PIXELS]

    hat = min(big, key=lambda s: circy.bbox(s)[1])
    rest = [s for s in big if s is not hat]
    # The back arm is on its own. The legs and the front arm are one stroke where they meet: the
    # front leg comes down off the bottom corner beside the front arm, and its knee lands on the
    # back leg. Taken apart along those lines, every pixel as drawn.
    rest.sort(key=len, reverse=True)
    joined, back_arm_pts = rest[0], rest[1]
    if len(rest) != 2:
        raise SystemExit('expected the back arm and one joined stroke in %s, found %d' % (IDLE, len(rest)))

    cut_line = base_y + IDLE_BASE_CUT
    rows = {}
    for (x, y) in joined:
        if y < cut_line:
            body_pts.append((x, y))
        else:
            rows.setdefault(y, []).append(x)
    bottom = max(rows)
    back_line, others = [], []
    widths = {}
    for y, xs in rows.items():
        xs.sort()
        left = xs[0]
        # The leftmost run of ink in each row is the back of his body and then his back leg - its
        # top where it leaves the bottom line, and its foot, whole - except where the front of his
        # body comes round to meet it: there it is one line wide.
        run_end = left
        while run_end + 1 in xs:
            run_end += 1
        widths[y] = run_end - left
        knee_zone = cut_line + IDLE_PART_ROWS < y < bottom - IDLE_FOOT_ROWS
        reach = min(run_end, left + IDLE_LEG_WIDTH) if knee_zone else run_end
        for x in xs:
            (back_line if x <= reach else others).append((x, y))
    # Where the front of his body comes round and meets the back: the first row, well down, where
    # the leftmost run is wider than a line. Above it the back line is his body; below, his leg.
    join_y = min(y for y in widths if y > cut_line + IDLE_PART_ROWS and widths[y] > IDLE_LEG_WIDTH + 8)
    # The knot where they meet is the body's, a line's width of it, so its bottom is closed.
    join_y += IDLE_LEG_WIDTH
    # The rest is the front of his body, the front arm beside it, and below where the front meets
    # the back, his front leg. Down from the bottom corner the arm (outside) and the front of the
    # body (inside) are one band, split down its middle, and where they are still one line it is
    # the body's: the arm starts where they part. Below that the front of his body is the leftmost
    # line in every row, and the arm lies outside it - except where his hand curls back and
    # touches it, and there the body's line is one line wide.
    others_rows = {}
    for (x, y) in others:
        others_rows.setdefault(y, []).append(x)
    front, arm = [], []
    for y, xs in others_rows.items():
        xs.sort()
        if y > join_y:
            front.extend((x, y) for x in xs)
            continue
        if y < cut_line + IDLE_PART_ROWS:
            middle = (xs[0] + xs[-1]) / 2.0
            wide = xs[-1] - xs[0] > IDLE_LEG_WIDTH * 1.4
            for x in xs:
                (front if x < middle or not wide else arm).append((x, y))
            continue
        run_end = xs[0]
        while run_end + 1 in xs:
            run_end += 1
        if run_end - xs[0] > IDLE_LEG_WIDTH * 1.6:
            run_end = xs[0] + int(IDLE_LEG_WIDTH * 1.3)
        for x in xs:
            (front if x <= run_end else arm).append((x, y))
    # Anything of the arm's not joined to it is a crumb of the body's line.
    arm_groups = groups(arm)
    arm_groups.sort(key=len, reverse=True)
    front_arm_pts = arm_groups[0]
    for crumb in arm_groups[1:]:
        front.extend(crumb)
    body_pts.extend(p for p in front + back_line if p[1] <= join_y)
    front_leg_pts = [p for p in front if p[1] > join_y]
    back_leg_pts = [p for p in back_line if p[1] > join_y]

    limbs = [back_arm_pts, front_arm_pts, back_leg_pts, front_leg_pts]
    for stray in strays:
        sx, sy = stray[0]
        nearest = min(limbs, key=lambda l: min((p[0] - sx) ** 2 + (p[1] - sy) ** 2 for p in l[::25]))
        nearest.extend(stray)

    # The face: two eyes - two upright strokes - and the smile, the lowest mark, all inside the
    # triangle.
    ink = groups([p for p in body_pts if px[p[0], p[1]][3] > 0 and not is_white(px[p[0], p[1]])])
    ink.sort(key=len, reverse=True)
    marks = [g for g in ink[1:] if len(g) >= FACE_MIN and circy.bbox(g)[3] < base_y]
    if len(marks) != 3:
        raise SystemExit('expected 2 eyes and a mouth in %s, found %d marks' % (IDLE, len(marks)))
    mouth_mark = max(marks, key=lambda g: circy.bbox(g)[3])
    back_eye_mark, front_eye_mark = sorted([g for g in marks if g is not mouth_mark], key=lambda g: circy.bbox(g)[0])
    face_set = set(mouth_mark) | set(back_eye_mark) | set(front_eye_mark)
    body_pts = [p for p in body_pts if p not in face_set]

    # His hip: the bottom point of his body, where both legs come out.
    leg_tops = [min(pts, key=lambda p: (p[1], p[0])) for pts in (back_leg_pts, front_leg_pts)]
    hip = ((leg_tops[0][0] + leg_tops[1][0]) / 2.0, float(join_y))

    def from_hip(point):
        return [round(point[0] - hip[0], 1), round(point[1] - hip[1], 1)]

    parts = {}

    def described(points):
        joint = min(points, key=lambda p: (p[1], p[0]))
        tip = max(points, key=lambda p: (p[0] - joint[0]) ** 2 + (p[1] - joint[1]) ** 2)
        return {'points': points, 'joint': joint, 'tip': tip}

    limb_names = [(back_arm_pts, 'ArmBack'), (front_arm_pts, 'ArmFront'), (back_leg_pts, 'LegBack'), (front_leg_pts, 'LegFront')]
    cut = {}
    for points, name in limb_names:
        d = described(points)
        length, knee = limb_parts(img, d, name + '_Upper', name + '_Lower', parts, parts_dir)
        cut[name] = (d, length, knee)

    # The body, with the face's marks lifted off it and the paper under them filled in white.
    body, origin = circy.extract(img, body_pts)
    bpx = body.load()
    bw, bh = body.size
    for (x, y) in face_set:
        for dx in range(-2, 3):
            for dy in range(-2, 3):
                q = (x - origin[0] + dx, y - origin[1] + dy)
                if 0 <= q[0] < bw and 0 <= q[1] < bh and ((x + dx, y + dy) in face_set or is_white(bpx[q])):
                    bpx[q] = (255, 255, 255, 255)
    # Below the triangle his body is drawn as an outline only: the paper inside it is filled in white,
    # as the triangle is, so it reads as body and not a hole (rule 1: fill in what is missing).
    seed = (int(hip[0] - origin[0]) + 3 * IDLE_LEG_WIDTH, int((cut_line + join_y) / 2.0 - origin[1]))
    # Filled up to his lines thickened a little, so a hairline gap in them does not let it out;
    # then out to the lines themselves.
    lines = body.split()[3].point(lambda v: 255 if v > 128 else 0).filter(ImageFilter.MaxFilter(7))
    inside = lines.point(lambda v: 0 if v else 255)
    ImageDraw.floodfill(inside, seed, 128)
    ipx = inside.load()
    if any(ipx[x, y] == 128 for x in (0, bw - 1) for y in range(bh)) or any(ipx[x, y] == 128 for y in (0, bh - 1) for x in range(bw)):
        body.save(os.path.join(ROOT, 'leak_body.png'))
        inside.save(os.path.join(ROOT, 'leak_inside.png'))
        raise SystemExit('the inside of his lower body is not closed in %s (seed %s)' % (IDLE, seed))
    filled = inside.point(lambda v: 255 if v == 128 else 0).filter(ImageFilter.MaxFilter(7))
    filled = ImageChops.multiply(filled, body.split()[3].point(lambda v: 0 if v > 128 else 255))
    body.paste((255, 255, 255, 255), (0, 0) + body.size, filled)
    body, bpad = linework.thicken(body, LINE_BOOST)
    body.save(os.path.join(parts_dir, 'Torso.png'))
    parts['Torso'] = {'texture': 'parts/Torso.png',
                      'pivot': [round(hip[0] - origin[0] + bpad, 2), round(hip[1] - origin[1] + bpad, 2)]}

    # The hat, where he drew it over the point, hung from the middle of its brim.
    hat_img, hat_origin = circy.extract(img, hat)
    hpx = hat_img.split()[3].load()
    hw, hh = hat_img.size
    brim_y = max(y for y in range(hh) if any(hpx[x, y] > 128 for x in range(0, hw, 2)))
    brim_xs = [x for y in range(brim_y - hh // 6, brim_y + 1) for x in range(hw) if hpx[x, y] > 128]
    brim = ((max(brim_xs) + min(brim_xs)) / 2.0, float(brim_y))
    hat_img, hpad = linework.thicken(hat_img, LINE_BOOST)
    hat_img.save(os.path.join(parts_dir, 'Hat.png'))
    parts['Hat'] = {'texture': 'parts/Hat.png', 'pivot': [round(brim[0] + hpad, 2), round(brim[1] + hpad, 2)]}
    hat_at = from_hip((hat_origin[0] + brim[0], hat_origin[1] + brim[1]))
    crown = hip[1] - circy.bbox(hat)[1]

    face = []
    for mark, name, kind, tilt in ((back_eye_mark, 'FaceEye', 'eye', 1),
                                   (front_eye_mark, 'FaceEyeFront', 'eye', -1),
                                   (mouth_mark, 'FaceMouth', 'mouth', 1)):
        piece, porigin = circy.extract(img, mark)
        x0, y0, x1, y1 = circy.bbox(mark)
        pin = ((x0 + x1) / 2.0, y0 if kind == 'mouth' else (y0 + y1) / 2.0)
        piece, fpad = linework.thicken(piece, LINE_BOOST)
        piece.save(os.path.join(parts_dir, name + '.png'))
        parts[name] = {'texture': 'parts/%s.png' % name,
                       'pivot': [round(pin[0] - porigin[0] + fpad, 1), round(pin[1] - porigin[1] + fpad, 1)]}
        face.append({'name': name, 'kind': kind, 'tilt': tilt, 'at': from_hip(pin)})

    def root(name):
        return from_hip(cut[name][0]['joint'])

    def lower(name):
        return [round(cut[name][2], 1), round(cut[name][1] / 2, 1)]

    bones = [
        {'name': 'Hip', 'parent': None, 'offset': [0, 0], 'part': None},
        {'name': 'LegBack_Upper', 'parent': 'Hip', 'offset': root('LegBack'), 'part': 'LegBack_Upper'},
        {'name': 'LegBack_Lower', 'parent': 'LegBack_Upper', 'offset': lower('LegBack'), 'part': 'LegBack_Lower'},
        {'name': 'LegFront_Upper', 'parent': 'Hip', 'offset': root('LegFront'), 'part': 'LegFront_Upper'},
        {'name': 'LegFront_Lower', 'parent': 'LegFront_Upper', 'offset': lower('LegFront'), 'part': 'LegFront_Lower'},
        {'name': 'Torso', 'parent': 'Hip', 'offset': [0, 0], 'part': 'Torso'},
        {'name': 'ArmBack_Upper', 'parent': 'Torso', 'offset': root('ArmBack'), 'part': 'ArmBack_Upper'},
        {'name': 'ArmBack_Lower', 'parent': 'ArmBack_Upper', 'offset': lower('ArmBack'), 'part': 'ArmBack_Lower'},
        {'name': 'Head', 'parent': 'Torso', 'offset': [0, round(-crown, 1)], 'part': None},
        {'name': 'ArmFront_Upper', 'parent': 'Torso', 'offset': root('ArmFront'), 'part': 'ArmFront_Upper'},
        {'name': 'ArmFront_Lower', 'parent': 'ArmFront_Upper', 'offset': lower('ArmFront'), 'part': 'ArmFront_Lower'},
    ]
    extras = [{'name': 'hat', 'bone': 'Torso', 'part': 'Hat', 'offset': hat_at, 'always': True, 'onPart': True}]
    for f in face:
        extras.append({'name': 'face' + f['name'][4:], 'bone': 'Torso', 'part': f['name'], 'offset': f['at'],
                       'always': True, 'onPart': True, 'face': f['kind'], 'tilt': f['tilt'],
                       'stretch': MOUTH_STRETCH if f['kind'] == 'mouth' else 1.1, 'scale': 1.0})

    # His point attack bows him forward from the hips until the point is out in front, as far over
    # as the triangle lies in the stab drawing. His legs stay under him, his arms go with his body,
    # and his face turns back level, as drawn there.
    tipped = {'angle': STAB_TIP, 'from': TIP_FROM, 'extras': {}}
    for f in face:
        tipped['extras']['face' + f['name'][4:]] = {'offset': f['at'], 'rotation': -STAB_TIP, 'scale': 1.0}

    leg_length = (cut['LegBack'][1] + cut['LegFront'][1]) / 2.0
    print('standing from %s: hip to crown %d, legs %d/%d, arms %d/%d' % (
        IDLE, crown, cut['LegBack'][1], cut['LegFront'][1], cut['ArmBack'][1], cut['ArmFront'][1]))
    return bones, parts, extras, hat_img, tipped, {'canonicalHeight': round(crown, 1),
                                                   'legLength': round(leg_length, 1),
                                                   'figureHeight': round(crown + leg_length)}


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


def _largest(small, count=1):
    """The largest 4-connected region of a (small) mask - or the `count` largest, together."""
    w, h = small.size
    sp = small.load()
    seen = bytearray(w * h)
    found = []
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
        found.append(comp)
    found.sort(key=len, reverse=True)
    region = Image.new('L', small.size, 0)
    rp = region.load()
    for comp in found[:count]:
        for i in comp:
            rp[i % w, i // w] = 255
    return region


def colour_body(img, seed, shapes=1):
    """The one big white shape - his body - coloured in white pencil, deeper toward its edge (or
    the `shapes` biggest: standing, his triangle and the body under it, with a line between).
    Smaller whites (an eye, the gaps in a mouth) stay white; nothing that is not white changes."""
    r, g, b, a = img.split()
    white = ImageChops.darker(ImageChops.darker(r, g), b).point(lambda v: 255 if v > 200 else 0)
    white = ImageChops.multiply(white, a.point(lambda v: 255 if v > 128 else 0))
    small = _largest(_small(white), shapes)
    body = _full(small, white)
    depth = small.filter(ImageFilter.MinFilter(7)).filter(ImageFilter.MinFilter(7))
    depth = depth.filter(ImageFilter.GaussianBlur(26 // SMALL)).resize(img.size, Image.BILINEAR)
    w, h = img.size
    cover = drawn.pencil_coverage((w // PENCIL_SCALE + 1, h // PENCIL_SCALE + 1), seed)
    cover = cover.resize((w // PENCIL_SCALE * PENCIL_SCALE + PENCIL_SCALE, h // PENCIL_SCALE * PENCIL_SCALE + PENCIL_SCALE),
                         Image.BICUBIC).crop((0, 0, w, h))
    pencil = Image.composite(Image.new('RGBA', img.size, BODY_WHITE + (255,)),
                             Image.new('RGBA', img.size, BODY_TOOTH + (255,)), cover)
    rim = Image.blend(pencil, Image.new('RGBA', img.size, BODY_RIM + (255,)), 0.55)
    shade = Image.composite(pencil, rim, depth)
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
    redo(os.path.join(parts_dir, 'Torso.png'), lambda img: colour_body(img, 'triguy-torso', 2))
    redo(os.path.join(parts_dir, 'FaceMouth.png'), fill_teeth)
    redo(os.path.join(parts_dir, 'Hat.png'), hat_sheen)
    redo(os.path.join(poses_dir, 'hat.png'), hat_sheen)
    for name, _ in BODY_POSES:
        redo(os.path.join(poses_dir, name + '.png'), lambda img, name=name: colour_body(img, 'triguy-' + name), hat_sheen)
    print('coloured in')


def main():
    parts_dir = os.path.join(ROOT, 'parts')
    poses_dir = os.path.join(ROOT, 'poses')
    for d in (parts_dir, poses_dir):
        if not os.path.isdir(d):
            os.makedirs(d)

    bones, parts, extras, hat_img, tipped, dims = cut_idle(parts_dir)
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
        # Tipped forward into his point attack, he turns back into the drawing (see cut_main).
        'tipped': tipped,
        'bones': bones,
        'extras': extras,
        'parts': parts,
        'poses': poses,
    }
    with open(os.path.join(ROOT, 'rig.json'), 'wb') as fh:
        fh.write(json.dumps(rig, indent=2, sort_keys=True).encode('utf-8'))
    # His eyes and mouth were lifted out in cut_main, not by faces.py: they are marks of their own
    # on the drawing, and have to stay level while the body under them is stood on end. Then the
    # colouring in, over where the face was as much as the rest of him.
    colour_in(parts_dir, poses_dir)
    print('done')


if __name__ == '__main__':
    main()
