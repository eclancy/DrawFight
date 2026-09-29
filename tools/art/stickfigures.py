# -*- coding: utf-8 -*-
"""
Generates two TEST fighters so that the rig can be built and played before any real drawing
exists. Run with the only Python on this machine:

    python tools/art/stickfigures.py

These are scaffolding, not content. They exist to prove the shared skeleton and the animation
library work, and they should be deleted the moment two real drawings arrive - see
fighters/README.md. Nothing here is a model for how his art gets processed: the real pipeline
cuts parts out of a photograph (M3), while this draws each part directly in its canonical
orientation, which is the "hand-cut test fighter" step M2 asks for.

Emits, per fighter:
    fighters/<name>/source/drawing.png   the whole figure on paper, for M3's importer to chew on
    fighters/<name>/parts/*.png          canonical-orientation transparent parts
    fighters/<name>/rig.json             bone tree, offsets and sprite pivots

CANONICAL ORIENTATION is the contract between this script and FighterRig.cs: every limb segment
is stored pointing straight DOWN from a pivot at its top, the torso points UP from a pivot at
the hip, and the head sits above a pivot at the base of the neck. A bone rotation of zero
therefore means "hanging straight down", and that is what lets one animation library drive every
fighter regardless of the pose it was drawn in.
"""

from __future__ import division, print_function

import json
import os
import random

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))

PAD = 10  # transparent margin around each part, so round caps are not clipped


# --------------------------------------------------------------------------- figures

SWIFT = {
    'name': 'Swift',
    'ink': (34, 34, 42, 255),
    'stroke': 13,
    'head_radius': 60,
    'head_style': 'spiky',
    'torso_style': 'stick',
    'torso_len': 205,
    'torso_width': 15,
    'upper_arm': 104,
    'lower_arm': 98,
    'upper_leg': 132,
    'lower_leg': 126,
    'hip_split': 9,
    'prop': None,
}

LUG = {
    'name': 'Lug',
    'ink': (30, 28, 32, 255),
    'stroke': 19,
    'head_radius': 76,
    'head_style': 'round',
    'torso_style': 'bulky',
    'torso_len': 190,
    'torso_width': 104,
    'upper_arm': 96,
    'lower_arm': 92,
    'upper_leg': 112,
    'lower_leg': 104,
    'hip_split': 20,
    'prop': {'name': 'PropFront', 'length': 150, 'head': 46},
}

FIGURES = [SWIFT, LUG]


# --------------------------------------------------------------------------- helpers

def new_part(w, h):
    return Image.new('RGBA', (int(w), int(h)), (0, 0, 0, 0))


def thick_line(draw, p0, p1, width, ink):
    """A line with round caps, since this PIL has no joint or cap options."""
    draw.line([p0, p1], fill=ink, width=int(width))
    r = width / 2.0
    for (x, y) in (p0, p1):
        draw.ellipse([x - r, y - r, x + r, y + r], fill=ink)


def limb_part(length, width, ink, taper=1.0):
    """
    One limb segment in canonical orientation: pointing straight down, pivot at the top.
    Returns (image, pivot).
    """
    end_width = max(3.0, width * taper)
    w = int(max(width, end_width) + PAD * 2)
    h = int(length + width + PAD * 2)
    img = new_part(w, h)
    draw = ImageDraw.Draw(img)

    cx = w / 2.0
    top = PAD + width / 2.0
    bottom = top + length

    # Drawn as a stack of shrinking circles so a tapered limb is possible without a polygon.
    steps = int(length)
    for i in range(steps + 1):
        t = i / float(steps) if steps else 0.0
        y = top + t * length
        r = (width + (end_width - width) * t) / 2.0
        draw.ellipse([cx - r, y - r, cx + r, y + r], fill=ink)

    return img, (cx, top)


def head_part(cfg):
    """Head in profile, facing right. Pivot at the base of the neck."""
    r = cfg['head_radius']
    ink = cfg['ink']
    stroke = cfg['stroke']
    neck = 30

    w = int(r * 2 + PAD * 2 + 34)
    h = int(r * 2 + neck + PAD * 2)
    img = new_part(w, h)
    draw = ImageDraw.Draw(img)

    cx = PAD + r
    cy = PAD + r
    pivot = (cx, cy + r + neck - stroke / 2.0)

    # Neck, then the skull on top of it.
    thick_line(draw, (cx, cy + r * 0.72), (pivot[0], pivot[1]), stroke, ink)
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=ink, width=int(stroke))

    # Profile: nose bump and one eye on the right, because the figure faces right.
    nose_x = cx + r
    draw.polygon(
        [(nose_x - 10, cy - 10), (nose_x + 34, cy + 8), (nose_x - 10, cy + 26)],
        fill=ink)
    eye_r = max(4, r * 0.11)
    draw.ellipse([cx + r * 0.34 - eye_r, cy - r * 0.22 - eye_r,
                  cx + r * 0.34 + eye_r, cy - r * 0.22 + eye_r], fill=ink)

    if cfg['head_style'] == 'spiky':
        for i, (dx, dy, lx, ly) in enumerate([
                (-0.60, -0.74, -0.46, -1.52),
                (-0.16, -0.96, -0.02, -1.70),
                (0.34, -0.88, 0.60, -1.46)]):
            thick_line(draw,
                       (cx + r * dx, cy + r * dy),
                       (cx + r * lx, cy + r * ly),
                       stroke * 0.8, ink)
    else:
        # A flat cap of hair, which reads as heavier than spikes.
        thick_line(draw, (cx - r * 0.86, cy - r * 0.52), (cx + r * 0.80, cy - r * 0.62),
                   stroke * 1.5, ink)

    return img, pivot


def torso_part(cfg):
    """Torso in canonical orientation: extending UP from a pivot at the hip."""
    length = cfg.get('torso_len', 0)
    ink = cfg['ink']
    stroke = cfg['stroke']

    if cfg['torso_style'] == 'bulky':
        body_w = cfg['torso_width']
        w = int(body_w + PAD * 2 + stroke)
        h = int(length + PAD * 2 + stroke)
        img = new_part(w, h)
        draw = ImageDraw.Draw(img)
        cx = w / 2.0
        top = PAD + stroke / 2.0
        bottom = top + length
        # A barrel: wide at the chest, narrower at the hip.
        for i in range(int(length) + 1):
            t = i / float(length)
            y = top + i
            half = (body_w * (0.92 - 0.30 * t)) / 2.0
            draw.ellipse([cx - half, y - stroke / 2.0, cx + half, y + stroke / 2.0], fill=ink)
        pivot = (cx, bottom)
    else:
        w = int(cfg['torso_width'] + PAD * 2 + stroke)
        h = int(length + PAD * 2 + stroke)
        img = new_part(w, h)
        draw = ImageDraw.Draw(img)
        cx = w / 2.0
        top = PAD + stroke / 2.0
        bottom = top + length
        thick_line(draw, (cx, top), (cx, bottom), stroke, ink)
        # Shoulder and hip bars, so a stick torso still has something to hang limbs off.
        thick_line(draw, (cx - 26, top + 6), (cx + 26, top + 6), stroke * 0.8, ink)
        pivot = (cx, bottom)

    return img, pivot


def prop_part(cfg):
    """
    A club, canonical orientation pointing DOWN from a pivot at the grip - the same way the
    limbs are stored. The club hangs off the forearm, so pointing down means rotation zero
    continues the arm OUTWARD. It was first stored pointing up, which laid the club back along
    the forearm, pointing at the fighter's own elbow.
    """
    spec = cfg['prop']
    ink = cfg['ink']
    stroke = cfg['stroke']
    length = spec['length']
    head = spec['head']

    w = int(head * 2 + PAD * 2)
    h = int(length + head + PAD * 2)
    img = new_part(w, h)
    draw = ImageDraw.Draw(img)

    cx = w / 2.0
    grip_y = h - PAD
    thick_line(draw, (cx, grip_y), (cx, PAD + head), stroke, ink)
    draw.ellipse([cx - head / 2.0, PAD, cx + head / 2.0, PAD + head * 1.3],
                 outline=ink, width=int(stroke))

    # Drawn head-up for convenience, then flipped so the grip is at the top.
    img = img.transpose(Image.FLIP_TOP_BOTTOM)
    return img, (cx, h - grip_y)


# --------------------------------------------------------------------------- composite

def composite_drawing(cfg):
    """
    The whole figure on paper, posed mid-stride, as if photographed. Not used by the rig - this
    is the input M3's importer will be tested against, and the reason it exists now is so that
    the importer has something realistic to fail on before real drawings are at stake.
    """
    W, H = 780, 950
    paper = Image.new('RGB', (W, H), (251, 249, 244))

    # Paper grain and a soft lighting gradient, both built small and scaled up so that PIL
    # without numpy stays fast. Kept very subtle on purpose: the point is to give M3's
    # background removal something slightly uneven to cope with, not to bury the linework.
    small = Image.new('L', (W // 10, H // 10))
    small.putdata([random.randint(0, 255) for _ in range(small.size[0] * small.size[1])])
    grain = small.resize((W, H), Image.BILINEAR).filter(ImageFilter.GaussianBlur(3))
    paper = Image.blend(paper, Image.merge('RGB', (grain, grain, grain)), 0.045)

    shade = Image.new('L', (32, 40))
    shade.putdata([min(255, 188 + (x * 3) + (y * 2))
                   for y in range(40) for x in range(32)])
    shade = shade.resize((W, H), Image.BILINEAR)
    paper = Image.composite(paper, Image.new('RGB', (W, H), (232, 228, 219)), shade)
    paper = paper.convert('RGBA')

    draw = ImageDraw.Draw(paper)
    ink = cfg['ink']
    stroke = cfg['stroke']

    # Mid-stride pose: front limbs forward-right, back limbs swung back-left, nothing touching.
    hip = (370, 572)
    shoulder = (370, 572 - cfg['torso_len'])
    ua, la = cfg['upper_arm'], cfg['lower_arm']
    ul, ll = cfg['upper_leg'], cfg['lower_leg']

    # Pronounced bends at every joint. A limb drawn straight gives the importer no visual cue
    # where to split it, and gives whoever places pivots nothing to aim at.
    f_elbow = (shoulder[0] + ua * 0.70, shoulder[1] + ua * 0.62)
    f_hand = (f_elbow[0] + la * 0.90, f_elbow[1] - la * 0.32)
    b_elbow = (shoulder[0] - ua * 0.58, shoulder[1] + ua * 0.74)
    b_hand = (b_elbow[0] - la * 0.34, b_elbow[1] + la * 0.90)

    f_knee = (hip[0] + ul * 0.58, hip[1] + ul * 0.80)
    f_foot = (f_knee[0] + ll * 0.06, f_knee[1] + ll * 0.99)
    b_knee = (hip[0] - ul * 0.44, hip[1] + ul * 0.89)
    b_foot = (b_knee[0] - ll * 0.56, b_knee[1] + ll * 0.82)

    # Back limbs first so the front ones overlap them, which is the depth order the rig uses.
    thick_line(draw, shoulder, b_elbow, stroke, ink)
    thick_line(draw, b_elbow, b_hand, stroke, ink)
    thick_line(draw, hip, b_knee, stroke, ink)
    thick_line(draw, b_knee, b_foot, stroke, ink)

    if cfg['torso_style'] == 'bulky':
        body_w = cfg['torso_width']
        for i in range(cfg['torso_len'] + 1):
            t = i / float(cfg['torso_len'])
            y = shoulder[1] + i
            half = (body_w * (0.92 - 0.30 * t)) / 2.0
            draw.ellipse([hip[0] - half, y - stroke / 2.0, hip[0] + half, y + stroke / 2.0],
                         fill=ink)
    else:
        thick_line(draw, shoulder, hip, stroke, ink)
        thick_line(draw, (shoulder[0] - 26, shoulder[1] + 6),
                   (shoulder[0] + 26, shoulder[1] + 6), stroke * 0.8, ink)

    thick_line(draw, shoulder, f_elbow, stroke, ink)
    thick_line(draw, f_elbow, f_hand, stroke, ink)
    thick_line(draw, hip, f_knee, stroke, ink)
    thick_line(draw, f_knee, f_foot, stroke, ink)

    # Head, drawn from the canonical part so the composite cannot disagree with the rig.
    head_img, head_pivot = head_part(cfg)
    neck = (shoulder[0], shoulder[1])
    paper.paste(head_img,
                (int(neck[0] - head_pivot[0]), int(neck[1] - head_pivot[1])),
                head_img)

    if cfg['prop']:
        club, grip = prop_part(cfg)
        # Held up in the photo, so turn the stored (grip-up) club back head-up first.
        club = club.transpose(Image.FLIP_TOP_BOTTOM)
        club = club.rotate(-28, expand=True, resample=Image.BICUBIC)
        paper.paste(club, (int(f_hand[0] - club.size[0] / 2), int(f_hand[1] - club.size[1] + 20)),
                    club)

    return paper.convert('RGB')


# --------------------------------------------------------------------------- emit

def build(cfg):
    name = cfg['name']
    root = os.path.join(REPO, 'fighters', name.lower())
    parts_dir = os.path.join(root, 'parts')
    source_dir = os.path.join(root, 'source')
    for d in (parts_dir, source_dir) if cfg.get('composite', True) else (parts_dir,):
        if not os.path.isdir(d):
            os.makedirs(d)

    ink = cfg['ink']
    stroke = cfg['stroke']
    parts = {}

    def emit(part_name, img_pivot):
        img, pivot = img_pivot
        img.save(os.path.join(parts_dir, part_name + '.png'))
        parts[part_name] = {
            'texture': 'parts/%s.png' % part_name,
            'pivot': [round(pivot[0], 2), round(pivot[1], 2)],
        }

    if cfg['head_style']:
        emit('Head', head_part(cfg))
    emit('Torso', torso_part(cfg))
    for side in ('Front', 'Back'):
        emit('Arm%s_Upper' % side, limb_part(cfg['upper_arm'], stroke, ink, taper=0.92))
        emit('Arm%s_Lower' % side, limb_part(cfg['lower_arm'], stroke * 0.92, ink, taper=0.85))
        emit('Leg%s_Upper' % side, limb_part(cfg['upper_leg'], stroke * 1.08, ink, taper=0.90))
        emit('Leg%s_Lower' % side, limb_part(cfg['lower_leg'], stroke, ink, taper=0.80))
    if cfg['prop']:
        emit(cfg['prop']['name'], prop_part(cfg))

    torso_len = cfg['torso_len']
    shoulder_y = -torso_len * 0.90

    # Bone offsets are relative to the PARENT bone, in canonical space. FighterRig.cs walks this
    # tree directly; the order of "children" is also the draw order, back-to-front.
    bones = [
        {'name': 'Hip', 'parent': None, 'offset': [0, 0], 'part': None},

        {'name': 'LegBack_Upper', 'parent': 'Hip',
         'offset': [-cfg['hip_split'], 0], 'part': 'LegBack_Upper'},
        {'name': 'LegBack_Lower', 'parent': 'LegBack_Upper',
         'offset': [0, cfg['upper_leg']], 'part': 'LegBack_Lower'},

        {'name': 'LegFront_Upper', 'parent': 'Hip',
         'offset': [cfg['hip_split'], 0], 'part': 'LegFront_Upper'},
        {'name': 'LegFront_Lower', 'parent': 'LegFront_Upper',
         'offset': [0, cfg['upper_leg']], 'part': 'LegFront_Lower'},

        {'name': 'Torso', 'parent': 'Hip', 'offset': [0, 0], 'part': 'Torso'},

        {'name': 'ArmBack_Upper', 'parent': 'Torso',
         'offset': [-cfg['hip_split'] * 0.6, shoulder_y], 'part': 'ArmBack_Upper'},
        {'name': 'ArmBack_Lower', 'parent': 'ArmBack_Upper',
         'offset': [0, cfg['upper_arm']], 'part': 'ArmBack_Lower'},

        {'name': 'Head', 'parent': 'Torso',
         'offset': [0, -torso_len], 'part': 'Head' if cfg['head_style'] else None},

        {'name': 'ArmFront_Upper', 'parent': 'Torso',
         'offset': [cfg['hip_split'] * 0.6, shoulder_y], 'part': 'ArmFront_Upper'},
        {'name': 'ArmFront_Lower', 'parent': 'ArmFront_Upper',
         'offset': [0, cfg['upper_arm']], 'part': 'ArmFront_Lower'},
    ]

    if cfg['prop']:
        bones.append({'name': cfg['prop']['name'], 'parent': 'ArmFront_Lower',
                      'offset': [0, cfg['lower_arm']], 'part': cfg['prop']['name']})

    # Hip-to-crown in canonical space. FighterRig scales every fighter so this maps to one
    # standard height, so that how big a fighter is in game is a deliberate choice rather than
    # an accident of how close the camera was held.
    if cfg['head_style']:
        canonical_height = torso_len + cfg['head_radius'] * 2 + 30
    else:
        canonical_height = torso_len

    rig = {
        'name': name,
        'canonicalHeight': canonical_height,
        'legLength': cfg['upper_leg'] + cfg['lower_leg'],
        'backLimbDarken': 0.70,
        'bones': bones,
        'parts': parts,
    }

    with open(os.path.join(root, 'rig.json'), 'wb') as fh:
        fh.write(json.dumps(rig, indent=2, sort_keys=True).encode('utf-8'))

    # A stand-in has no source drawing: source/ is reserved for the photographed original.
    if cfg.get('composite', True):
        composite_drawing(cfg).save(os.path.join(source_dir, 'drawing.png'))

    print('%-6s -> %d parts, canonical height %d' % (name, len(parts), canonical_height))


def main():
    import sys
    # Optional names on the command line build only those figures, e.g. "stickfigures.py circy".
    wanted = [a.lower() for a in sys.argv[1:]]
    random.seed(7)
    for cfg in FIGURES:
        if wanted and cfg['name'].lower() not in wanted:
            continue
        build(cfg)
    print('done')


if __name__ == '__main__':
    main()
