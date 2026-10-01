# -*- coding: utf-8 -*-
"""
Extra art for EdgeLord in the same coloured-pencil style as color_edgelord.py: a rack of swords
for his attacks ("infinite swords") and his body alone, turning, for a spin. Run with:

    python tools/art/edgelord_extras.py

Writes fighters/edgelord/draft/swords/<name>.png   one transparent sword each, tip up
       fighters/edgelord/draft/turn/turn_<deg>.png   the body turned that many degrees
       and a *_sheet.png of each set, on paper, to look at.

None of this is his drawing. The swords are ours, drawn to sit beside his - his two sheathed
swords set the look: a straight blade, a brass crossguard, a leather grip and a ring pommel.
The turning frames are his coloured body, squeezed as a flat cutout would be when it turns
(the game's fighters are cutouts, so that is the honest way for him to spin), with his back
filled in plain. `draft/` carries a .gdignore until he is rigged and this art is chosen.
"""

from __future__ import division, print_function

import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

import color_edgelord as ce

OUT = os.path.join(ce.REPO, 'fighters', 'edgelord', 'draft')
PAPER = (244, 241, 234)
IRON = (70, 70, 82)


# --- Swords ---------------------------------------------------------------------------

# Every sword is tip up, built from the same few numbers. Lengths are pixels at the scale of
# his drawing (his own swords are about 30 wide), so they can sit in his hand without resizing.
SWORDS = [
    # name,        blade length, width, profile, tip, curve, guard (w, h, look), grip (w, h), pommel
    ('dagger',      90, 24, 'taper', 0.35, 0.0, (58, 12, 'cross'), (14, 36), ('ball', 7)),
    ('shortsword', 150, 30, 'leaf', 0.22, 0.0, (80, 14, 'cross'), (16, 44), ('ring', 10)),
    ('longsword',  250, 30, 'taper', 0.12, 0.0, (122, 16, 'cross'), (17, 66), ('ring', 12)),
    ('greatsword', 340, 58, 'taper', 0.10, 0.0, (190, 22, 'cross'), (22, 104), ('ring', 17)),
    ('katana',     270, 22, 'straight', 0.09, 0.10, (46, 12, 'tsuba'), (18, 84), ('cap', 9)),
    ('scimitar',   220, 26, 'widen', 0.22, 0.20, (84, 14, 'cross'), (16, 50), ('ball', 8)),
    ('rapier',     290, 10, 'taper', 0.05, 0.0, (72, 10, 'swept'), (14, 56), ('ball', 9)),
    ('edgeblade',  260, 40, 'serrated', 0.16, 0.0, (110, 22, 'spiked'), (18, 64), ('ring', 13)),
    ('claymore',   300, 44, 'taper', 0.12, 0.0, (160, 18, 'claymore'), (20, 110), ('ring', 15)),
    ('broadsword', 230, 52, 'straight', 0.14, 0.0, (112, 18, 'cross'), (18, 60), ('ball', 11)),
]


def blade_outline(cx, base, length, width, profile, tip, curve):
    """The blade as a polygon: left edge up to the tip, right edge back down."""
    left, right, spine = [], [], []
    steps = 48
    for i in range(steps + 1):
        t = i / steps
        y = base - t * length
        c = cx + curve * length * t * t
        if profile == 'taper':
            k = 1.0 - 0.35 * t
        elif profile == 'leaf':
            k = 1.0 + 0.28 * math.sin(math.pi * min(1.0, t / 0.85))
        elif profile == 'widen':
            k = 1.0 + 0.75 * t
        else:
            k = 1.0
        if t > 1.0 - tip:
            k *= (1.0 - t) / tip
        hw = width / 2.0 * k
        l, r = c - hw, c + hw
        if profile == 'serrated' and 0.12 < t < 1.0 - tip:
            r += 6.0 if i % 2 else -2.0
        left.append((l, y))
        right.append((r, y))
        spine.append((c, y))
    return left + right[::-1], spine


def guard_outline(cx, top, w, h, look):
    if look == 'tsuba':
        return None, [cx - w / 2, top - h / 2, cx + w / 2, top + h / 2]
    if look == 'spiked':
        hw = w / 2.0
        return [(cx - hw - 14, top - 16), (cx - hw + 10, top - 4), (cx - 18, top - 8), (cx, top - 2),
                (cx + 18, top - 8), (cx + hw - 10, top - 4), (cx + hw + 14, top - 16),
                (cx + hw - 4, top + h), (cx - hw + 4, top + h)], None
    if look == 'claymore':
        # A claymore's quillons sweep up toward the blade at both ends.
        hw = w / 2.0
        return [(cx - hw, top - h * 1.7), (cx, top), (cx + hw, top - h * 1.7),
                (cx + hw, top - h * 0.5), (cx, top + h), (cx - hw, top - h * 0.5)], None
    hw, sag = w / 2.0, h * 0.35
    return [(cx - hw, top - sag), (cx, top), (cx + hw, top - sag),
            (cx + hw, top + h - sag), (cx, top + h), (cx - hw, top + h - sag)], None


def draw_sword(spec, with_grip=False):
    # with_grip also returns the middle of the grip - where a hand holds it - in the image.
    name, length, width, profile, tip, curve, guard, grip, pommel = spec
    gw, gh, look = guard
    rw, rh = grip
    kind, pr = pommel
    pad = 24
    W = int(max(gw + 40, width * 1.9 + curve * length) + pad * 2)
    H = int(length + gh + rh + pr * 2 + pad * 2 + 10)
    cx = pad + (W - pad * 2) / 2.0 - curve * length * 0.4
    base = pad + length
    size = (W, H)
    canvas = Image.new('RGBA', size, (0, 0, 0, 0))
    texture = ce.pencil_texture(size, 58.0, 101 + len(name), 0.013)
    cross = ce.pencil_texture(size, -32.0, 202 + len(name), 0.010)

    def local(fn):
        # pencil_fill hands shade functions photo coordinates; these swords have no photo.
        return lambda x, y: fn(x - ce.CROP[0], y - ce.CROP[1])

    def fill(mask, fn, outline=True):
        ce.pencil_fill(canvas, mask, texture, cross, local(fn))
        if outline:
            edge = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(7)), mask)
            canvas.paste(Image.new('RGBA', size, ce.INK + (255,)), (0, 0), edge.filter(ImageFilter.GaussianBlur(0.8)))

    def poly(points):
        m = Image.new('L', size, 0)
        ImageDraw.Draw(m).polygon(points, fill=255)
        return m

    def ellipse(box, hole=None):
        m = Image.new('L', size, 0)
        d = ImageDraw.Draw(m)
        d.ellipse(box, fill=255)
        if hole:
            d.ellipse(hole, fill=0)
        return m

    # The blade: a lit half and a shadowed half either side of the ridge, like a cartoon sword.
    outline, spine = blade_outline(cx, base, length, width, profile, tip, curve)

    def steel(x, y):
        t = max(0.0, min(1.0, (base - y) / float(length)))
        c = cx + curve * length * t * t
        if x < c:
            return ce.mix(ce.STEEL_LIGHT, ce.STEEL, min(1.0, (c - x) / (width * 0.5)) * 0.5), 0.0
        return ce.mix(ce.STEEL, ce.STEEL_SHADOW, min(1.0, (x - c) / (width * 0.4))), 0.35
    fill(poly(outline), steel)
    ridge = [p for i, p in enumerate(spine) if 2 <= i <= int(len(spine) * (0.86 - tip))]
    if width >= 20 and len(ridge) > 2:
        ce.sketch_line(canvas, ridge, ce.STEEL_SHADOW, 2, 400 + len(name))

    # The grip, then the guard over the top of it and the blade's base.
    grip_top = base + gh * 0.6
    grip_mask = poly([(cx - rw / 2.0, grip_top), (cx + rw / 2.0, grip_top),
                      (cx + rw / 2.0, grip_top + rh), (cx - rw / 2.0, grip_top + rh)])
    fill(grip_mask, lambda x, y: (ce.LEATHER, 0.4))
    wrap = random.Random(len(name))
    for i in range(int(rh // 9)):
        y = grip_top + 5 + i * 9
        ce.sketch_line(canvas, [(cx - rw / 2.0 + 1, y), (cx + rw / 2.0 - 1, y + 5 + wrap.uniform(-1, 1))],
                       ce.LEATHER_SHADOW, 2, 500 + i)

    pts, box = guard_outline(cx, base, gw, gh, look)
    if box:
        fill(ellipse(box), lambda x, y: (IRON, 0.3))
    else:
        colour = ce.BODY if look == 'spiked' else ce.BRASS
        fill(poly(pts), lambda x, y: (colour, 0.2))
    if look == 'swept':
        # A rapier's hand guard: a brass loop from the crossguard down round the grip.
        loop = [(cx - gw / 2.0 + 6, base + 2)]
        for i in range(1, 13):
            a = math.pi * i / 12
            loop.append((cx - gw * 0.28 * math.cos(a) - 4, base + gh + rh * 0.9 * math.sin(a) * 0.6 + rh * 0.3 * i / 12))
        ce.sketch_line(canvas, loop, ce.INK, 7, 600)
        ce.sketch_line(canvas, loop, ce.BRASS, 3, 601)

    pcy = grip_top + rh + pr - 2
    if kind == 'ring':
        # His pommels are rings, so the swords that sit closest to his are given them too.
        m = ellipse([cx - pr, pcy - pr, cx + pr, pcy + pr], [cx - pr * 0.45, pcy - pr * 0.45, cx + pr * 0.45, pcy + pr * 0.45])
        fill(m, lambda x, y: (ce.BRASS, 0.2))
        hole = ellipse([cx - pr * 0.45, pcy - pr * 0.45, cx + pr * 0.45, pcy + pr * 0.45])
        edge = ImageChops.subtract(hole, hole.filter(ImageFilter.MinFilter(5)))
        canvas.paste(Image.new('RGBA', size, ce.INK + (255,)), (0, 0), edge)
    elif kind == 'cap':
        fill(poly([(cx - rw / 2.0 - 1, pcy - pr), (cx + rw / 2.0 + 1, pcy - pr),
                   (cx + rw / 2.0 - 1, pcy), (cx - rw / 2.0 + 1, pcy)]), lambda x, y: (IRON, 0.3))
    else:
        fill(ellipse([cx - pr, pcy - pr, cx + pr, pcy + pr]), lambda x, y: (ce.BRASS, 0.2))

    box = canvas.getbbox()
    image = canvas.crop(box)
    if with_grip:
        return image, (cx - box[0], grip_top + rh / 2.0 - box[1])
    return image


def draw_axe(with_grip=False):
    """
    A battle axe, head up: a long wooden haft, a broad crescent blade on the front and a small
    spike behind. Drawn in the same pencil as the swords.
    """
    haft_len, haft_w = 300, 18
    pad = 24
    W, H = 190 + pad * 2, haft_len + pad * 2
    cx = pad + 70
    top = pad
    size = (W, H)
    canvas = Image.new('RGBA', size, (0, 0, 0, 0))
    texture = ce.pencil_texture(size, 58.0, 707, 0.013)
    cross = ce.pencil_texture(size, -32.0, 708, 0.010)

    def poly(points):
        m = Image.new('L', size, 0)
        ImageDraw.Draw(m).polygon(points, fill=255)
        return m

    def fill(mask, fn):
        ce.pencil_fill(canvas, mask, texture, cross, lambda x, y: fn(x - ce.CROP[0], y - ce.CROP[1]))
        edge = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(7)), mask)
        canvas.paste(Image.new('RGBA', size, ce.INK + (255,)), (0, 0), edge.filter(ImageFilter.GaussianBlur(0.8)))

    wood = (150, 98, 52)
    fill(poly([(cx - haft_w / 2.0, top + 10), (cx + haft_w / 2.0, top + 10),
               (cx + haft_w / 2.0, top + haft_len), (cx - haft_w / 2.0, top + haft_len)]),
         lambda x, y: (wood, 0.3))

    # The blade: a crescent swelling out in front of the haft, its edge an arc.
    head = [(cx + 6, top + 30)]
    for i in range(0, 21):
        a = math.radians(-70 + 140 * i / 20.0)
        head.append((cx + 34 + 86 * math.cos(a), top + 78 + 70 * math.sin(a)))
    head.append((cx + 6, top + 126))

    def steel(x, y):
        edge = x - (cx + 34)
        return ce.mix(ce.STEEL_LIGHT, ce.STEEL_SHADOW, max(0.0, min(1.0, 1.0 - edge / 86.0))), 0.25
    fill(poly(head), steel)
    fill(poly([(cx - 6, top + 60), (cx - 58, top + 78), (cx - 6, top + 96)]), steel)

    # Leather wrap near the bottom, where it is held.
    grip_top = top + haft_len - 90
    for i in range(8):
        y = grip_top + 6 + i * 10
        ce.sketch_line(canvas, [(cx - haft_w / 2.0 + 1, y), (cx + haft_w / 2.0 - 1, y + 5)], ce.LEATHER_SHADOW, 3, 720 + i)

    box = canvas.getbbox()
    image = canvas.crop(box)
    if with_grip:
        return image, (cx - box[0], grip_top + 45 - box[1])
    return image


# --- Turning ---------------------------------------------------------------------------

TURN_ANGLES = [0, 45, 78, 120, 180]
AXIS_X = ce.HIP[0] - ce.CROP[0]   # he turns about his hip, where his legs meet the V
THICKNESS = 14                    # how thick the cutout is, seen side-on


def turn_frame(degrees):
    theta = math.radians(degrees)
    s = math.cos(theta)
    squeeze = max(abs(s), 0.12)
    ce.LIGHT_SWAPPED[0] = s < 0
    layer = ce.render(limbs=False, face=s >= 0, stretch=1.0 / squeeze)
    ce.LIGHT_SWAPPED[0] = False
    axis = float(AXIS_X)
    if s < 0:
        # Past side-on, what faces us is his back, which is his front the other way round.
        layer = layer.transpose(Image.FLIP_LEFT_RIGHT)
        axis = layer.size[0] - 1 - axis
    size = layer.size
    out_axis = float(AXIS_X)
    # Squeeze toward the turning axis: output x maps back to axis + (x - out_axis) / squeeze.
    warped = layer.transform(size, Image.AFFINE, (1.0 / squeeze, 0, axis - out_axis / squeeze, 0, 1, 0),
                             resample=Image.BICUBIC)
    alpha = warped.split()[3].point(lambda a: 255 if a > 100 else 0)

    canvas = Image.new('RGBA', size, (0, 0, 0, 0))
    texture = ce.pencil_texture(size, 58.0, 900 + degrees, 0.011)
    cross = ce.pencil_texture(size, -32.0, 901 + degrees, 0.008)
    band_px = int(round(THICKNESS * abs(math.sin(theta))))
    if band_px:
        # The cutout's edge, seen as it turns: on the left while his front turns away to the
        # right, on the right once his back comes round.
        dx = -band_px if s >= 0 else band_px
        shifted = ImageChops.offset(alpha, dx, 0)
        solid = ImageChops.lighter(alpha, shifted)
        for step in range(1, band_px):
            solid = ImageChops.lighter(solid, ImageChops.offset(alpha, int(dx * step / band_px), 0))
        band = ImageChops.subtract(solid, alpha)
        ce.pencil_fill(canvas, band.filter(ImageFilter.MaxFilter(3)), texture, cross,
                       lambda x, y: (ce.BODY_SHADOW, 0.5))
        edge = ImageChops.subtract(solid.filter(ImageFilter.MaxFilter(7)), solid)
        canvas.paste(Image.new('RGBA', size, ce.INK + (255,)), (0, 0), edge.filter(ImageFilter.GaussianBlur(0.8)))
        # The crease where the edge meets the face.
        crease = ImageChops.subtract(alpha.filter(ImageFilter.MaxFilter(5)), alpha)
        canvas.paste(Image.new('RGBA', size, ce.INK + (255,)), (0, 0), ImageChops.multiply(crease, solid))
    canvas.alpha_composite(warped)
    # Squeezing thins every upright line, so the outline is drawn again at full weight.
    ring = ImageChops.subtract(alpha.filter(ImageFilter.MaxFilter(7)), alpha)
    canvas.paste(Image.new('RGBA', size, ce.INK + (255,)), (0, 0), ring.filter(ImageFilter.GaussianBlur(0.8)))
    return canvas


def sheet(images, path, gap=30):
    w = sum(i.size[0] for i in images) + gap * (len(images) + 1)
    h = max(i.size[1] for i in images) + gap * 2
    out = Image.new('RGBA', (w, h), PAPER + (255,))
    x = gap
    for im in images:
        out.alpha_composite(im, (x, h - gap - im.size[1]))
        x += im.size[0] + gap
    out.convert('RGB').save(path)


def main():
    for sub in ('swords', 'turn'):
        d = os.path.join(OUT, sub)
        if not os.path.isdir(d):
            os.makedirs(d)
    ignore = os.path.join(OUT, '.gdignore')
    if not os.path.exists(ignore):
        open(ignore, 'w').close()

    swords = []
    for spec in SWORDS:
        im = draw_sword(spec)
        im.save(os.path.join(OUT, 'swords', spec[0] + '.png'))
        swords.append(im)
        print('sword', spec[0], im.size)
    axe = draw_axe()
    axe.save(os.path.join(OUT, 'swords', 'axe.png'))
    swords.append(axe)
    sheet(swords, os.path.join(OUT, 'swords_sheet.png'))

    frames = []
    for deg in TURN_ANGLES:
        frames.append(turn_frame(deg))
        print('turn', deg)
    box = None
    for f in frames:
        b = f.getbbox()
        box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    frames = [f.crop(box) for f in frames]
    for deg, f in zip(TURN_ANGLES, frames):
        f.save(os.path.join(OUT, 'turn', 'turn_%03d.png' % deg))
    sheet(frames, os.path.join(OUT, 'turn_sheet.png'))
    print('wrote', os.path.relpath(OUT, ce.REPO))


if __name__ == '__main__':
    main()
