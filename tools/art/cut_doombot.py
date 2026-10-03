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

HIS RIGHT ARM COMES OUT OF THE SOCKET. Unlike a normal far arm it stays in front of his body
("inFront" in rig.json), pivots at the middle of the black socket, and the socket itself is cut
out as a small extra part drawn over the top of the arm - so wherever the arm swings, its root
disappears into the hole.
"""

from __future__ import division, print_function

import json
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

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
BACK_UPPER = [(222, 698), (606, 698), (606, 820), (222, 822)]
BACK_LOWER = [(222, 698), (378, 702), (378, 962), (436, 950), (436, 1166), (96, 1166), (96, 950),
              (204, 958), (204, 820), (222, 820)]
# His left arm: out from his side to the elbow block, then the forearm UP to the claw.
FRONT_UPPER = [(916, 708), (1244, 700), (1244, 816), (916, 812)]
FRONT_LOWER = [(1122, 700), (1248, 700), (1248, 818), (1122, 818), (1122, 710), (1132, 710),
               (1132, 580), (1066, 584), (1066, 416), (1312, 412), (1312, 584), (1240, 580),
               (1240, 710), (1122, 710)]

BACK_LEG = [(512, 1340), (664, 1340), (644, 1880), (778, 1876), (774, 2012), (428, 2012),
            (424, 1876), (486, 1880)]
FRONT_LEG = [(860, 1340), (994, 1340), (992, 1856), (1186, 1850), (1186, 1988), (846, 1988),
             (850, 1856)]

# Joints, in the same pixels.
HIP = (722, 1354)
NECK = (695, 642)
BACK_SHOULDER = (622, 758)
BACK_ELBOW = (296, 760)
BACK_HAND = (268, 1150)
FRONT_SHOULDER = (928, 760)
FRONT_ELBOW = (1182, 758)
FRONT_HAND = (1188, 428)
BACK_HIP = (586, 1354)
BACK_FOOT = (560, 1884)
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
ANTENNA_BACK = (624, 219)
ANTENNA_FRONT = (879, 233)
# Where the grey comes from: the clean band of body between the white glint under the socket
# and the panel line across his middle. It is shorter than the patch, so the patch takes two
# copies of it, stacked: (first row of the patch that uses it, how far below to copy from).
CLONE_FROM_BELOW = [(694, 146), (758, 86)]

JOINT_OVERLAP = 12


def poly_mask(size, polygon, grow=0):
    mask = Image.new('L', size, 0)
    ImageDraw.Draw(mask).polygon(polygon, fill=255)
    if grow:
        mask = mask.filter(ImageFilter.MaxFilter(grow * 2 + 1))
    return mask


def is_paper(p):
    r, g, b = p[:3]
    return (r + g + b) / 3.0 > 150 and max(r, g, b) - min(r, g, b) < 42


def cut(photo, polygon):
    """The photo inside the shape, with the paper around the drawing made transparent."""
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
    piece = crop.convert('RGBA')
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
    ImageDraw.Draw(torso).line(BODY_EDGE, fill=(46, 40, 52, 255), width=5)
    return torso


def canonical(piece, joint, end, name_upper, name_lower, parts_dir, parts, split=0.5):
    """
    Rotates a limb about its joint until it hangs straight down, then cuts it in two a little
    past `split` of the way from the joint to `end` - upper above, lower below, overlapping at
    the cut so no gap opens when the joint bends. With no name_lower the whole limb is one part.
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
    parts = {}

    # The body: the torso, with the arm drawn over it cut away and the patch filled.
    torso = cut(photo, TORSO)
    arm_over_body = ImageChops.multiply(poly_mask(WORK, COVERED), poly_mask(WORK, TORSO))
    torso = fill_covered(torso, photo)
    tbox = torso.getbbox()
    torso.crop(tbox).save(os.path.join(parts_dir, 'Torso.png'))

    # The socket, lifted out of the finished body (rim and all) to sit over the arm's root.
    (cx, cy), (rx, ry) = SOCKET
    hole = Image.new('L', WORK, 0)
    ImageDraw.Draw(hole).ellipse([cx - rx - 7, cy - ry - 7, cx + rx + 7, cy + ry + 7], fill=255)
    socket = Image.new('RGBA', WORK, (0, 0, 0, 0))
    socket.paste(torso, (0, 0), ImageChops.multiply(hole, torso.split()[3]))
    sbox = socket.getbbox()
    socket.crop(sbox).save(os.path.join(parts_dir, 'Socket.png'))
    parts['Socket'] = {'texture': 'parts/Socket.png', 'pivot': [HIP[0] - sbox[0], HIP[1] - sbox[1]]}
    parts['Torso'] = {'texture': 'parts/Torso.png', 'pivot': [HIP[0] - tbox[0], HIP[1] - tbox[1]]}

    head = cut(photo, HEAD)
    hbox = head.getbbox()
    head.crop(hbox).save(os.path.join(parts_dir, 'Head.png'))
    parts['Head'] = {'texture': 'parts/Head.png', 'pivot': [NECK[0] - hbox[0], NECK[1] - hbox[1]]}

    # Arms: each half cut from its own shape, so the elbow block is in both and covers the joint.
    upper_back = canonical(cut(photo, BACK_UPPER), BACK_SHOULDER, BACK_ELBOW, 'ArmBack_Upper', None, parts_dir, parts)
    canonical(cut(photo, BACK_LOWER), BACK_ELBOW, BACK_HAND, 'ArmBack_Lower', None, parts_dir, parts)
    upper_front = canonical(cut(photo, FRONT_UPPER), FRONT_SHOULDER, FRONT_ELBOW, 'ArmFront_Upper', None, parts_dir, parts)
    canonical(cut(photo, FRONT_LOWER), FRONT_ELBOW, FRONT_HAND, 'ArmFront_Lower', None, parts_dir, parts)

    # Legs: one straight piece each, halved - there is no knee drawn, so the middle is the knee.
    knee_back = canonical(cut(photo, BACK_LEG), BACK_HIP, BACK_FOOT, 'LegBack_Upper', 'LegBack_Lower', parts_dir, parts)
    knee_front = canonical(cut(photo, FRONT_LEG), FRONT_HIP, FRONT_FOOT, 'LegFront_Upper', 'LegFront_Lower', parts_dir, parts)

    def rel(p):
        return [p[0] - HIP[0], p[1] - HIP[1]]

    bones = [
        {'name': 'Hip', 'parent': None, 'offset': [0, 0], 'part': None},
        {'name': 'LegBack_Upper', 'parent': 'Hip', 'offset': rel(BACK_HIP), 'part': 'LegBack_Upper'},
        {'name': 'LegBack_Lower', 'parent': 'LegBack_Upper', 'offset': [0, round(knee_back, 1)], 'part': 'LegBack_Lower'},
        {'name': 'LegFront_Upper', 'parent': 'Hip', 'offset': rel(FRONT_HIP), 'part': 'LegFront_Upper'},
        {'name': 'LegFront_Lower', 'parent': 'LegFront_Upper', 'offset': [0, round(knee_front, 1)], 'part': 'LegFront_Lower'},
        {'name': 'Torso', 'parent': 'Hip', 'offset': [0, 0], 'part': 'Torso'},
        {'name': 'ArmBack_Upper', 'parent': 'Torso', 'offset': rel(BACK_SHOULDER), 'part': 'ArmBack_Upper', 'inFront': True},
        {'name': 'ArmBack_Lower', 'parent': 'ArmBack_Upper', 'offset': [0, round(upper_back, 1)], 'part': 'ArmBack_Lower', 'inFront': True},
        {'name': 'Head', 'parent': 'Torso', 'offset': rel(NECK), 'part': 'Head'},
        {'name': 'ArmFront_Upper', 'parent': 'Torso', 'offset': rel(FRONT_SHOULDER), 'part': 'ArmFront_Upper'},
        {'name': 'ArmFront_Lower', 'parent': 'ArmFront_Upper', 'offset': [0, round(upper_front, 1)], 'part': 'ArmFront_Lower'},
    ]
    foot = max(BACK_LEG + FRONT_LEG, key=lambda p: p[1])[1]
    rig = {
        'name': 'DoomBot',
        'canonicalHeight': HIP[1] - hbox[1],
        'legLength': foot - HIP[1],
        'backLimbDarken': 0.85,
        'bones': bones,
        'parts': parts,
        # The socket over the arm that comes out of it. On the Torso bone, whose extras draw
        # after its limbs; its pivot is the hip, so offset zero puts it back where it was drawn.
        'extras': [{'name': 'socket', 'bone': 'Torso', 'part': 'Socket', 'offset': [0, 0], 'always': True}],
        'points': [
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
