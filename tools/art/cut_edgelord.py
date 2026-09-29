# -*- coding: utf-8 -*-
"""
Cuts EdgeLord into a rig. Run with the only Python on this machine:

    python tools/art/cut_edgelord.py

PLACEHOLDER ART. The colouring this cuts is ours (tools/art/color_edgelord.py), and so are the
swords and the turning frames (tools/art/edgelord_extras.py). The coloured drawing and all of
his move art are being redrawn by hand; when they arrive they replace everything this script
makes. The shapes it cuts along are traced from THIS drawing, so the new art will want its own
cut - ideally as layered PNGs, one part per layer, per .ai/art-pipeline.md.

Emits:
    fighters/edgelord/parts/*.png   the body, the four limbs and the sword in his hand
    fighters/edgelord/poses/*.png   the thrown and planted swords, and the turning frames
    fighters/edgelord/rig.json      bone tree, offsets, pivots, extras and poses

HOW HE IS CUT. The same way as Circy (tools/art/cut_circy.py): the V is the Torso, face and all,
with no separate head; each limb is rotated about its joint until it hangs straight down and
split in half. He is drawn facing the viewer, so the limbs on the right of the page are the
front ones. The turning frames hang on the Torso as extras, shown one at a time when he spins.
"""

from __future__ import division, print_function

import json
import math
import os

from PIL import Image, ImageChops, ImageFilter

import color_edgelord as ce
import edgelord_extras as ex

ROOT = os.path.join(ce.REPO, 'fighters', 'edgelord')
JOINT_OVERLAP = 10

# Where each arm meets the body is the arm's pixel nearest the middle of the V.
BODY_CENTRE = (575 - ce.CROP[0], 640 - ce.CROP[1])
HIP = (ce.HIP[0] - ce.CROP[0], ce.HIP[1] - ce.CROP[1])

# The sword he holds for his normal attacks, and the ones he throws.
HELD_SWORD = 'longsword'


def masked(image, mask):
    out = image.copy()
    out.putalpha(ImageChops.multiply(image.split()[3], mask))
    return out


def binary(mask):
    return mask.point(lambda v: 255 if v > 40 else 0)


def pixels(mask):
    px = mask.load()
    w, h = mask.size
    return [(x, y) for y in range(0, h) for x in range(0, w) if px[x, y]]


def canonical_limb(piece, mask, reference, name_upper, name_lower, parts_dir, parts):
    """Rotates a limb about its joint until its far end hangs straight down, then halves it."""
    pts = pixels(mask)
    joint = min(pts, key=lambda p: (p[0] - reference[0]) ** 2 + (p[1] - reference[1]) ** 2)
    tip = max(pts, key=lambda p: (p[0] - joint[0]) ** 2 + (p[1] - joint[1]) ** 2)
    length = math.hypot(tip[0] - joint[0], tip[1] - joint[1])

    box = piece.getbbox()
    crop = piece.crop(box)
    jx, jy = joint[0] - box[0], joint[1] - box[1]
    side = int(length * 2 + 80)
    canvas = Image.new('RGBA', (side, side), (0, 0, 0, 0))
    canvas.paste(crop, (int(side / 2 - jx), int(side / 2 - jy)))
    angle = math.degrees(math.atan2(tip[1] - joint[1], tip[0] - joint[0]))
    canvas = canvas.rotate(angle - 90.0, resample=Image.BICUBIC, center=(side / 2, side / 2))

    mid = side / 2 + length / 2.0
    upper = canvas.crop((0, 0, side, int(mid + JOINT_OVERLAP)))
    lower = canvas.crop((0, int(mid - JOINT_OVERLAP), side, side))
    ubox, lbox = upper.getbbox(), lower.getbbox()
    upper.crop(ubox).save(os.path.join(parts_dir, name_upper + '.png'))
    lower.crop(lbox).save(os.path.join(parts_dir, name_lower + '.png'))

    parts[name_upper] = {'texture': 'parts/%s.png' % name_upper,
                         'pivot': [round(side / 2 - ubox[0], 2), round(side / 2 - ubox[1], 2)]}
    lower_top = int(mid - JOINT_OVERLAP)
    parts[name_lower] = {'texture': 'parts/%s.png' % name_lower,
                         'pivot': [round(side / 2 - lbox[0], 2), round(mid - lower_top - lbox[1], 2)]}
    return joint, length


def main():
    parts_dir = os.path.join(ROOT, 'parts')
    poses_dir = os.path.join(ROOT, 'poses')
    for d in (parts_dir, poses_dir):
        if not os.path.isdir(d):
            os.makedirs(d)

    m = {}
    full = ce.render(masks=m)
    size = full.size

    # The limbs. A leg is its coloured shape and the outline round it, minus the body it tucks
    # up behind - so the body's own outline stays on the body.
    body_keep = m['body'].filter(ImageFilter.MaxFilter(9))
    limb_masks = {
        'leg_left': ImageChops.subtract(binary(m['leg_left']).filter(ImageFilter.MaxFilter(25)), body_keep),
        'leg_right': ImageChops.subtract(binary(m['leg_right']).filter(ImageFilter.MaxFilter(25)), body_keep),
        'arm_left': binary(m['arm_left']).filter(ImageFilter.MaxFilter(3)),
        'arm_right': binary(m['arm_right']).filter(ImageFilter.MaxFilter(3)),
    }
    everything = Image.new('L', size, 0)
    for mask in limb_masks.values():
        everything = ImageChops.lighter(everything, mask)

    parts = {}
    lengths = {}
    joints = {}
    for key, upper, lower, ref in (
        ('leg_left', 'LegBack_Upper', 'LegBack_Lower', HIP),
        ('leg_right', 'LegFront_Upper', 'LegFront_Lower', HIP),
        ('arm_left', 'ArmBack_Upper', 'ArmBack_Lower', BODY_CENTRE),
        ('arm_right', 'ArmFront_Upper', 'ArmFront_Lower', BODY_CENTRE),
    ):
        piece = masked(full, limb_masks[key])
        joints[key], lengths[key] = canonical_limb(piece, limb_masks[key], ref, upper, lower, parts_dir, parts)

    # The body: everything that is not a limb - but only on or above the V, so a stray bit of
    # leg outline left over from the cut does not ride along with the body.
    above = Image.new('L', size, 0)
    above.paste(255, (0, 0, size[0], HIP[1] - 30))
    torso_keep = ImageChops.lighter(m['body'].filter(ImageFilter.MaxFilter(31)), above)
    torso = masked(full, ImageChops.multiply(ImageChops.invert(everything), torso_keep))
    # Near-invisible specks would otherwise stretch the part's box down to his feet.
    torso.putalpha(torso.split()[3].point(lambda v: v if v > 12 else 0))
    tbox = torso.getbbox()
    torso.crop(tbox).save(os.path.join(parts_dir, 'Torso.png'))
    parts['Torso'] = {'texture': 'parts/Torso.png', 'pivot': [HIP[0] - tbox[0], HIP[1] - tbox[1]]}

    # The sword in his hand, stored hanging down from the grip like every other part.
    spec = [s for s in ex.SWORDS if s[0] == HELD_SWORD][0]
    sword, grip = ex.draw_sword(spec, with_grip=True)
    held = sword.transpose(Image.FLIP_TOP_BOTTOM)
    held.save(os.path.join(parts_dir, 'PropFront.png'))
    parts['PropFront'] = {'texture': 'parts/PropFront.png',
                          'pivot': [round(grip[0], 1), round(held.size[1] - 1 - grip[1], 1)]}

    # Every sword as move art, anchored at its grip.
    poses = {}
    for spec in ex.SWORDS:
        image, grip = ex.draw_sword(spec, with_grip=True)
        name = 'sword_' + spec[0]
        image.save(os.path.join(poses_dir, name + '.png'))
        poses[name] = {'texture': 'poses/%s.png' % name, 'anchor': [round(grip[0], 1), round(grip[1], 1)]}
        if spec[0] == HELD_SWORD:
            # Planted: tip down, the last of the blade sunk out of sight in the ground.
            planted = image.transpose(Image.FLIP_TOP_BOTTOM)
            planted = planted.crop((0, 0, planted.size[0], int(planted.size[1] * 0.8)))
            planted.save(os.path.join(poses_dir, 'planted.png'))
            poses['planted'] = {'texture': 'poses/planted.png',
                                'anchor': [planted.size[0] / 2.0, planted.size[1] / 2.0]}

    # The turning frames, hung on the Torso and pinned at the hip, so they sit exactly where
    # the body does. The game mirrors them for the second half of a turn.
    frames = [ex.turn_frame(deg) for deg in ex.TURN_ANGLES]
    extras = []
    for i, (deg, frame) in enumerate(zip(ex.TURN_ANGLES, frames)):
        box = frame.getbbox()
        name = 'turn_%03d' % deg
        frame.crop(box).save(os.path.join(poses_dir, name + '.png'))
        parts['Turn%d' % i] = {'texture': 'poses/%s.png' % name, 'pivot': [HIP[0] - box[0], HIP[1] - box[1]]}
        extras.append({'name': 'turn%d' % i, 'bone': 'Torso', 'part': 'Turn%d' % i, 'offset': [0, 0]})
        print('turn', deg)

    def rel(point):
        return [round(point[0] - HIP[0], 1), round(point[1] - HIP[1], 1)]

    bones = [
        {'name': 'Hip', 'parent': None, 'offset': [0, 0], 'part': None},
        {'name': 'LegBack_Upper', 'parent': 'Hip', 'offset': rel(joints['leg_left']), 'part': 'LegBack_Upper'},
        {'name': 'LegBack_Lower', 'parent': 'LegBack_Upper', 'offset': [0, round(lengths['leg_left'] / 2, 1)], 'part': 'LegBack_Lower'},
        {'name': 'LegFront_Upper', 'parent': 'Hip', 'offset': rel(joints['leg_right']), 'part': 'LegFront_Upper'},
        {'name': 'LegFront_Lower', 'parent': 'LegFront_Upper', 'offset': [0, round(lengths['leg_right'] / 2, 1)], 'part': 'LegFront_Lower'},
        {'name': 'Torso', 'parent': 'Hip', 'offset': [0, 0], 'part': 'Torso'},
        {'name': 'ArmBack_Upper', 'parent': 'Torso', 'offset': rel(joints['arm_left']), 'part': 'ArmBack_Upper'},
        {'name': 'ArmBack_Lower', 'parent': 'ArmBack_Upper', 'offset': [0, round(lengths['arm_left'] / 2, 1)], 'part': 'ArmBack_Lower'},
        {'name': 'Head', 'parent': 'Torso', 'offset': [0, -(HIP[1] - tbox[1])], 'part': None},
        {'name': 'ArmFront_Upper', 'parent': 'Torso', 'offset': rel(joints['arm_right']), 'part': 'ArmFront_Upper'},
        {'name': 'ArmFront_Lower', 'parent': 'ArmFront_Upper', 'offset': [0, round(lengths['arm_right'] / 2, 1)], 'part': 'ArmFront_Lower'},
        {'name': 'PropFront', 'parent': 'ArmFront_Lower', 'offset': [0, round(lengths['arm_right'] / 2, 1)], 'part': 'PropFront'},
    ]

    rig = {
        'name': 'EdgeLord',
        'canonicalHeight': HIP[1] - tbox[1],
        'legLength': round((lengths['leg_left'] + lengths['leg_right']) / 2.0, 1),
        'backLimbDarken': 0.8,
        'bones': bones,
        'parts': parts,
        'extras': extras,
        'poses': poses,
    }
    with open(os.path.join(ROOT, 'rig.json'), 'wb') as fh:
        fh.write(json.dumps(rig, indent=2, sort_keys=True).encode('utf-8'))
    print('legs %d/%d  arms %d/%d  body %dx%d' % (
        lengths['leg_left'], lengths['leg_right'], lengths['arm_left'], lengths['arm_right'],
        tbox[2] - tbox[0], tbox[3] - tbox[1]))


if __name__ == '__main__':
    main()
