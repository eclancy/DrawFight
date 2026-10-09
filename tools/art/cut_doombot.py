# -*- coding: utf-8 -*-
"""
Cuts DoomBot out of the photo of his drawing. Run with the only Python on this machine:

    python tools/art/cut_doombot.py

Reads  fighters/doombot/source/doombot.jpg   (the photo: the master copy, never edited)
Writes fighters/doombot/parts/*.png          the head, body and four limbs, in canonical orientation
       fighters/doombot/rig.json             bone tree, offsets and pivots

The drawing is a boxy robot on a photographed sheet of paper, facing the viewer. Every part is
a traced shape (coordinates below, in pixels of the photo at half size) with the paper removed:
paper is whatever pale, grey-ish colour can be reached from outside the shape without crossing
his outline, so the cut follows his line exactly and never trims it.

ONE THING IS FILLED IN, because a rig needs it. His right arm (on the left of the page) is drawn
across the front of his body, coming out of the black socket on his chest. Cut free, that arm
leaves a hole in the body where nothing was drawn. The hole is filled with grey cloned from the
body just below it, and his body's outline is continued down through it - filling in what is
missing, never replacing what is there.

He faces the viewer, so, as with Circy, the limbs on the right of the page are the front ones.

HIS RIGHT ARM IS THE FRONT ARM. It is drawn coming out of the black socket on his chest, so it
is the near arm, in front of his body, and his left arm - out from his side on the right of the
page - is the far one, behind his body and darkened a little. (Both once drew in front, which
read as two front arms; then the wrong one went behind. Eric's call, 2026-10-04.) The socket is
lifted out of the body as a small extra part drawn over the arm's root, so wherever the arm
swings it comes out of the hole.

HIS BOOTS ARE FEET. Each boot is cut as its own piece, hung on the end of its shin at the ankle
("foot" extras in rig.json), so the game can keep it flat on the floor while the knee bends -
and he stands balanced over his feet instead of leaning on tilted shins. And each leg has a round
cap at the hip and the knee, filled with leg cloned from just below the hip (it sits behind the
body at rest), so a leg swung far out never shows a gap at the joint. Eric's call, 2026-10-04.

HIS OUTLINE IS THICKENED. Every piece gets a dark ring round its outside edge, so he reads
clearly against any stage (Eric's call, 2026-10-04). It is drawn round each whole shape before
it is cut at the elbow or knee, so no line appears across a joint, and never round the thin
antennae, which it would turn into poles.

THE COLOURS ARE LEVELLED. The photo is a pencil drawing under room light, and he came out dull
and grey next to everyone else. The cut pieces take their colour from a levelled copy of the
photo - black point, white point and a lift to the mid-greys, and a little more colour - which
is the "levels nudge so pencil is visible" the art rules allow (.ai/art-pipeline.md), never a
restyle. The paper is still found on the photo as taken, so the cut itself does not change.
"""

from __future__ import division, print_function

import json
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
ROOT = os.path.join(REPO, 'fighters', 'doombot')
PHOTO = os.path.join(ROOT, 'source', 'doombot.jpg')

# The photo is worked on at half size: plenty of detail for a fighter on screen, and a quarter
# of the pixels for Python 2.7 without numpy to walk.
WORK = (1536, 2040)

# --- The traced shapes ----------------------------------------------------------------------

# Head, antennae and the short neck. The bottom follows the head's own bottom line, so the top
# of the body just under it stays with the body.
HEAD = [(592, 190), (918, 196), (952, 340), (944, 636), (762, 628), (762, 646), (632, 646),
        (632, 624), (518, 614), (540, 318)]
TORSO = [(492, 622), (932, 634), (990, 1360), (452, 1358)]

# His right arm, drawn across his body: the upper arm runs from the socket on his chest out to
# the elbow block; the forearm hangs from the block down to the claw.
RIGHT_UPPER = [(222, 698), (606, 698), (606, 820), (222, 822)]
RIGHT_LOWER = [(222, 698), (378, 702), (378, 962), (436, 950), (436, 1166), (96, 1166), (96, 950),
              (204, 958), (204, 820), (222, 820)]
# His left arm: out from his side to the elbow block, then the forearm UP to the claw.
LEFT_UPPER = [(916, 708), (1244, 700), (1244, 816), (916, 812)]
LEFT_LOWER = [(1122, 700), (1248, 700), (1248, 818), (1122, 818), (1122, 710), (1132, 710),
               (1132, 580), (1066, 584), (1066, 416), (1312, 412), (1312, 584), (1240, 580),
               (1240, 710), (1122, 710)]

# Each leg is a shin, from under the body down into the top of its boot (the boot covers the
# overlap), and a boot.
BACK_LEG = [(508, 1340), (666, 1340), (650, 1898), (482, 1898)]
BACK_BOOT = [(422, 1882), (782, 1882), (778, 2014), (426, 2014)]
FRONT_LEG = [(858, 1340), (996, 1340), (994, 1878), (852, 1878)]
FRONT_BOOT = [(842, 1861), (1190, 1861), (1190, 1992), (842, 1992)]

# Where the drawing of each leg is plain leg, all the way across - cloned up into the hip cap.
# Above it the body's bottom outline runs across the top of the leg.
LEG_CLEAN_FROM = 1364
LEG_CLONE_ROWS = 24

# Joints, in the same pixels.
HIP = (722, 1354)
NECK = (695, 642)
RIGHT_SHOULDER = (622, 758)
RIGHT_ELBOW = (296, 760)
RIGHT_HAND = (268, 1150)
LEFT_SHOULDER = (928, 760)
LEFT_ELBOW = (1182, 758)
LEFT_HAND = (1188, 428)
BACK_HIP = (586, 1354)
BACK_FOOT = (560, 1884)     # the ankle: where the shin meets the top of the boot
FRONT_HIP = (926, 1354)
FRONT_FOOT = (930, 1866)

# The covered patch of body behind his right arm, the black socket the arm comes out of (left
# alone), and where his body's left outline runs behind the arm.
COVERED = [(470, 694), (606, 694), (606, 822), (470, 822)]
SOCKET = ((630, 771), (52, 92))
BODY_EDGE = [(491, 690), (489, 826)]

# Named spots on the drawing that moves come out of, in the same pixels: his round grille eye
# fires the eye laser, and the tips of his antennae throw the up smash's lightning. Written into
# rig.json relative to the head's joint, so they move with his head.
EYE = (831, 464)

# Where each claw begins on its forearm, in the same pixels: the stretched-arm grab stretches the
# arm above this and leaves the claw below it its own size. Written into rig.json as the texture
# row of the lower arm where the hand starts ("hand").
LEFT_CLAW_START = (1188, 584)
RIGHT_CLAW_START = (268, 950)
ANTENNA_BACK = (624, 219)
ANTENNA_FRONT = (879, 233)
# Where the grey comes from: the clean band of body between the white glint under the socket
# and the panel line across his middle. It is shorter than the patch, so the patch takes two
# copies of it, stacked: (first row of the patch that uses it, how far below to copy from).
CLONE_FROM_BELOW = [(694, 146), (758, 86)]

JOINT_OVERLAP = 12

# The thickened outline: how many pixels (at WORK size) of dark ring go round each piece - about
# two pixels on screen - and the head's ring stops above this row, where the antennae begin.
OUTLINE = 12
OUTLINE_INK = (26, 22, 30, 255)
ANTENNA_BASE_Y = 330


# The levels: the darkest pencil (about 16) and the brightest highlights (about 197) stretched
# to the full range, the mid-greys lifted, and the reds a little stronger. Eric's call, 2026-10-04.
LEVELS_BLACK = 18
LEVELS_WHITE = 200
LEVELS_GAMMA = 0.85
SATURATION = 1.2


def level(v):
    t = max(0.0, min(1.0, (v - LEVELS_BLACK) / float(LEVELS_WHITE - LEVELS_BLACK)))
    return int(round(255.0 * t ** LEVELS_GAMMA))


def levelled(photo):
    """The photo brightened and given more contrast and colour - only ever used for the colour
    of the cut pieces, never to decide what is paper."""
    lut = [level(v) for v in range(256)]
    return ImageEnhance.Color(photo.point(lut * 3)).enhance(SATURATION)


def poly_mask(size, polygon, grow=0):
    mask = Image.new('L', size, 0)
    ImageDraw.Draw(mask).polygon(polygon, fill=255)
    if grow:
        mask = mask.filter(ImageFilter.MaxFilter(grow * 2 + 1))
    return mask


def is_paper(p):
    r, g, b = p[:3]
    return (r + g + b) / 3.0 > 150 and max(r, g, b) - min(r, g, b) < 42


def cut(photo, polygon, look=None):
    """The photo inside the shape, with the paper around the drawing made transparent. The paper
    is found on `photo`; the colours come from `look` (the levelled photo) when given."""
    size = photo.size
    shape = poly_mask(size, polygon, grow=3)
    x0, y0, x1, y1 = shape.getbbox()
    x0, y0, x1, y1 = max(0, x0 - 4), max(0, y0 - 4), min(size[0], x1 + 4), min(size[1], y1 + 4)
    crop = photo.crop((x0, y0, x1, y1))
    shape = shape.crop((x0, y0, x1, y1))

    # Paper and everything outside the shape are one colour, his lines and colouring another;
    # a flood from the corner then takes the paper touching the outside, and only that.
    marks = Image.new('L', crop.size, 0)
    src, dst, inside = crop.load(), marks.load(), shape.load()
    w, h = crop.size
    for y in range(h):
        for x in range(w):
            if not inside[x, y] or is_paper(src[x, y]):
                dst[x, y] = 255
    ImageDraw.floodfill(marks, (0, 0), 100)
    keep = marks.point(lambda v: 0 if v == 100 else 255)
    keep = ImageChops.multiply(keep, shape)

    out = Image.new('RGBA', size, (0, 0, 0, 0))
    piece = (look.crop((x0, y0, x1, y1)) if look is not None else crop).convert('RGBA')
    piece.putalpha(keep)
    out.paste(piece, (x0, y0))
    return out


def fill_covered(torso, photo):
    """Fills the patch of body his right arm was drawn over, and runs the body's outline through it."""
    size = torso.size
    patch = poly_mask(size, COVERED)
    (cx, cy), (rx, ry) = SOCKET
    socket = Image.new('L', size, 0)
    ImageDraw.Draw(socket).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
    patch = ImageChops.subtract(patch, socket)
    patch = ImageChops.multiply(patch, poly_mask(size, TORSO, grow=3))

    # Outside the body's own edge the arm is simply gone: nothing was drawn there.
    (ex0, ey0), (ex1, ey1) = BODY_EDGE
    outside = Image.new('L', size, 0)
    ImageDraw.Draw(outside).polygon([(0, ey0), (ex0 - 1, ey0), (ex1 - 1, ey1), (0, ey1)], fill=255)
    outside = ImageChops.multiply(outside, poly_mask(size, COVERED))
    torso.paste((0, 0, 0, 0), (0, 0), outside)
    patch = ImageChops.subtract(patch, outside)

    # Grey pencil from below the arm, where the same shading carries on, faded in at the edges
    # of the patch so there is no hard seam.
    shifted = Image.new('RGBA', size, (0, 0, 0, 0))
    for start, offset in CLONE_FROM_BELOW:
        # Begun a few rows early, so the faded edge above the patch has grey to fade into.
        start -= 12
        rows = photo.crop((0, start + offset, size[0], size[1])).convert('RGBA')
        shifted.paste(rows, (0, start))
    soft = patch.filter(ImageFilter.GaussianBlur(3))
    soft = ImageChops.multiply(soft, poly_mask(size, TORSO))
    torso.paste(shifted, (0, 0), ImageChops.lighter(ImageChops.multiply(soft, patch), soft))
    torso.putalpha(ImageChops.lighter(torso.split()[3], patch))

    # And the body's left outline, carried on down behind the arm.
    ImageDraw.Draw(torso).line(BODY_EDGE, fill=(level(46), level(40), level(52), 255), width=5)
    return torso


def ringed(piece, below=0):
    """The piece with a dark ring OUTLINE pixels wide round its outside edge - drawn behind it,
    so nothing of the drawing is covered - and none above row `below`. The pen is round: the
    shape is grown a pixel at a time, alternating a square step and a plus-shaped one."""
    shape = piece.split()[3].point(lambda v: 255 if v > 100 else 0)
    grown = shape
    for step in range(OUTLINE):
        if step % 2 == 0:
            grown = grown.filter(ImageFilter.MaxFilter(3))
        else:
            grown = ImageChops.lighter(
                ImageChops.lighter(grown, ImageChops.offset(grown, 1, 0)),
                ImageChops.lighter(ImageChops.lighter(ImageChops.offset(grown, -1, 0), ImageChops.offset(grown, 0, 1)),
                                   ImageChops.offset(grown, 0, -1)))
    if below:
        grown.paste(0, (0, 0, grown.size[0], below))
        grown = ImageChops.lighter(grown, shape)
    # Pasted rather than given an alpha, so the paper round it stays fully empty - colour left
    # under a zero alpha still counts towards a part's bounding box.
    ring = Image.new('RGBA', piece.size, (0, 0, 0, 0))
    ring.paste(OUTLINE_INK, (0, 0), grown)
    out = Image.alpha_composite(ring, piece)
    out.putalpha(out.split()[3].point(lambda v: v if v > 8 else 0))
    empty = Image.new('RGBA', piece.size, (0, 0, 0, 0))
    return Image.composite(out, empty, out.split()[3])


def hand_row(parts, name, joint, end, start):
    """Marks where the hand begins on a limb part hanging from `joint` toward `end`: the distance
    from the joint to `start`, measured along the limb, below the part's pivot."""
    length = math.hypot(end[0] - joint[0], end[1] - joint[1])
    along = ((start[0] - joint[0]) * (end[0] - joint[0]) + (start[1] - joint[1]) * (end[1] - joint[1])) / length
    parts[name]['hand'] = round(parts[name]['pivot'][1] + along, 1)


def capped_leg(photo, look, polygon, hip):
    """
    A leg with a round cap over its hip: the plain leg below the body's outline cloned upward
    (mirrored band by band, so there is no seam) to fill a disc round the hip joint. At rest the
    cap is hidden behind the body; swung out, it is the rounded top of the thigh.
    """
    leg = cut(photo, polygon, look)
    left = min(x for x, y in polygon[:2])
    right = max(x for x, y in polygon[:2])
    radius = (right - left) / 2.0
    band = leg.crop((0, LEG_CLEAN_FROM, WORK[0], LEG_CLEAN_FROM + LEG_CLONE_ROWS))
    fill = Image.new('RGBA', WORK, (0, 0, 0, 0))
    y, flip = LEG_CLEAN_FROM - LEG_CLONE_ROWS, True
    while y > hip[1] - radius - LEG_CLONE_ROWS:
        fill.paste(band.transpose(Image.FLIP_TOP_BOTTOM) if flip else band, (0, y))
        y, flip = y - LEG_CLONE_ROWS, not flip
    cap = Image.new('L', WORK, 0)
    draw = ImageDraw.Draw(cap)
    draw.ellipse([hip[0] - radius, hip[1] - radius, hip[0] + radius, hip[1] + radius], fill=255)
    draw.rectangle([left, hip[1], right, LEG_CLEAN_FROM], fill=255)
    cap = ImageChops.multiply(cap, fill.split()[3].point(lambda v: 255 if v > 100 else 0))
    fill.putalpha(cap)
    # The drawn leg below the clean row; the cloned cap above it.
    below = leg.copy()
    below.paste((0, 0, 0, 0), (0, 0, WORK[0], LEG_CLEAN_FROM))
    return Image.alpha_composite(fill, below), radius


def boot(photo, look, polygon, ankle, name, parts_dir, parts):
    """A boot, as drawn - level - pinned at its ankle."""
    piece = ringed(cut(photo, polygon, look))
    box = piece.getbbox()
    piece.crop(box).save(os.path.join(parts_dir, name + '.png'))
    parts[name] = {'texture': 'parts/%s.png' % name, 'pivot': [ankle[0] - box[0], ankle[1] - box[1]]}


def canonical(piece, joint, end, name_upper, name_lower, parts_dir, parts, split=0.5, cap=0.0):
    """
    Rotates a limb about its joint until it hangs straight down, then cuts it in two a little
    past `split` of the way from the joint to `end` - upper above, lower below, overlapping at
    the cut so no gap opens when the joint bends. With no name_lower the whole limb is one part.
    With a `cap` radius, both halves also keep a disc of the limb round the joint, so bending it
    turns one round end over another instead of opening a notch.
    """
    length = math.hypot(end[0] - joint[0], end[1] - joint[1])
    box = piece.getbbox()
    crop = piece.crop(box)
    jx, jy = joint[0] - box[0], joint[1] - box[1]
    side = int(max(crop.size) * 2.4 + 80)
    canvas = Image.new('RGBA', (side, side), (0, 0, 0, 0))
    canvas.paste(crop, (int(side / 2 - jx), int(side / 2 - jy)))
    angle = math.degrees(math.atan2(end[1] - joint[1], end[0] - joint[0]))
    canvas = canvas.rotate(angle - 90.0, resample=Image.BICUBIC, center=(side / 2, side / 2))

    def emit(name, image, top):
        b = image.getbbox()
        image.crop(b).save(os.path.join(parts_dir, name + '.png'))
        parts[name] = {'texture': 'parts/%s.png' % name,
                       'pivot': [round(side / 2 - b[0], 1), round(top - b[1], 1)]}

    if name_lower is None:
        emit(name_upper, canvas, side / 2)
        return length

    mid = side / 2 + length * split
    if cap > 0.0:
        disc = Image.new('L', (side, side), 0)
        ImageDraw.Draw(disc).ellipse([side / 2 - cap, mid - cap, side / 2 + cap, mid + cap], fill=255)
        halves = []
        for box in ((0, 0, side, int(mid)), (0, int(mid), side, side)):
            keep = disc.copy()
            keep.paste(255, box)
            # Composited onto empty rather than given an alpha, so nothing outside the shape
            # counts towards the part's bounding box.
            alpha = ImageChops.multiply(canvas.split()[3], keep)
            halves.append(Image.composite(canvas, Image.new('RGBA', canvas.size, (0, 0, 0, 0)), alpha))
        emit(name_upper, halves[0], side / 2)
        emit(name_lower, halves[1], mid)
        return length * split
    upper = canvas.crop((0, 0, side, int(mid + JOINT_OVERLAP)))
    lower = canvas.crop((0, int(mid - JOINT_OVERLAP), side, side))
    emit(name_upper, upper, side / 2)
    emit(name_lower, lower, mid - int(mid - JOINT_OVERLAP))
    return length * split


def main():
    parts_dir = os.path.join(ROOT, 'parts')
    if not os.path.isdir(parts_dir):
        os.makedirs(parts_dir)

    photo = Image.open(PHOTO).convert('RGB').resize(WORK, Image.LANCZOS)
    look = levelled(photo)
    parts = {}

    # The body: the torso, with the arm drawn over it cut away and the patch filled.
    torso = cut(photo, TORSO, look)
    arm_over_body = ImageChops.multiply(poly_mask(WORK, COVERED), poly_mask(WORK, TORSO))
    torso = fill_covered(torso, look)

    # The socket, lifted out of the finished body (rim and all) to sit over the front arm's root.
    (cx, cy), (rx, ry) = SOCKET
    hole = Image.new('L', WORK, 0)
    ImageDraw.Draw(hole).ellipse([cx - rx - 7, cy - ry - 7, cx + rx + 7, cy + ry + 7], fill=255)
    socket = Image.new('RGBA', WORK, (0, 0, 0, 0))
    socket.paste(torso, (0, 0), ImageChops.multiply(hole, torso.split()[3]))
    sbox = socket.getbbox()
    socket.crop(sbox).save(os.path.join(parts_dir, 'Socket.png'))
    parts['Socket'] = {'texture': 'parts/Socket.png', 'pivot': [HIP[0] - sbox[0], HIP[1] - sbox[1]]}

    torso = ringed(torso)
    tbox = torso.getbbox()
    torso.crop(tbox).save(os.path.join(parts_dir, 'Torso.png'))
    parts['Torso'] = {'texture': 'parts/Torso.png', 'pivot': [HIP[0] - tbox[0], HIP[1] - tbox[1]]}

    head = ringed(cut(photo, HEAD, look), below=ANTENNA_BASE_Y)
    hbox = head.getbbox()
    head.crop(hbox).save(os.path.join(parts_dir, 'Head.png'))
    parts['Head'] = {'texture': 'parts/Head.png', 'pivot': [NECK[0] - hbox[0], NECK[1] - hbox[1]]}

    # Arms: each half cut from its own shape, so the elbow block is in both and covers the joint.
    # His right arm, out of the socket, is the front one; his left arm the back one.
    upper_front = canonical(ringed(cut(photo, RIGHT_UPPER, look)), RIGHT_SHOULDER, RIGHT_ELBOW, 'ArmFront_Upper', None, parts_dir, parts)
    canonical(ringed(cut(photo, RIGHT_LOWER, look)), RIGHT_ELBOW, RIGHT_HAND, 'ArmFront_Lower', None, parts_dir, parts)
    upper_back = canonical(ringed(cut(photo, LEFT_UPPER, look)), LEFT_SHOULDER, LEFT_ELBOW, 'ArmBack_Upper', None, parts_dir, parts)
    canonical(ringed(cut(photo, LEFT_LOWER, look)), LEFT_ELBOW, LEFT_HAND, 'ArmBack_Lower', None, parts_dir, parts)
    hand_row(parts, 'ArmFront_Lower', RIGHT_ELBOW, RIGHT_HAND, RIGHT_CLAW_START)
    hand_row(parts, 'ArmBack_Lower', LEFT_ELBOW, LEFT_HAND, LEFT_CLAW_START)

    # Legs: one straight piece each, halved - there is no knee drawn, so the middle is the knee.
    # Ringed whole, before the halving, so no line runs across the knee; capped round at the
    # hip and the knee; and the boots cut off as feet.
    leg, cap = capped_leg(photo, look, BACK_LEG, BACK_HIP)
    knee_back = canonical(ringed(leg), BACK_HIP, BACK_FOOT, 'LegBack_Upper', 'LegBack_Lower', parts_dir, parts, cap=cap)
    leg, cap = capped_leg(photo, look, FRONT_LEG, FRONT_HIP)
    knee_front = canonical(ringed(leg), FRONT_HIP, FRONT_FOOT, 'LegFront_Upper', 'LegFront_Lower', parts_dir, parts, cap=cap)
    boot(photo, look, BACK_BOOT, BACK_FOOT, 'FootBack', parts_dir, parts)
    boot(photo, look, FRONT_BOOT, FRONT_FOOT, 'FootFront', parts_dir, parts)
    shin_back = math.hypot(BACK_FOOT[0] - BACK_HIP[0], BACK_FOOT[1] - BACK_HIP[1]) - knee_back
    shin_front = math.hypot(FRONT_FOOT[0] - FRONT_HIP[0], FRONT_FOOT[1] - FRONT_HIP[1]) - knee_front

    def rel(p):
        return [p[0] - HIP[0], p[1] - HIP[1]]

    bones = [
        {'name': 'Hip', 'parent': None, 'offset': [0, 0], 'part': None},
        {'name': 'LegBack_Upper', 'parent': 'Hip', 'offset': rel(BACK_HIP), 'part': 'LegBack_Upper'},
        {'name': 'LegBack_Lower', 'parent': 'LegBack_Upper', 'offset': [0, round(knee_back, 1)], 'part': 'LegBack_Lower'},
        {'name': 'LegFront_Upper', 'parent': 'Hip', 'offset': rel(FRONT_HIP), 'part': 'LegFront_Upper'},
        {'name': 'LegFront_Lower', 'parent': 'LegFront_Upper', 'offset': [0, round(knee_front, 1)], 'part': 'LegFront_Lower'},
        {'name': 'Torso', 'parent': 'Hip', 'offset': [0, 0], 'part': 'Torso'},
        {'name': 'ArmBack_Upper', 'parent': 'Torso', 'offset': rel(LEFT_SHOULDER), 'part': 'ArmBack_Upper'},
        {'name': 'ArmBack_Lower', 'parent': 'ArmBack_Upper', 'offset': [0, round(upper_back, 1)], 'part': 'ArmBack_Lower'},
        {'name': 'Head', 'parent': 'Torso', 'offset': rel(NECK), 'part': 'Head'},
        {'name': 'ArmFront_Upper', 'parent': 'Torso', 'offset': rel(RIGHT_SHOULDER), 'part': 'ArmFront_Upper'},
        {'name': 'ArmFront_Lower', 'parent': 'ArmFront_Upper', 'offset': [0, round(upper_front, 1)], 'part': 'ArmFront_Lower'},
    ]
    foot = max(BACK_BOOT + FRONT_BOOT, key=lambda p: p[1])[1]
    rig = {
        'name': 'DoomBot',
        'canonicalHeight': HIP[1] - hbox[1],
        'legLength': foot - HIP[1],
        'backLimbDarken': 0.85,
        'bones': bones,
        'parts': parts,
        # The socket over the front arm that comes out of it. On the Torso bone, whose extras draw
        # after its limbs; its pivot is the hip, so offset zero puts it back where it was drawn.
        # The boots, on the ends of the shins at the ankles: "foot" extras the game keeps level.
        'extras': [
            {'name': 'socket', 'bone': 'Torso', 'part': 'Socket', 'offset': [0, 0], 'always': True},
            {'name': 'footBack', 'bone': 'LegBack_Lower', 'part': 'FootBack', 'offset': [0, round(shin_back, 1)],
             'always': True, 'foot': True},
            {'name': 'footFront', 'bone': 'LegFront_Lower', 'part': 'FootFront', 'offset': [0, round(shin_front, 1)],
             'always': True, 'foot': True},
        ],
        'points': [
            # The socket on his chest, which the pocket missile comes out of.
            {'name': 'socket', 'bone': 'Torso', 'offset': [SOCKET[0][0] - HIP[0], SOCKET[0][1] - HIP[1]]},
            {'name': 'eye', 'bone': 'Head', 'offset': [EYE[0] - NECK[0], EYE[1] - NECK[1]]},
            {'name': 'antenna_back', 'bone': 'Head', 'offset': [ANTENNA_BACK[0] - NECK[0], ANTENNA_BACK[1] - NECK[1]]},
            {'name': 'antenna_front', 'bone': 'Head', 'offset': [ANTENNA_FRONT[0] - NECK[0], ANTENNA_FRONT[1] - NECK[1]]},
        ],
    }
    with open(os.path.join(ROOT, 'rig.json'), 'wb') as fh:
        fh.write(json.dumps(rig, indent=2, sort_keys=True).encode('utf-8'))
    print('head %s  torso %s  covered patch %d px' % (hbox, tbox, sum(arm_over_body.histogram()[128:])))


if __name__ == '__main__':
    main()
