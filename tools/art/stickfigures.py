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
    # Shown in game as Flambe (FighterData); the folder stays fighters/swift.
    # A fire punk: a flame-coloured mohawk, a French chef's twirled moustache, a black leather
    # jacket with silver spikes on the shoulders and flames licking up from the hem, dark red
    # jeans and boots.
    'head_style': 'mohawk',
    'mustache': True,
    'torso_style': 'bulky',
    'torso_len': 205,
    'torso_width': 74,
    'upper_arm': 104,
    'lower_arm': 98,
    'upper_leg': 132,
    'lower_leg': 126,
    'hip_split': 9,
    # No weapon of his own, but a hand to put one in: an empty prop part, so a move can put the
    # flaming frying pan in his hand (a French chef, after all) the way Lug swaps his tools.
    'prop': {'name': 'PropFront', 'style': 'empty', 'length': 4, 'head': 2},
    'tools': ['wings'],
    'held_tools': ['pan'],
    'colours': {
        'jacket': (52, 48, 58, 255),
        'shirt': (52, 48, 58, 255),
        'skin': (236, 190, 150, 255),
        'jeans': (126, 34, 42, 255),
        'boots': (40, 36, 40, 255),
    },
}

LUG = {
    'name': 'Lug',  # shown in game as Lugnut (FighterData); the folder stays fighters/lug
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
    # A sledgehammer: a long handle with a heavy block across its end. Lug is a construction
    # worker, and a hard hat goes on when he blocks.
    'prop': {'name': 'PropFront', 'style': 'sledgehammer', 'length': 230, 'head': 56},
    'hard_hat': True,
    # Drawings for his construction-site specials, emitted as poses (whole pictures, not rig
    # parts) the way Circy's effect drawings are.
    'tools': ['wheelbarrow', 'wreckingball', 'girder', 'cone'],
    # The heavy tools he swaps into his hand for different attacks, tip up and anchored at the
    # grip like any held weapon (see MoveData.PropArt).
    'held_tools': ['sledgehammer', 'pickaxe', 'shovel', 'pipewrench', 'crowbar', 'jackhammer',
                   'beam', 'sign', 'nailgun'],
    # Colour, so he is a construction worker and not a silhouette: a hi-vis vest over a blue
    # work shirt, jeans, boots, and a face.
    'colours': {
        'vest': (246, 122, 30, 255),
        'stripe': (250, 226, 90, 255),
        'shirt': (66, 112, 190, 255),
        'skin': (234, 184, 142, 255),
        'jeans': (54, 84, 144, 255),
        'boots': (128, 82, 42, 255),
    },
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


def limb_part(length, width, ink, taper=1.0, fill=None, end_fill=None, end_from=1.0):
    """
    One limb segment in canonical orientation: pointing straight down, pivot at the top.
    Returns (image, pivot). With a fill colour it is an ink outline coloured in - a sleeve, a
    trouser leg - and end_fill recolours it from end_from of the way down (a hand, a boot).
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

    if fill:
        # The colour inside the outline, leaving a rim of ink round it.
        rim = max(3.0, width * 0.2)
        for i in range(steps + 1):
            t = i / float(steps) if steps else 0.0
            y = top + t * length
            r = (width + (end_width - width) * t) / 2.0 - rim
            if r <= 0:
                continue
            colour = end_fill if end_fill and t >= end_from else fill
            draw.ellipse([cx - r, y - r, cx + r, y + r], fill=colour)

    return img, (cx, top)


def head_part(cfg):
    """Head in profile, facing right. Pivot at the base of the neck."""
    r = cfg['head_radius']
    ink = cfg['ink']
    stroke = cfg['stroke']
    neck = 30

    # Room above the skull for hair that sticks up.
    hair = r * 1.5 if cfg['head_style'] == 'mohawk' else 0
    w = int(r * 2 + PAD * 2 + 34)
    h = int(r * 2 + neck + PAD * 2 + hair)
    img = new_part(w, h)
    draw = ImageDraw.Draw(img)

    cx = PAD + r
    cy = PAD + r + hair
    pivot = (cx, cy + r + neck - stroke / 2.0)

    # Neck, then the skull on top of it.
    thick_line(draw, (cx, cy + r * 0.72), (pivot[0], pivot[1]), stroke, ink)
    skin = cfg.get('colours', {}).get('skin')
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=skin, outline=ink, width=int(stroke))

    # Profile: nose bump and one eye on the right, because the figure faces right.
    nose_x = cx + r
    draw.polygon(
        [(nose_x - 10, cy - 10), (nose_x + 34, cy + 8), (nose_x - 10, cy + 26)],
        fill=ink)
    eye_r = max(4, r * 0.11)
    draw.ellipse([cx + r * 0.34 - eye_r, cy - r * 0.22 - eye_r,
                  cx + r * 0.34 + eye_r, cy - r * 0.22 + eye_r], fill=ink)

    if cfg.get('mustache'):
        # A French chef's moustache, seen side on: a thick bar under the nose that sweeps out in
        # front of the face and curls up into a tight twirl at the tip, with the far side's curl
        # just showing behind it.
        brown = (44, 30, 26, 255)
        lip_y = cy + r * 0.56
        w = stroke * 0.95
        bar = [(cx + r * 0.72, lip_y), (cx + r * 0.98, lip_y + 5), (cx + r * 1.26, lip_y)]
        draw.line(bar, fill=brown, width=int(w))
        for x, y in bar:
            draw.ellipse([x - w * 0.5, y - w * 0.5, x + w * 0.5, y + w * 0.5], fill=brown)
        # A tight twirl curling up at each end.
        for (x, y), start, end in (((cx + r * 1.30, lip_y - 11), 0, 250), ((cx + r * 0.68, lip_y - 11), 290, 540)):
            draw.arc([x - 12, y - 12, x + 12, y + 12], start, end, fill=brown, width=int(stroke * 0.55))

    if cfg['head_style'] == 'mohawk':
        # A tall mohawk of flame: a row of spikes along the top of the skull, front to back,
        # red at the root and yellow at the tips, the tallest in the middle.
        spikes = [(-0.62, 0.9), (-0.30, 1.25), (0.02, 1.4), (0.34, 1.2), (0.62, 0.85)]
        for i, (dx, height) in enumerate(spikes):
            base_x = cx + r * dx
            base_y = cy - r * (0.92 - 0.35 * abs(dx))
            tip = (base_x - r * 0.22, base_y - r * height)
            left = (base_x - r * 0.2, base_y + r * 0.12)
            right = (base_x + r * 0.2, base_y + r * 0.12)
            draw.polygon([left, tip, right], fill=(222, 58, 40, 255), outline=ink)
            inner_tip = (base_x - r * 0.17, base_y - r * height * 0.72)
            draw.polygon([(base_x - r * 0.1, base_y + r * 0.05), inner_tip, (base_x + r * 0.1, base_y + r * 0.05)],
                         fill=(250, 150, 40, 255))
            yellow_tip = (base_x - r * 0.13, base_y - r * height * 0.42)
            draw.polygon([(base_x - r * 0.05, base_y), yellow_tip, (base_x + r * 0.05, base_y)],
                         fill=(252, 220, 80, 255))
        # Redraw the skull line over the roots, so the hair grows from it.
        draw.arc([cx - r, cy - r, cx + r, cy + r], 200, 340, fill=ink, width=int(stroke))
    elif cfg['head_style'] == 'spiky':
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

        colours = cfg.get('colours')
        if colours and colours.get('jacket'):
            # A leather jacket: dark inside the outline, flames licking up from the hem, a
            # silver zip, and a row of silver spikes along the shoulders.
            rim = stroke * 0.55
            for i in range(int(length) + 1):
                t = i / float(length)
                y = top + i
                half = (body_w * (0.92 - 0.30 * t)) / 2.0 - rim
                if half > 0:
                    draw.ellipse([cx - half, y - stroke / 2.0 + rim * 0.3, cx + half, y + stroke / 2.0 - rim * 0.3],
                                 fill=colours['jacket'])
            hem = bottom - 4
            import math
            for j, (fx, fh) in enumerate([(-0.28, 0.42), (0.0, 0.62), (0.28, 0.46)]):
                bx = cx + body_w * fx
                w_ = body_w * 0.2
                draw.polygon([(bx - w_, hem), (bx - w_ * 0.4, hem - length * fh * 0.5), (bx, hem - length * fh),
                              (bx + w_ * 0.4, hem - length * fh * 0.5), (bx + w_, hem)], fill=(236, 84, 36, 255))
                draw.polygon([(bx - w_ * 0.5, hem), (bx, hem - length * fh * 0.6), (bx + w_ * 0.5, hem)],
                             fill=(252, 196, 64, 255))
            draw.line([(cx + body_w * 0.12, top + 10), (cx + body_w * 0.08, bottom - 10)], fill=(186, 190, 200, 255), width=3)
            for k in range(5):
                sx = cx - body_w * 0.4 + k * body_w * 0.2
                sy = top + 4 + abs(k - 2) * 3
                draw.polygon([(sx - 7, sy + 6), (sx, sy - 12), (sx + 7, sy + 6)], fill=(200, 204, 214, 255), outline=ink)
        elif colours:
            # A hi-vis vest, inset from the ink outline, with two reflective stripes across it.
            rim = stroke * 0.55
            for i in range(int(length) + 1):
                t = i / float(length)
                y = top + i
                half = (body_w * (0.92 - 0.30 * t)) / 2.0 - rim
                if half <= 0:
                    continue
                stripe = 0.42 < t < 0.52 or 0.70 < t < 0.80
                colour = colours['stripe'] if stripe else colours['vest']
                draw.ellipse([cx - half, y - stroke / 2.0 + rim * 0.3, cx + half, y + stroke / 2.0 - rim * 0.3],
                             fill=colour)
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
    if spec.get('style') == 'empty':
        # Nothing in the hand: a blank part, only there so a move has a hand to put a prop in.
        return new_part(w, h), (cx, PAD)
    if spec.get('style') == 'sledgehammer':
        # A long yellow fibreglass handle, and a solid steel block set across its end.
        thick_line(draw, (cx, grip_y), (cx, PAD + head * 0.5), stroke * 0.8, ink)
        thick_line(draw, (cx, grip_y - 4), (cx, PAD + head * 0.5), stroke * 0.4, TOOL_YELLOW)
        block_w = head * 1.9
        block = [cx - block_w / 2.0, PAD, cx + block_w / 2.0, PAD + head]
        draw.rectangle(block, fill=TOOL_STEEL, outline=ink, width=int(stroke * 0.8))
    else:
        thick_line(draw, (cx, grip_y), (cx, PAD + head), stroke, ink)
        draw.ellipse([cx - head / 2.0, PAD, cx + head / 2.0, PAD + head * 1.3],
                     outline=ink, width=int(stroke))

    # Drawn head-up for convenience, then flipped so the grip is at the top.
    img = img.transpose(Image.FLIP_TOP_BOTTOM)
    return img, (cx, h - grip_y)


def hard_hat_part(cfg):
    """
    A yellow hard hat, drawn to sit on top of the head. Pivot at the middle of the brim, which is
    where it rests on the skull.
    """
    r = cfg['head_radius']
    ink = cfg['ink']
    stroke = cfg['stroke']
    w = int(r * 2.5 + PAD * 2)
    h = int(r * 1.15 + PAD * 2)
    img = new_part(w, h)
    draw = ImageDraw.Draw(img)

    cx = w / 2.0
    brim_y = h - PAD - stroke * 0.6
    yellow = (246, 196, 52, 255)
    # The dome, then the brim across its base, then a ridge down the middle.
    draw.pieslice([cx - r * 1.02, brim_y - r * 1.02, cx + r * 1.02, brim_y + r * 1.02], 180, 360,
                  fill=yellow, outline=ink, width=int(stroke * 0.7))
    thick_line(draw, (cx - r * 1.22, brim_y), (cx + r * 1.22, brim_y), stroke * 0.9, ink)
    thick_line(draw, (cx, brim_y - r * 0.98), (cx, brim_y - r * 0.2), stroke * 0.5, ink)
    return img, (cx, brim_y)


TOOL_GREY = (118, 124, 138, 255)
TOOL_ORANGE = (236, 124, 56, 255)
TOOL_RED = (206, 70, 58, 255)
TOOL_YELLOW = (250, 204, 44, 255)
TOOL_STEEL = (150, 158, 174, 255)
TOOL_DARK = (96, 102, 118, 255)
TOOL_WOOD = (184, 126, 66, 255)


def held_tool(name, ink, stroke):
    """
    One of the heavy tools Lug swaps into his hand, drawn TIP UP with the grip near the bottom,
    as every held weapon is stored (see MoveData.PropArt). Returns (image, grip). All about as
    long as his sledgehammer, which is what his reach is built on.
    """
    w, h = 190, 300
    img = new_part(w, h)
    d = ImageDraw.Draw(img)
    cx = w / 2.0
    grip = (cx, h - 30)
    s = stroke * 0.7

    def handle(top, colour, width=0.9):
        thick_line(d, (cx, h - 16), (cx, top), s * width + 6, ink)
        thick_line(d, (cx, h - 20), (cx, top + 4), s * width, colour)

    if name == 'sledgehammer':
        handle(40, TOOL_YELLOW)
        d.rectangle([cx - 56, 12, cx + 56, 68], fill=TOOL_STEEL, outline=ink, width=int(s))
        d.rectangle([cx - 50, 18, cx - 36, 62], fill=(196, 202, 214, 255))
    elif name == 'pickaxe':
        handle(40, TOOL_WOOD)
        # A curved steel head, pointed at both ends, painted red at the eye.
        d.polygon([(cx - 88, 70), (cx - 30, 28), (cx + 30, 28), (cx + 88, 70), (cx + 30, 44), (cx - 30, 44)],
                  fill=TOOL_STEEL, outline=ink)
        d.line([(cx - 88, 70), (cx - 30, 28), (cx + 30, 28), (cx + 88, 70), (cx + 30, 44), (cx - 30, 44), (cx - 88, 70)],
               fill=ink, width=int(s * 0.6))
        d.rectangle([cx - 14, 22, cx + 14, 52], fill=TOOL_RED, outline=ink, width=int(s * 0.5))
    elif name == 'shovel':
        handle(90, TOOL_WOOD)
        # A pointed spade blade, and a D-grip... at the bottom, where he holds it.
        d.polygon([(cx - 40, 96), (cx + 40, 96), (cx + 40, 40), (cx, 8), (cx - 40, 40)], fill=TOOL_STEEL, outline=ink)
        d.line([(cx - 40, 96), (cx + 40, 96), (cx + 40, 40), (cx, 8), (cx - 40, 40), (cx - 40, 96)], fill=ink, width=int(s * 0.6))
        d.rectangle([cx - 12, 92, cx + 12, 118], fill=TOOL_ORANGE, outline=ink, width=int(s * 0.4))
    elif name == 'pipewrench':
        handle(80, TOOL_RED, 1.3)
        # Jaws at the top: a fixed hook and an adjusting nut.
        d.polygon([(cx - 22, 90), (cx - 22, 20), (cx + 40, 20), (cx + 40, 44), (cx + 4, 44), (cx + 4, 90)],
                  fill=TOOL_DARK, outline=ink)
        d.rectangle([cx - 30, 58, cx + 20, 76], fill=TOOL_STEEL, outline=ink, width=int(s * 0.5))
    elif name == 'crowbar':
        # A red crowbar with a hooked claw at the top.
        thick_line(d, (cx, h - 16), (cx, 60), s * 1.1 + 6, ink)
        thick_line(d, (cx, h - 20), (cx, 64), s * 1.1, TOOL_RED)
        d.arc([cx - 4, 14, cx + 60, 78], 180, 330, fill=ink, width=int(s * 1.1 + 6))
        d.arc([cx - 1, 17, cx + 57, 75], 180, 330, fill=TOOL_RED, width=int(s * 1.1))
    elif name == 'jackhammer':
        # A yellow body, T-handles at the bottom, and the steel chisel out of the top.
        d.rectangle([cx - 8, 6, cx + 8, 80], fill=TOOL_STEEL, outline=ink, width=int(s * 0.5))
        d.polygon([(cx - 8, 6), (cx + 8, 6), (cx, -4)], fill=TOOL_STEEL)
        d.rectangle([cx - 30, 80, cx + 30, 210], fill=TOOL_YELLOW, outline=ink, width=int(s * 0.7))
        d.rectangle([cx - 30, 150, cx + 30, 166], fill=TOOL_DARK)
        thick_line(d, (cx - 60, 238), (cx + 60, 238), s + 6, ink)
        thick_line(d, (cx - 58, 238), (cx + 58, 238), s, TOOL_DARK)
        thick_line(d, (cx, 210), (cx, 238), s + 6, ink)
        grip = (cx, 238)
    elif name == 'beam':
        # A red steel I-beam, held by one end.
        d.rectangle([cx - 30, 6, cx + 30, h - 30], fill=(176, 64, 52, 255), outline=ink, width=int(s * 0.6))
        d.rectangle([cx - 30, 6, cx - 18, h - 30], fill=TOOL_RED, outline=ink, width=int(s * 0.4))
        d.rectangle([cx + 18, 6, cx + 30, h - 30], fill=TOOL_RED, outline=ink, width=int(s * 0.4))
        for y in range(30, h - 60, 44):
            d.ellipse([cx - 6, y - 6, cx + 6, y + 6], fill=(250, 240, 230, 255), outline=ink)
    elif name == 'sign':
        # A red STOP-style octagon on a pole - the kind of sign a site has everywhere.
        handle(120, TOOL_STEEL)
        import math
        pts = [(cx + 62 * math.cos(math.radians(22.5 + 45 * i)), 64 + 62 * math.sin(math.radians(22.5 + 45 * i)))
               for i in range(8)]
        d.polygon(pts, fill=TOOL_RED, outline=ink)
        d.line(pts + [pts[0]], fill=ink, width=int(s * 0.6))
        inner = [(cx + 50 * math.cos(math.radians(22.5 + 45 * i)), 64 + 50 * math.sin(math.radians(22.5 + 45 * i)))
                 for i in range(8)]
        d.line(inner + [inner[0]], fill=(250, 240, 230, 255), width=int(s * 0.4))
        d.rectangle([cx - 34, 58, cx + 34, 70], fill=(250, 240, 230, 255))
    elif name == 'pan':
        # A frying pan held up by its handle, flambe flames roaring up off it: a black pan with a
        # steel rim, a wooden handle down to the grip, and fire leaping from the pan's face.
        handle(150, TOOL_WOOD, 1.0)
        d.ellipse([cx - 62, 92, cx + 62, 160], fill=(70, 70, 78, 255), outline=ink, width=int(s * 0.7))
        d.ellipse([cx - 50, 100, cx + 50, 150], fill=(112, 114, 124, 255))
        for i, (dx, top, wide) in enumerate([(-34, 30, 20), (0, 4, 26), (34, 26, 20), (-16, 50, 16), (18, 46, 16)]):
            d.polygon([(cx + dx - wide, 118), (cx + dx + wide, 118), (cx + dx + wide * 0.2, top)], fill=(226, 64, 36, 235))
            d.polygon([(cx + dx - wide * 0.6, 118), (cx + dx + wide * 0.6, 118), (cx + dx, top + 24)], fill=(248, 150, 40, 240))
            d.polygon([(cx + dx - wide * 0.3, 118), (cx + dx + wide * 0.3, 118), (cx + dx, top + 46)], fill=(252, 218, 90, 245))
    elif name == 'nailgun':
        # A nail gun, muzzle up: an orange body along the top and a grip sticking out to the
        # side at the bottom, so held in the hand the barrel points the way the arm does.
        img = new_part(150, 190)
        d = ImageDraw.Draw(img)
        cx = 60
        d.rectangle([cx - 20, 10, cx + 20, 130], fill=TOOL_ORANGE, outline=ink, width=int(s * 0.6))
        d.rectangle([cx - 10, 0, cx + 10, 14], fill=TOOL_DARK, outline=ink, width=int(s * 0.4))
        d.rectangle([cx - 20, 60, cx + 20, 74], fill=TOOL_DARK)
        # The magazine of nails running down the front, then the handle.
        d.rectangle([cx + 20, 30, cx + 34, 110], fill=TOOL_STEEL, outline=ink, width=int(s * 0.4))
        d.polygon([(cx - 20, 118), (cx + 16, 118), (cx + 70, 176), (cx + 40, 184)], fill=TOOL_DARK, outline=ink)
        return img, (cx + 34, 160)
    else:
        raise ValueError(name)
    return img, grip


def tool_art(name, ink, stroke):
    """One of Lug's construction-site tools, as a whole picture facing right."""
    if name == 'wheelbarrow':
        img = new_part(300, 170)
        d = ImageDraw.Draw(img)
        # Tray, wheel at the front, handles trailing back toward the fighter.
        d.polygon([(40, 30), (250, 30), (220, 110), (90, 110)], fill=TOOL_ORANGE, outline=ink)
        thick_line(d, (40, 30), (250, 30), stroke * 0.6, ink)
        thick_line(d, (250, 30), (220, 110), stroke * 0.6, ink)
        thick_line(d, (220, 110), (90, 110), stroke * 0.6, ink)
        thick_line(d, (90, 110), (40, 30), stroke * 0.6, ink)
        thick_line(d, (60, 50), (10, 70), stroke * 0.6, ink)
        d.ellipse([196, 104, 256, 164], fill=TOOL_GREY, outline=ink, width=int(stroke * 0.6))
        thick_line(d, (150, 110), (140, 160), stroke * 0.5, ink)
        return img
    if name == 'wings':
        # Two wings made of fire, one each side of his back with a gap between them: each a
        # bird's wing - a curved leading edge sweeping up and out to a point, with long flame
        # feathers hanging off it, red outside, orange, then yellow at the heart. The middle of
        # the picture, where the wings meet his back, is the anchor; the game hinges each wing
        # there and flaps them separately.
        import math
        img = new_part(860, 330)
        d = ImageDraw.Draw(img)
        cx, cy = 430, 240
        layers = [((226, 64, 36, 235), 1.0), ((248, 142, 40, 240), 0.78), ((252, 214, 84, 245), 0.5)]
        for side in (-1, 1):
            root = (cx + side * 18, cy)

            def edge(t):
                # The leading edge: up and out from the root to the wingtip.
                x = root[0] + side * (250 * t)
                y = root[1] - 190 * math.sin(t * math.pi * 0.5) + 40 * t * t
                return (x, y)

            for colour, scale in layers:
                pts = [root]
                feathers = 7
                for k in range(feathers + 1):
                    t = k / float(feathers)
                    ex, ey = edge(t)
                    pts.append((root[0] + (ex - root[0]) * (0.55 + 0.45 * scale), root[1] + (ey - root[1]) * (0.55 + 0.45 * scale)))
                # Back along the trailing edge: a flame feather hanging down and out from each
                # point, longest out at the tip.
                for k in range(feathers, -1, -1):
                    t = k / float(feathers)
                    ex, ey = edge(t)
                    ang = math.radians(90 + side * (-10 - 60 * t))
                    length = (60 + 90 * t) * scale
                    fx = ex + math.cos(ang) * length
                    fy = ey + math.sin(ang) * length * 0.8
                    bx = root[0] + (fx - root[0]) * (0.55 + 0.45 * scale)
                    by = root[1] + (fy - root[1]) * (0.55 + 0.45 * scale)
                    pts.append((bx, by))
                    if k > 0:
                        # The notch between this feather and the next one in.
                        nx, ny = edge((k - 0.5) / float(feathers))
                        pts.append((root[0] + (nx - root[0]) * (0.55 + 0.45 * scale),
                                    root[1] + (ny - root[1] + 40 * scale) * (0.55 + 0.45 * scale)))
                d.polygon(pts, fill=colour)
        return img
    if name == 'wreckingball':
        img = new_part(170, 170)
        d = ImageDraw.Draw(img)
        d.ellipse([12, 12, 158, 158], fill=(98, 104, 120, 255), outline=ink, width=int(stroke * 0.7))
        # Hazard stripes round its middle, and a shackle on top for the cable.
        d.chord([12, 12, 158, 158], 150, 210, fill=TOOL_YELLOW)
        d.chord([12, 12, 158, 158], 330, 30, fill=TOOL_YELLOW)
        d.ellipse([40, 34, 74, 62], fill=(186, 192, 206, 255))
        return img
    if name == 'cone':
        # A traffic cone, standing on its square foot: orange, with two white reflective bands.
        # His down tilt kicks one along the floor for people to trip over.
        img = new_part(130, 150)
        d = ImageDraw.Draw(img)
        top, foot, mid = 10, 124, 65

        def half(y):
            return 12 + 31 * (y - top) / float(foot - top)

        d.polygon([(mid - half(top), top), (mid + half(top), top), (mid + half(foot), foot), (mid - half(foot), foot)],
                  fill=TOOL_ORANGE)
        for y0, y1 in ((42, 60), (82, 100)):
            d.polygon([(mid - half(y0), y0), (mid + half(y0), y0), (mid + half(y1), y1), (mid - half(y1), y1)],
                      fill=(250, 248, 240, 255))
        w = stroke * 0.5
        thick_line(d, (mid - half(top), top), (mid - half(foot), foot), w, ink)
        thick_line(d, (mid + half(top), top), (mid + half(foot), foot), w, ink)
        thick_line(d, (mid - half(top), top), (mid + half(top), top), w, ink)
        d.rectangle([8, foot, 122, 142], fill=TOOL_ORANGE, outline=ink, width=int(w))
        return img
    if name == 'girder':
        # A steel I-beam seen side on: two flanges and a web with rivet holes.
        img = new_part(360, 110)
        d = ImageDraw.Draw(img)
        d.rectangle([10, 10, 350, 30], fill=TOOL_RED, outline=ink, width=int(stroke * 0.5))
        d.rectangle([10, 80, 350, 100], fill=TOOL_RED, outline=ink, width=int(stroke * 0.5))
        d.rectangle([24, 30, 336, 80], fill=(176, 64, 52, 255), outline=ink, width=int(stroke * 0.5))
        for x in range(50, 340, 50):
            d.ellipse([x - 8, 47, x + 8, 63], fill=(250, 240, 230, 255), outline=ink)
        return img
    raise ValueError(name)


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

    if cfg['prop'] and cfg['prop'].get('style') != 'empty':
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
    c = cfg.get('colours') or {}
    # A coloured fighter gets thicker limbs, so there is room for colour inside the outline.
    grow = 1.6 if c else 1.0
    for side in ('Front', 'Back'):
        emit('Arm%s_Upper' % side, limb_part(cfg['upper_arm'], stroke * grow, ink, taper=0.92,
                                            fill=c.get('shirt')))
        emit('Arm%s_Lower' % side, limb_part(cfg['lower_arm'], stroke * 0.92 * grow, ink, taper=0.85,
                                            fill=c.get('shirt'), end_fill=c.get('skin'), end_from=0.7))
        emit('Leg%s_Upper' % side, limb_part(cfg['upper_leg'], stroke * 1.08 * grow, ink, taper=0.90,
                                            fill=c.get('jeans')))
        emit('Leg%s_Lower' % side, limb_part(cfg['lower_leg'], stroke * grow, ink, taper=0.80,
                                            fill=c.get('jeans'), end_fill=c.get('boots'), end_from=0.72))
    if cfg['prop']:
        emit(cfg['prop']['name'], prop_part(cfg))
        if cfg['prop'].get('style') == 'empty':
            parts[cfg['prop']['name']]['empty'] = True

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

    # Extras: drawings that hang off an existing bone and are only shown in certain states. They
    # add no bones - the shared skeleton stays exactly the same.
    extras = []
    if cfg.get('hard_hat'):
        emit('HardHat', hard_hat_part(cfg))
        r = cfg['head_radius']
        neck = 30
        # From the head bone (base of the neck) up to where the brim sits on the skull.
        brim_above_pivot = r + neck - stroke / 2.0 + r * 0.62
        extras.append({'name': 'hardhat', 'bone': 'Head', 'part': 'HardHat',
                       'offset': [round(r * 0.05, 1), round(-brim_above_pivot, 1)]})

    poses = {}
    if cfg.get('tools'):
        poses_dir = os.path.join(root, 'poses')
        if not os.path.isdir(poses_dir):
            os.makedirs(poses_dir)
        for tool in cfg['tools']:
            img = tool_art(tool, ink, stroke)
            img.save(os.path.join(poses_dir, tool + '.png'))
            # Wings hinge where they meet his back - between their roots - not at the middle of
            # the picture, so each one flaps about the right point.
            anchor = [430.0, 240.0] if tool == 'wings' else [img.size[0] / 2.0, img.size[1] / 2.0]
            poses[tool] = {'texture': 'poses/%s.png' % tool, 'anchor': anchor}
        for tool in cfg.get('held_tools', []):
            img, grip = held_tool(tool, ink, stroke)
            name_ = 'tool_' + tool
            img.save(os.path.join(poses_dir, name_ + '.png'))
            poses[name_] = {'texture': 'poses/%s.png' % name_, 'anchor': [round(grip[0], 1), round(grip[1], 1)]}

    rig = {
        'name': name,
        'canonicalHeight': canonical_height,
        'legLength': cfg['upper_leg'] + cfg['lower_leg'],
        'backLimbDarken': 0.70,
        'bones': bones,
        'extras': extras,
        'poses': poses,
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
