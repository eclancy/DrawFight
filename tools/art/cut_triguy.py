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
    fighters/triguy/parts/*.png   the body stood on end, the hat, the face's three marks, and the
                                  four limbs in canonical orientation
    fighters/triguy/poses/*.png   the taunts, the crying pose, the effects and the thrown hat
    fighters/triguy/rig.json      bone tree, offsets, pivots, where each pose stands, and where
                                  everything goes when he tips over into the drawing ("tipped")

HOW THE MAIN DRAWING IS CUT. Triguy is a white triangle with a black top hat and stick limbs, on
a transparent canvas, drawn side-on facing right:
  - the BODY is the white fill, grown outward far enough to take in its outline. He has no
    separate head: the triangle is his head and body both, as Circy's ball is.
  - the FACE - the marks drawn inside the triangle that do not touch its outline: two eyes, each
    a brow with the eye hooked down off its end, and the big mouth - is lifted off it, each mark
    whole, and the paper under them filled in white (rule 1: fill in what is missing). They are
    hung back on the body as extras the game moves (FighterRig.SetFace) - lifted here, not by
    faces.py, because they must stay level while the body under them is stood on end.
  - the HAT - the one big stroke above it - is masked off as its own piece, so he can take it off
    and throw it (his side special, Eric's call 2026-10-10). It is the same pixels, only kept
    apart and turned; a copy is also saved as poses/hat.png.
  - every other stroke is a limb. The two that reach lowest are the legs (they end in feet), the
    other two the arms; the further left of each pair is the back one, since he faces right.
  - each limb's joint is its top end, where it meets the triangle. It is rotated about that joint
    until its far end hangs straight down (the canonical orientation), then split at half its
    length into upper and lower pieces that overlap a little at the knee or elbow - the knee put
    where the stroke actually crosses halfway, as for Circy.

HOW HE STANDS (Eric's call, 2026-10-19, from Elim's feedback: "triguys idle is STILL his stab",
"he's supposed to be tall"). In triguy.png the triangle lies on its side, its point out in front:
that is his point attack, "he sticks the top of his triangle out". Every drawing of him standing -
crying.png, taunt_pog.png, taunt_surprised.png - has it on end, point up, the hat on the point and
long legs under it. So the body is turned a quarter turn and a little more (`tip`, about 98
degrees) to stand it point up, and the rest is hung on it the way those drawings have it: the legs
out of the bottom, either side of the middle (LEG_SPREAD); the arms out of the two sides, low down
(ARM_HEIGHT); the hat turned level (HAT_LEAN) and sat over the point (HAT_GRIP); and the face, each
mark level as drawn, as high on the body as it fits (fit_face). The stood-up triangle is narrower
than it was long, and the face drawn along its length does not fit across it full size, so
standing it is drawn a little closer together and shown smaller (75%) - full size again tipped
over. Nothing is redrawn: pieces are only turned and moved.

TIPPED OVER, HE IS THE DRAWING. rig.json's "tipped" says where every limb, the body, the hat and
the face are on triguy.png, measured on the stood-up body. As the body tips forward past TIP_FROM
degrees the rig slides each piece toward its place there, and at `tip` degrees he is exactly the
drawing - point forward, hat on the back corner, legs out of the underside, face level. His point
attacks (AttackAnim.PointThrust: the dash attack, the forward smash, the last jab, a throw) tip
him that far; nothing else does.

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
HAT_KEEP_OUTLINE = 12  # rows of ink above the white the body keeps: the triangle's own outline
HAT_REACH = 18         # px from the hat stroke the brim can reach into what the body took

# How many pixels each side every line of ink is widened by in the generated parts and poses
# (tools/art/linework.py). He is drawn on a bigger canvas than Circy and shrunk further, so it
# takes more to bring his line to about 2.5px wide at match size. Eric's call, 2026-10-04. 0
# gives Elim's line exactly as drawn.
LINE_BOOST = 12

# How he stands (Eric's call, 2026-10-19, from Elim's feedback - "triguys idle is STILL his
# stab", "he's supposed to be tall"): the triangle on end, point up, as in every drawing of him
# standing. The numbers say where things go on it; none of them changes a line.
LEVEL = 0.5          # 0 stands the point straight up, 1 the bottom level; his triangle is uneven
LEG_SPREAD = 0.42    # legs out from the middle of the bottom, as a share of the way to its corners
LEG_TUCK = 24        # px the legs start up inside the bottom's line, so no gap shows at the join
ARM_HEIGHT = 0.14    # arms out of the sides, this share of the way up from the bottom corners
ARM_TUCK = 34        # px in from the edge, so the arm comes out of the line, not beside it
HAT_LEAN = 32.0      # degrees his hat leans back in triguy.png, perched on the back corner; turned upright by this
HAT_GRIP = 0.34      # sat down over the point until the triangle is this share of the brim's width
FACE_ROOM = 24       # px of white kept between the face and the body's line
FACE_GAP = 24        # px kept between one mark of the face and the next
FACE_DROP = 0        # px the face sits below the highest place it fits
FACE_SCALES = [1.0 - i / 20.0 for i in range(9)]  # how big the face may be shown standing, largest first
FACE_MIN = 1000      # px: a mark inside the body at least this big is part of his face
MOUTH_STRETCH = 0.12  # how far his mouth opens: the drawing has little room under it
TIP_FROM = 40.0      # degrees forward a tip starts turning him back into the drawing (the rig's "tipped")


def turn(v, degrees):
    """A vector turned `degrees` anticlockwise on the screen (y down), as PIL's rotate turns a picture."""
    a = math.radians(degrees)
    c, s = math.cos(a), math.sin(a)
    return (v[0] * c + v[1] * s, -v[0] * s + v[1] * c)


def turned(piece, pivot, degrees):
    """A piece turned `degrees` anticlockwise about `pivot`, a point on it, and trimmed - only
    turned, as the rig turns a part every frame. Returns the piece and where `pivot` is on it."""
    w, h = piece.size
    reach = max(math.hypot(x - pivot[0], y - pivot[1]) for x in (0, w) for y in (0, h))
    side = int(reach * 2 + 8)
    ox, oy = int(round(side / 2.0 - pivot[0])), int(round(side / 2.0 - pivot[1]))
    canvas = Image.new('RGBA', (side, side), (0, 0, 0, 0))
    canvas.paste(piece, (ox, oy))
    at = (pivot[0] + ox, pivot[1] + oy)
    canvas = canvas.rotate(degrees, resample=Image.BICUBIC, center=at)
    canvas, box = circy.trim(canvas)
    return canvas, (at[0] - box[0], at[1] - box[1])


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


def fit_face(body, pivot, face, parts, parts_dir, ceiling):
    """Sets where each mark of the face goes on the stood-up body (rig offsets, from his hip),
    and how big it is shown there.

    The stood-up triangle is narrower than it was long, and the face drawn along its length does
    not fit across it as it is. So the marks keep their places to each other as drawn as nearly
    as they can - a little drawn in toward each other across the face, the front eye and the mouth
    let down a step - and, only as far as that is not enough, the face is shown smaller while he
    stands (FACE_SCALES; the rig shows it full size again as he tips over into the drawing). It
    takes the least of that which leaves every mark inside the body's line with FACE_ROOM to
    spare, below the hat's brim (`ceiling`), and FACE_GAP clear of each other, and goes as high
    as it fits, in the middle of the room there. Worked on a copy a quarter the size."""
    room = body.split()[3].point(lambda v: 255 if v > 128 else 0)
    white = body.convert('L').point(lambda v: 255 if v > 200 else 0)
    room = ImageChops.multiply(room, white)
    rw, rh = body.size[0] // SMALL, body.size[1] // SMALL
    room = room.resize((rw, rh), Image.NEAREST)
    room = room.filter(ImageFilter.MinFilter(2 * (FACE_ROOM // SMALL) + 1)).load()
    # The room in each row, from side to side (the body is one run across in every row).
    span = {}
    for y in range(rh):
        xs = [x for x in range(rw) if room[x, y]]
        if xs and y * SMALL - pivot[1] >= ceiling + FACE_ROOM:
            span[y] = (xs[0], xs[-1])
    middle = (sum(f['drawn'][0] for f in face) / len(face), sum(f['drawn'][1] for f in face) / len(face))
    gap = FACE_GAP // SMALL
    ink = []
    for f in face:
        img = Image.open(os.path.join(parts_dir, f['name'] + '.png'))
        alpha = img.split()[3].load()
        pv = parts[f['name']]['pivot']
        ink.append([(x - pv[0], y - pv[1]) for y in range(0, img.size[1], 2) for x in range(0, img.size[0], 2) if alpha[x, y] > 128])

    for scale in FACE_SCALES:
        cells, rows, grown = [], [], []
        for marks in ink:
            mine = set((int(round(x * scale / SMALL)), int(round(y * scale / SMALL))) for (x, y) in marks)
            cells.append(mine)
            by_row = {}
            for (x, y) in mine:
                lo, hi = by_row.get(y, (x, x))
                by_row[y] = (min(lo, x), max(hi, x))
            rows.append(by_row)
            grown.append(set((x + dx, y + dy) for (x, y) in mine for dx in range(-gap, gap + 1) for dy in range(-gap, gap + 1)))

        def placed(closer, drops):
            return [(int(round((f['drawn'][0] - middle[0]) * scale * (1.0 - closer) / SMALL)),
                     int(round(((f['drawn'][1] - middle[1]) * scale + drop) / SMALL))) for f, drop in zip(face, drops)]

        def clear_of_each_other(rel):
            for i in range(len(face)):
                for j in range(i + 1, len(face)):
                    dx, dy = rel[j][0] - rel[i][0], rel[j][1] - rel[i][1]
                    if any((x + dx, y + dy) in grown[i] for (x, y) in cells[j]):
                        return False
            return True

        def room_at(rel, ay):
            """The range of places across the face can go with its middle at row `ay`, or None."""
            lo, hi = -10 ** 6, 10 ** 6
            for i in range(len(face)):
                for y, (x0, x1) in rows[i].items():
                    row = int(ay + rel[i][1] + y + pivot[1] / SMALL)
                    if row not in span:
                        return None
                    left, right = span[row]
                    lo = max(lo, left - (rel[i][0] + x0 + pivot[0] / SMALL))
                    hi = min(hi, right - (rel[i][0] + x1 + pivot[0] / SMALL))
                    if lo > hi:
                        return None
            return lo, hi

        # Least changed first: a tenth drawn in costs as much as a 60px step down.
        tries = [(c / 20.0, (0, b * 20, m * 20)) for c in range(0, 8) for b in range(0, 9) for m in range(0, 9)]
        tries.sort(key=lambda t: t[0] * 10 + (t[1][1] + t[1][2]) / 60.0)
        for closer, drops in tries:
            rel = placed(closer, drops)
            if not clear_of_each_other(rel):
                continue
            for ay in range(int(ceiling // SMALL), 0):
                found = room_at(rel, ay)
                if found:
                    ax = (found[0] + found[1]) / 2.0
                    for f, r in zip(face, rel):
                        f['at'] = [round((ax + r[0]) * SMALL, 1), round((ay + r[1]) * SMALL + FACE_DROP, 1)]
                        f['scale'] = scale
                    print('face: shown at %d%%, drawn in %d%%, front eye down %dpx, mouth down %dpx' % (
                        scale * 100, closer * 100, drops[1], drops[2]))
                    return
    raise SystemExit('his face does not fit on his body')


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

    # His face: the marks drawn inside the triangle that do not touch its outline - two eyes, each
    # a brow with the eye hooked down off its end, and the big mouth. In the drawing all three
    # stand clear of the outline, so they come away whole, without a line cut; they have to, to
    # stay level while the triangle under them is stood on end.
    ink = groups([p for p in body_pts if not is_white(px[p[0], p[1]])])
    ink.sort(key=len, reverse=True)
    marks = [g for g in ink[1:] if len(g) >= FACE_MIN]
    if len(marks) != 3:
        raise SystemExit('expected 2 eyes and a mouth in %s, found %d marks' % (MAIN, len(marks)))
    mouth_mark = marks[0]
    back_eye_mark, front_eye_mark = sorted(marks[1:], key=lambda g: circy.bbox(g)[0])
    face_set = set(mouth_mark) | set(back_eye_mark) | set(front_eye_mark)
    body_pts = [p for p in body_pts if p not in face_set]

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

    # Where the legs leave the body on the drawing: the hip, tipped over.
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

    # --- Stood up -------------------------------------------------------------------------------
    # The triangle in the drawing lies on its side, its point out in front: that is his point
    # attack. Standing, it is on end - point up, hat on the point, legs out of the bottom - as
    # every other drawing of him has it. The body is turned by `tip` to stand it up; turned
    # forward by the same again in a match, it is back exactly as drawn (the rig's "tipped").
    apex = max(body_pts, key=lambda p: (p[0], -p[1]))
    top_back = min(body_pts, key=lambda p: (p[1], p[0]))
    bottom_back = max(body_pts, key=lambda p: p[1] - p[0])
    base = ((top_back[0] + bottom_back[0]) / 2.0, (top_back[1] + bottom_back[1]) / 2.0)
    # Point straight up, or bottom level: his triangle is not even, so it cannot be both.
    point_up = 90.0 + math.degrees(math.atan2(apex[1] - base[1], apex[0] - base[0]))
    base_level = math.degrees(math.atan2(bottom_back[1] - top_back[1], bottom_back[0] - top_back[0]))
    tip = point_up + LEVEL * (base_level - point_up)

    def up(point):
        """A point on the drawing, on the stood-up body: from the middle of its bottom, his hip."""
        v = turn((point[0] - base[0], point[1] - base[1]), tip)
        return (round(v[0], 1), round(v[1], 1))

    # The body, with the face's marks lifted off it and the paper under them filled in white -
    # which the colouring in then takes as body, like the rest of the white.
    body, body_origin = circy.extract(img, body_pts)
    bpx = body.load()
    for (x, y) in face_set:
        bpx[x - body_origin[0], y - body_origin[1]] = (255, 255, 255, 255)
    # And the faint grey at the very edge of each mark, which would show as a ghost of it.
    bw, bh = body.size
    for (x, y) in face_set:
        for dx in range(-2, 3):
            for dy in range(-2, 3):
                q = (x - body_origin[0] + dx, y - body_origin[1] + dy)
                if 0 <= q[0] < bw and 0 <= q[1] < bh and is_white(bpx[q]):
                    bpx[q] = (255, 255, 255, 255)
    body, bpivot = turned(body, (base[0] - body_origin[0], base[1] - body_origin[1]), tip)
    body, bpad = linework.thicken(body, LINE_BOOST)
    body.save(os.path.join(parts_dir, 'Torso.png'))
    parts['Torso'] = {'texture': 'parts/Torso.png', 'pivot': [round(bpivot[0] + bpad, 2), round(bpivot[1] + bpad, 2)]}

    corner_back, corner_front, top = up(top_back), up(bottom_back), up(apex)
    height = -top[1]
    print('stood up %.1f degrees: %d tall, %d across the bottom' % (
        tip, height, corner_front[0] - corner_back[0]))

    def along(a, b, t):
        return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)

    # Legs out of the bottom, either side of the middle, starting a little up inside its line.
    leg_roots = []
    for corner in (corner_back, corner_front):
        x, y = along((0.0, 0.0), corner, LEG_SPREAD)
        leg_roots.append([round(x, 1), round(y - LEG_TUCK, 1)])
    # Arms out of the two sides, as his front-on drawings have them, a little in from the edge.
    arm_roots = []
    for corner, inward in ((corner_back, 1), (corner_front, -1)):
        x, y = along(corner, top, ARM_HEIGHT)
        arm_roots.append([round(x + inward * ARM_TUCK, 1), round(y, 1)])

    # The hat, apart, turned level and sat on the point - how every drawing of him standing wears
    # it. Its pixels are the drawing's; it is only turned.
    hat_raw, hat_origin = circy.extract(img, hat)
    hat_centre = (hat_raw.size[0] / 2.0, hat_raw.size[1] / 2.0)
    hat_level, hat_turn_at = turned(hat_raw, hat_centre, -HAT_LEAN)
    hpx = hat_level.split()[3].load()
    hw, hh = hat_level.size
    brim_rows = [y for y in range(hh) if any(hpx[x, y] > 128 for x in range(0, hw, 2))]
    brim_y = brim_rows[-1]
    brim_xs = [x for y in range(brim_y - hh // 6, brim_y + 1) for x in range(hw) if hpx[x, y] > 128]
    brim_w = max(brim_xs) - min(brim_xs)
    brim = ((max(brim_xs) + min(brim_xs)) / 2.0, float(brim_y))
    # Where that brim is on the drawing: his hat in its place there, as the stab wears it.
    back = turn((brim[0] - hat_turn_at[0], brim[1] - hat_turn_at[1]), HAT_LEAN)
    brim_drawn = (hat_origin[0] + hat_centre[0] + back[0], hat_origin[1] + hat_centre[1] + back[1])
    hat_img, hpad = linework.thicken(hat_level, LINE_BOOST)
    hat_img.save(os.path.join(parts_dir, 'Hat.png'))
    parts['Hat'] = {'texture': 'parts/Hat.png', 'pivot': [round(brim[0] + hpad, 2), round(brim[1] + hpad, 2)]}
    # Sat down over the point until the triangle under the brim is HAT_GRIP of its width.
    across = corner_front[0] - corner_back[0]
    sink = min(0.9, HAT_GRIP * brim_w / across)
    on = along(top, along(corner_back, corner_front, 0.5), sink)
    hat_at = [round(on[0], 1), round(on[1], 1)]
    crown = -(hat_at[1] - brim[1])

    # The face, each mark on its own, level as drawn. Standing, they are set on the body as they
    # sit to each other on the drawing as nearly as fits (fit_face); tipped over into the stab,
    # they go back exactly where they are on the drawing.
    face = []
    for mark, name, kind, tilt in ((back_eye_mark, 'FaceEye', 'eye', 1),
                                   (front_eye_mark, 'FaceEyeFront', 'eye', -1),
                                   (mouth_mark, 'FaceMouth', 'mouth', 1)):
        piece, origin = circy.extract(img, mark)
        x0, y0, x1, y1 = circy.bbox(mark)
        # Eyes turn about their middles; a mouth opens down from its top.
        pin = ((x0 + x1) / 2.0, y0 if kind == 'mouth' else (y0 + y1) / 2.0)
        piece, fpad = linework.thicken(piece, LINE_BOOST)
        piece.save(os.path.join(parts_dir, name + '.png'))
        parts[name] = {'texture': 'parts/%s.png' % name,
                       'pivot': [round(pin[0] - origin[0] + fpad, 1), round(pin[1] - origin[1] + fpad, 1)]}
        face.append({'name': name, 'kind': kind, 'tilt': tilt, 'drawn': pin})
    fit_face(body, (bpivot[0] + bpad, bpivot[1] + bpad), face, parts, parts_dir, hat_at[1])

    bones = [
        {'name': 'Hip', 'parent': None, 'offset': [0, 0], 'part': None},
        {'name': 'LegBack_Upper', 'parent': 'Hip', 'offset': leg_roots[0], 'part': 'LegBack_Upper'},
        {'name': 'LegBack_Lower', 'parent': 'LegBack_Upper', 'offset': [round(knee_dx[0], 1), round(leg_len[0] / 2, 1)], 'part': 'LegBack_Lower'},
        {'name': 'LegFront_Upper', 'parent': 'Hip', 'offset': leg_roots[1], 'part': 'LegFront_Upper'},
        {'name': 'LegFront_Lower', 'parent': 'LegFront_Upper', 'offset': [round(knee_dx[1], 1), round(leg_len[1] / 2, 1)], 'part': 'LegFront_Lower'},
        {'name': 'Torso', 'parent': 'Hip', 'offset': [0, 0], 'part': 'Torso'},
        {'name': 'ArmBack_Upper', 'parent': 'Torso', 'offset': arm_roots[0], 'part': 'ArmBack_Upper'},
        {'name': 'ArmBack_Lower', 'parent': 'ArmBack_Upper', 'offset': [round(elbow_dx[0], 1), round(arm_len[0] / 2, 1)], 'part': 'ArmBack_Lower'},
        {'name': 'Head', 'parent': 'Torso', 'offset': [0, round(-crown, 1)], 'part': None},
        {'name': 'ArmFront_Upper', 'parent': 'Torso', 'offset': arm_roots[1], 'part': 'ArmFront_Upper'},
        {'name': 'ArmFront_Lower', 'parent': 'ArmFront_Upper', 'offset': [round(elbow_dx[1], 1), round(arm_len[1] / 2, 1)], 'part': 'ArmFront_Lower'},
    ]
    extras = [{'name': 'hat', 'bone': 'Torso', 'part': 'Hat', 'offset': hat_at, 'always': True,
               # On the body's drawing, not just its bone, so it turns round with it.
               'onPart': True}]
    for f in face:
        extras.append({'name': 'face' + f['name'][4:], 'bone': 'Torso', 'part': f['name'], 'offset': f['at'],
                       'always': True, 'onPart': True, 'face': f['kind'], 'tilt': f['tilt'],
                       'stretch': MOUTH_STRETCH if f['kind'] == 'mouth' else 1.1, 'scale': f['scale']})

    # Tipped forward by `tip` - his point attack - he is the drawing again: every limb and the hat
    # back where they are on it, the face too, level as drawn. The rig slides each one there as
    # the body tips (FighterRig "tipped"). Legs and body are placed from the hip, as the legs
    # hang from it; arms, hat and face on the body, as they hang from that.
    def drawn_from_hip(point):
        return [round(point[0] - hip[0], 1), round(point[1] - hip[1], 1)]
    tipped = {
        'angle': round(tip, 2),
        'from': TIP_FROM,
        'bones': {
            'Torso': drawn_from_hip(base),
            'LegBack_Upper': drawn_from_hip(back_leg['joint']),
            'LegFront_Upper': drawn_from_hip(front_leg['joint']),
            'ArmBack_Upper': list(up(back_arm['joint'])),
            'ArmFront_Upper': list(up(front_arm['joint'])),
        },
        'extras': {'hat': {'offset': list(up(brim_drawn)), 'rotation': round(-HAT_LEAN - tip, 2)}},
    }
    for f in face:
        tipped['extras']['face' + f['name'][4:]] = {'offset': list(up(f['drawn'])), 'rotation': round(-tip, 2), 'scale': 1.0}

    leg_length = sum(leg_len) / 2.0 - LEG_TUCK
    print('arms %d/%d  legs %d/%d  hip to crown %d  figure %d tall' % (
        arm_len[0], arm_len[1], leg_len[0], leg_len[1], crown, crown + leg_length))
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


def colour_body(img, seed):
    """The one big white shape - his body - coloured in white pencil, deeper toward its edge.
    Smaller whites (an eye, the gaps in a mouth) stay white; nothing that is not white changes."""
    r, g, b, a = img.split()
    white = ImageChops.darker(ImageChops.darker(r, g), b).point(lambda v: 255 if v > 200 else 0)
    white = ImageChops.multiply(white, a.point(lambda v: 255 if v > 128 else 0))
    small = _largest(_small(white))
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
    redo(os.path.join(parts_dir, 'Torso.png'), lambda img: colour_body(img, 'triguy-torso'))
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

    bones, parts, extras, hat_img, tipped, dims = cut_main(parts_dir)
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
