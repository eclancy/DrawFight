# -*- coding: utf-8 -*-
"""
Colours in EdgeLord - a pencil drawing on grey paper - as if with coloured pencil. Run with the
only Python on this machine:

    python tools/art/color_edgelord.py

Reads  fighters/edgelord/source/edgelord.webp   (the photo: the master copy, never edited)
Writes fighters/edgelord/source/edgelord_colored.png   (transparent background)
       fighters/edgelord/source/edgelord_colored_preview.png   (the same, on paper)

WHAT THIS CHANGES, AND WHY. The project's rule is that a kid's linework is never redrawn. This
script goes further than that on purpose, because it was asked to:
  - the pencil lines are DARKENED and THICKENED (by about two pixels) so they survive being
    shrunk to match size - faint pencil on grey paper vanishes in a fight
  - the two drawn eyes are REPLACED with glowing ones: the scarred eye glows yellow-orange with
    no pupil and an outlined pink scar; the other glows orange-yellow with a small black pupil
    looking forward
  - the grin is coloured as teeth, with a bite line and gaps drawn in
Everything else - the shapes, the swords, the grin, his own shading - is his, coloured over.
Because the photo is kept and this is a script, every one of those choices can be undone or
retuned by editing a number here and running it again.

HOW THE COLOUR GOES ON. His pencil lines are too faint and gappy for a paint-bucket fill, so
each coloured area is a hand-placed shape that follows the inside of his lines (coordinates are
in the original photo's pixels). Colour is laid on with a paper-grain and diagonal-stroke
texture and a cartoon two-tone shade (lit from the top left), then his own lines are drawn back
on top. Colour slightly over or under a line is fine - that is what coloured pencil looks like.
"""

from __future__ import division, print_function

import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
SOURCE = os.path.join(REPO, 'fighters', 'edgelord', 'source')
PHOTO = os.path.join(SOURCE, 'edgelord.webp')

# The character, and nothing else from the page: the name and the labels are outside it.
CROP = (296, 336, 884, 1200)

# --- Colours --------------------------------------------------------------------------

BODY = (206, 38, 38)
BODY_SHADOW = (128, 14, 22)
BODY_LIGHT = (238, 92, 78)
LIMB = (124, 16, 22)
LIMB_SHADOW = (92, 10, 16)
MOUTH = (58, 10, 14)
STEEL = (178, 186, 198)
STEEL_SHADOW = (118, 126, 140)
STEEL_LIGHT = (226, 232, 240)
BRASS = (196, 152, 64)
LEATHER = (122, 78, 40)
LEATHER_SHADOW = (84, 50, 26)
INK = (20, 14, 18)
SCAR = (250, 170, 198)         # healed-over pink: reads against the red, not as part of it
TEETH = (240, 232, 206)
TEETH_SHADOW = (190, 178, 158)
TEETH_LINE = (110, 92, 88)

# --- Shapes (original photo coordinates) --------------------------------------------

BODY_V = [(315, 397), (568, 598), (848, 398), (598, 946), (483, 781), (416, 636)]   # his left edge bows out
MOUTH_TRI = [(531, 798), (630, 684), (614, 784)]

LEFT_LEG = [(502, 896), (476, 960), (452, 1040), (445, 1110), (452, 1186),
            (463, 1150), (470, 1080), (487, 1010), (516, 950), (544, 912)]
RIGHT_LEG = [(606, 910), (628, 880), (668, 970), (712, 1050), (770, 1116), (818, 1178),
             (790, 1152), (735, 1095), (686, 1030), (652, 960)]

SWORD_BLADES = [
    [(454, 488), (506, 488), (516, 562), (484, 566)],          # left sword, above the notch
    [(596, 470), (664, 468), (642, 560), (572, 596)],          # right sword
]
SWORD_GUARDS = [
    [(430, 462), (470, 450), (510, 452), (528, 470), (522, 492), (446, 494), (428, 482)],
    [(592, 438), (694, 434), (678, 474), (640, 454)],
    [(554, 492), (642, 498), (638, 522), (550, 514)],
]
SWORD_GRIPS = [
    [(462, 412), (480, 412), (482, 454), (462, 454)],
    [(628, 360), (646, 360), (646, 438), (628, 438)],
]
POMMELS = [((470, 405), 12), ((637, 352), 12)]

BELT = [(470, 820), (506, 838), (575, 836), (640, 828), (700, 812), (704, 830),
        (650, 850), (575, 862), (506, 866), (470, 848)]
DAGGER = [(680, 792), (694, 792), (694, 852), (680, 852)]
DAGGER_POMMEL = ((712, 807), 9)

# The arms are single thick pencil strokes, so they are coloured by thickening his own stroke
# inside these regions rather than by filling a shape.
LEFT_ARM_ZONE = [(330, 698), (432, 698), (414, 760), (446, 1012), (330, 1012)]
RIGHT_ARM_ZONE = [(772, 606), (874, 606), (874, 806), (742, 818), (740, 760), (748, 690), (762, 640)]

# Where his legs meet his body: the rig's hip, and the point he turns about when he spins.
HIP = (571, 901)

# Pencil marks to leave out: the ends of the label pointers, and the drawn eyes being replaced.
ERASE = [
    [(296, 336), (344, 336), (344, 392), (296, 392)],          # "Super speed" pointer end
    [(668, 336), (730, 336), (730, 378), (668, 378)],          # "Infinite swords" pointer end
    [(440, 585), (530, 585), (530, 712), (440, 712)],          # scarred eye and scar (redrawn below)
    [(580, 592), (660, 592), (660, 676), (580, 676)],          # other eye (redrawn below)
]

# Eyes, in the character's terms: his right eye is on the viewer's left.
RIGHT_EYE = {'centre': (483, 641), 'angle': 38.0, 'length': 90.0, 'height': 28.0, 'pupil': False}
LEFT_EYE = {'centre': (619, 635), 'angle': -47.0, 'length': 84.0, 'height': 28.0, 'pupil': True}
SCAR_LINE = [(485, 582), (490, 716)]
# Where the pupil sits along the eye, -1 to 1: negative looks toward the viewer's left, which
# is the way he faces in a fight.
PUPIL_LOOK = (-0.42, -0.15)

# The scar he drew on his upper left forehead - an outlined jag. His outline stays; the inside
# is coloured the same pink as the eye scar.
FOREHEAD_SCAR = [(718, 504), (724, 512), (708, 545), (714, 560), (690, 606), (697, 568), (700, 548)]

# A pencil stroke is any run of pixels at least LINE_WEAK darker than the paper that contains
# at least one pixel LINE_STRONG darker. That keeps every real stroke, faint ends included, and
# drops paper grain and smudges, which never get that dark.
LINE_WEAK = 17
LINE_STRONG = 42
LINE_MIN_PIXELS = 30
LINE_GROW = 1.6          # how much each line is thickened, in pixels


# --- Helpers ----------------------------------------------------------------------------

def shift(points):
    return [(x - CROP[0], y - CROP[1]) for x, y in points]


def poly_mask(size, polygons):
    mask = Image.new('L', size, 0)
    d = ImageDraw.Draw(mask)
    for poly in polygons:
        d.polygon(shift(poly), fill=255)
    return mask


def circle_mask(size, circles):
    mask = Image.new('L', size, 0)
    d = ImageDraw.Draw(mask)
    for (cx, cy), r in circles:
        cx, cy = cx - CROP[0], cy - CROP[1]
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    return mask


def strokes_only(strength):
    """His strokes, as a 0/255 mask, with paper grain and specks removed."""
    w, h = strength.size
    src = strength.load()
    out = Image.new('L', (w, h), 0)
    dst = out.load()
    seen = bytearray(w * h)
    for y in range(h):
        for x in range(w):
            if seen[y * w + x] or src[x, y] < LINE_WEAK:
                continue
            stack = [(x, y)]
            seen[y * w + x] = 1
            group = []
            strong = False
            while stack:
                cx, cy = stack.pop()
                group.append((cx, cy))
                if src[cx, cy] >= LINE_STRONG:
                    strong = True
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx] and src[nx, ny] >= LINE_WEAK:
                        seen[ny * w + nx] = 1
                        stack.append((nx, ny))
            if strong and len(group) >= LINE_MIN_PIXELS:
                for gx, gy in group:
                    dst[gx, gy] = 255
    return out


def grow(mask, radius):
    """Thickens a mask smoothly - a blur and a threshold, so edges stay round, not blocky."""
    return mask.filter(ImageFilter.GaussianBlur(radius)).point(lambda v: 255 if v > 50 else 0)


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


PAPER = (246, 242, 234)


def pencil_texture(size, angle, seed, density):
    """
    Coloured-pencil strokes: many short parallel marks at one angle, with gaps between them
    where the paper shows. Hashed from a seed, so the texture is the same on every run.
    """
    import random
    rng = random.Random(seed)
    tex = Image.new('L', size, 0)
    d = ImageDraw.Draw(tex)
    w, h = size
    count = int(w * h * density)
    for _ in range(count):
        x = rng.uniform(-20, w + 20)
        y = rng.uniform(-20, h + 20)
        a = math.radians(angle + rng.uniform(-9, 9))
        length = rng.uniform(12, 34)
        dx, dy = math.cos(a) * length / 2.0, -math.sin(a) * length / 2.0
        d.line([(x - dx, y - dy), (x + dx, y + dy)], fill=rng.randint(150, 255), width=2)
    return tex.filter(ImageFilter.GaussianBlur(0.6))


def flood_region(size, polygon_mask, barrier):
    """
    The coloured area: flooded out from inside the traced shape, stopping at his lines. Where
    his line has a gap, the traced shape (grown a little) is the wall instead. So the colour
    meets his lines wherever they exist, and only falls back to the traced shape at a gap.
    """
    w, h = size
    grown = polygon_mask.filter(ImageFilter.MaxFilter(17))
    # Seeds come from well inside the shape - but thin shapes (a blade, a leg) have no 'well
    # inside', so fall back to shrinking less.
    shrunk = polygon_mask.filter(ImageFilter.MinFilter(25))
    if not shrunk.getbbox():
        shrunk = polygon_mask.filter(ImageFilter.MinFilter(7))
    if not shrunk.getbbox():
        shrunk = polygon_mask
    ring = ImageChops.subtract(grown, grown.filter(ImageFilter.MinFilter(3)))
    wall = ImageChops.lighter(barrier, ring)

    g = grown.load()
    s = shrunk.load()
    b = wall.load()
    out = Image.new('L', size, 0)
    o = out.load()
    stack = []
    for y in range(0, h, 5):
        for x in range(0, w, 5):
            if s[x, y] > 128 and b[x, y] < 128:
                stack.append((x, y))
    while stack:
        x, y = stack.pop()
        if o[x, y] or b[x, y] > 128 or g[x, y] < 128:
            continue
        o[x, y] = 255
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not o[nx, ny]:
                stack.append((nx, ny))
    # Close the slivers between his doubled strokes, then tuck the colour a couple of pixels
    # under his lines, so no paper shows at the join.
    out = out.filter(ImageFilter.MaxFilter(17)).filter(ImageFilter.MinFilter(17))
    # A shape so thin his two strokes nearly touch leaves the flood nowhere to go; there, the
    # traced shape is the better guess.
    area = lambda m: sum(m.histogram()[128:])
    if area(out) < 0.5 * area(polygon_mask):
        out = ImageChops.lighter(out, polygon_mask)
    return ImageChops.multiply(out.filter(ImageFilter.MaxFilter(5)), grown)


def pencil_fill(canvas, mask, texture, cross, shade_fn):
    """
    Lays colour into mask as coloured pencil: strokes of colour over paper, with the paper
    showing between them, soft at the edges, and cross-hatched where it is in shadow.
    """
    px = canvas.load()
    soft = mask.filter(ImageFilter.GaussianBlur(1.4)).load()
    t = texture.load()
    c = cross.load()
    w, h = canvas.size
    for y in range(h):
        for x in range(w):
            edge = soft[x, y]
            if edge < 8:
                continue
            colour, shadow = shade_fn(x + CROP[0], y + CROP[1])
            # Strokes of colour with paper between them: heavier strokes also press darker.
            k = t[x, y] / 255.0
            coverage = 0.58 + 0.42 * k
            colour = (colour[0] * (1.08 - 0.16 * k), colour[1] * (1.08 - 0.16 * k), colour[2] * (1.08 - 0.16 * k))
            if shadow:
                colour = mix(colour, (colour[0] * 0.55, colour[1] * 0.55, colour[2] * 0.55), c[x, y] / 255.0 * shadow)
            r = PAPER[0] + (colour[0] - PAPER[0]) * coverage
            g = PAPER[1] + (colour[1] - PAPER[1]) * coverage
            b = PAPER[2] + (colour[2] - PAPER[2]) * coverage
            old = px[x, y]
            a = edge / 255.0
            if old[3] > 0:
                # Layered over colour already there: blend, so edges stay soft.
                r, g, b = old[0] + (r - old[0]) * a, old[1] + (g - old[1]) * a, old[2] + (b - old[2]) * a
                px[x, y] = (int(r), int(g), int(b), 255)
            else:
                px[x, y] = (int(r), int(g), int(b), int(edge))


def side_of_line(p, a, b):
    return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])


def distance_to_segment(p, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((p[0] - ax) * dx + (p[1] - ay) * dy) / float(dx * dx + dy * dy)))
    return math.hypot(p[0] - (ax + t * dx), p[1] - (ay + t * dy))


# The turning frames draw his back, which is his front mirrored - so to keep the light coming
# from the same side of the screen, the back is shaded with the sides swapped.
LIGHT_SWAPPED = [False]


def body_shade(x, y):
    """
    Cartoon two-tone, lit from the top RIGHT: the left blade of the V is in shadow, with a
    lit rim down the right edge. Returns (colour, how much shadow cross-hatching to add).
    """
    notch, bottom = (568, 598), (598, 946)
    in_shadow = side_of_line((x, y), notch, bottom) > 0
    if LIGHT_SWAPPED[0]:
        in_shadow = not in_shadow
    if in_shadow:
        edge = distance_to_segment((x, y), notch, bottom)
        k = min(1.0, edge / 18.0)
        return mix(BODY, BODY_SHADOW, k), k
    rim_edge = ((315, 397), (598, 946)) if LIGHT_SWAPPED[0] else ((848, 398), (598, 946))
    rim = distance_to_segment((x, y), rim_edge[0], rim_edge[1])
    if rim < 22:
        return mix(BODY_LIGHT, BODY, rim / 22.0), 0.0
    return BODY, 0.0


def limb_shade(x, y):
    return LIMB, 0.6


def flat(colour, shadow=0.0):
    return lambda x, y: (colour, shadow)


def steel_shade(x, y):
    t = ((x * 0.7 + y * 0.3) % 40) / 40.0
    return mix(STEEL_LIGHT, STEEL_SHADOW, t), 0.0


def leather_shade(x, y):
    return LEATHER, 0.4


def teeth_shade(x, y):
    # Teeth sit back in the mouth: shaded toward the top edge, where the lip overhangs them.
    a, b = MOUTH_TRI[0], MOUTH_TRI[1]
    k = max(0.0, 1.0 - distance_to_segment((x, y), a, b) / 16.0)
    return mix(TEETH, TEETH_SHADOW, k), 0.0


def sketch_line(canvas, points, colour, width, seed, closed=False):
    """
    A line drawn the way a pencil draws one: two slightly different passes over the same path,
    softened, so it never looks ruled.
    """
    import random
    rng = random.Random(seed)
    mask = Image.new('L', canvas.size, 0)
    d = ImageDraw.Draw(mask)
    path = list(points) + ([points[0]] if closed else [])
    for passes, w in ((0, width), (1, max(1, width - 1))):
        jittered = [(x + rng.uniform(-1.0, 1.0) * passes, y + rng.uniform(-1.0, 1.0) * passes) for x, y in path]
        d.line(jittered, fill=255, width=w)
        for x, y in jittered:
            r = w / 2.0 - 0.5
            d.ellipse([x - r, y - r, x + r, y + r], fill=255)
    canvas.paste(Image.new('RGBA', canvas.size, colour + (255,)), (0, 0), mask.filter(ImageFilter.GaussianBlur(0.7)))


def draw_teeth(canvas):
    """A zigzag bite across the grin and a few gaps between teeth, clipped to the mouth."""
    size = canvas.size
    mouth = poly_mask(size, [MOUTH_TRI]).filter(ImageFilter.MinFilter(3))
    layer = Image.new('RGBA', size, (0, 0, 0, 0))
    (ax, ay), (bx, by), (cx, cy) = shift(MOUTH_TRI)
    mx, my = (bx + cx) / 2.0, (by + cy) / 2.0
    length = math.hypot(mx - ax, my - ay)
    nx, ny = -(my - ay) / length, (mx - ax) / length
    bite = []
    for i in range(9):
        t = i / 8.0
        z = 4.0 if i % 2 else -4.0
        bite.append((ax + (mx - ax) * t + nx * z, ay + (my - ay) * t + ny * z))
    sketch_line(layer, bite, TEETH_LINE, 2, 71)
    for i, t in enumerate((0.3, 0.5, 0.7, 0.88)):
        px, py = ax + (mx - ax) * t, ay + (my - ay) * t
        sketch_line(layer, [(px - nx * 40, py - ny * 40), (px + nx * 40, py + ny * 40)], TEETH_LINE, 2, 80 + i)
    alpha = ImageChops.multiply(layer.split()[3], mouth)
    layer.putalpha(alpha)
    canvas.alpha_composite(layer)


def eye_frame(eye):
    cx, cy = eye['centre'][0] - CROP[0], eye['centre'][1] - CROP[1]
    ang = math.radians(eye['angle'])
    return cx, cy, math.cos(ang), math.sin(ang), eye['length'] / 2.0, eye['height'] / 2.0


def inside_eye(eye, x, y, grow=1.0):
    cx, cy, ca, sa, half_l, half_h = eye_frame(eye)
    dx, dy = x - cx, y - cy
    u = (dx * ca + dy * sa) / (half_l * grow)
    v = (-dx * sa + dy * ca) / (half_h * grow)
    return abs(u) <= 1.0 and abs(v) <= 1.0 - u * u


def draw_eye(canvas, eye, texture):
    """
    A glowing almond, coloured in pencil like everything else: a soft halo, a hot centre with
    the strokes showing, a sketched rim, and maybe a pupil looking the way he faces.
    """
    cx, cy, ca, sa, half_l, half_h = eye_frame(eye)
    px = canvas.load()
    tex = texture.load()
    w, h = canvas.size
    reach = int(half_l * 1.9)
    for y in range(max(0, int(cy - reach)), min(h, int(cy + reach))):
        for x in range(max(0, int(cx - reach)), min(w, int(cx + reach))):
            dx, dy = x - cx, y - cy
            u = (dx * ca + dy * sa) / half_l
            v = (-dx * sa + dy * ca) / half_h
            inside = abs(u) <= 1.0 and abs(v) <= 1.0 - u * u
            r = (u * u + v * v * 0.35) ** 0.5
            base = px[x, y]
            if inside:
                t = min(1.0, (u * u + v * v) ** 0.5)
                glow = mix((255, 246, 170), (255, 150, 30), t)
                stroke = 1.0 - tex[x, y] / 255.0
                px[x, y] = mix(glow, (226, 104, 18), stroke * 0.45) + (255,)
            elif r < 1.9 and base[3] > 0:
                glow = max(0.0, 1.0 - (r - 1.0) / 0.9) * 0.55
                px[x, y] = mix(base[:3], (255, 170, 40), glow) + (base[3],)

    outline = []
    for i in range(41):
        u = -1.0 + 2.0 * i / 40
        outline.append((u, 1.0 - u * u))
    for i in range(41):
        u = 1.0 - 2.0 * i / 40
        outline.append((u, -(1.0 - u * u)))
    outline = [(cx + (u * half_l) * ca - (v * half_h) * sa, cy + (u * half_l) * sa + (v * half_h) * ca)
               for u, v in outline]
    sketch_line(canvas, outline, INK, 3, int(cx * 7 + cy))
    if eye['pupil']:
        u, v = PUPIL_LOOK
        pcx = cx + u * half_l * ca - v * half_h * sa
        pcy = cy + u * half_l * sa + v * half_h * ca
        # Scribbled in, not stamped: a few small overlapping round strokes.
        import random
        rng = random.Random(5)
        mask = Image.new('L', canvas.size, 0)
        d = ImageDraw.Draw(mask)
        for _ in range(7):
            ox, oy, r = rng.uniform(-1.6, 1.6), rng.uniform(-1.6, 1.6), rng.uniform(4.2, 5.8)
            d.ellipse([pcx + ox - r, pcy + oy - r, pcx + ox + r, pcy + oy + r], fill=255)
        mask = ImageChops.multiply(mask.filter(ImageFilter.GaussianBlur(0.8)),
                                   texture.point(lambda k: 190 + k // 4))
        canvas.paste(Image.new('RGBA', canvas.size, (10, 8, 10, 255)), (0, 0), mask)


def draw_scar(canvas, eye):
    """
    The scar, pink with a black outline, above and below the eye - never across the eye
    itself. Outlined like everything else he drew, so it reads as a mark on the skin.
    """
    (x0, y0), (x1, y1) = SCAR_LINE
    steps = 60
    points = [(x0 + (x1 - x0) * i / float(steps) - CROP[0], y0 + (y1 - y0) * i / float(steps) - CROP[1])
              for i in range(steps + 1)]
    clear = [not inside_eye(eye, x, y, grow=1.3) for x, y in points]
    runs, run = [], []
    for i in range(steps + 1):
        if clear[i]:
            run.append(points[i])
        elif run:
            runs.append(run)
            run = []
    if run:
        runs.append(run)
    ticks = []
    for i in (8, 16, 46, 54):
        if clear[i]:
            sx, sy = points[i]
            ticks.append([(sx - 9, sy + 1), (sx + 9, sy - 1)])
    # Ink first, wider; the pink over it, narrower - the difference is the outline.
    for k, run in enumerate(runs):
        sketch_line(canvas, run, INK, 12, 300 + k)
    for k, tick in enumerate(ticks):
        sketch_line(canvas, tick, INK, 8, 310 + k)
    for k, run in enumerate(runs):
        sketch_line(canvas, run, SCAR, 6, 320 + k)
    for k, tick in enumerate(ticks):
        sketch_line(canvas, tick, SCAR, 3, 330 + k)


def render(limbs=True, face=True, stretch=1.0, masks=None):
    """
    Colours him in and returns the RGBA image. limbs=False leaves out the arms, legs and swords
    (the body alone); face=False also leaves out the face and every line of his inside the
    outline (his back). stretch widens the pencil texture, for a frame that will be squeezed
    narrower afterwards, so the strokes come out the normal shape.

    Pass a dict as masks and it is filled with where each piece ended up - 'body', 'leg_left',
    'leg_right', 'arm_left', 'arm_right' - which is what cut_edgelord.py cuts the rig along.
    """
    if masks is None:
        masks = {}
    photo = Image.open(PHOTO).convert('RGB').crop(CROP)
    size = photo.size
    grey = photo.convert('L')
    paper = grey.filter(ImageFilter.BoxBlur(30))
    strength = ImageChops.subtract(paper, grey)            # how much darker than the paper

    # His lines, first: they are the walls the colour floods up against.
    strokes = ImageChops.subtract(strokes_only(strength), poly_mask(size, ERASE))
    if face:
        # He shaded the inside of the grin dark; those strokes would sit on top of the teeth,
        # so inside the mouth only its outline is kept.
        inside_mouth = poly_mask(size, [MOUTH_TRI]).filter(ImageFilter.MinFilter(13))
        strokes = ImageChops.subtract(strokes, inside_mouth)
    arms = poly_mask(size, [LEFT_ARM_ZONE, RIGHT_ARM_ZONE])
    wall = grow(strokes, 1.2)

    wide = (int(size[0] * stretch), size[1])
    texture = pencil_texture(wide, 58.0, 11, 0.011).resize(size, Image.BILINEAR)
    cross = pencil_texture(wide, -32.0, 23, 0.008).resize(size, Image.BILINEAR)
    canvas = Image.new('RGBA', size, (0, 0, 0, 0))

    def colour_in(polygons, shade_fn, circles=None):
        shape = circle_mask(size, circles) if circles else poly_mask(size, polygons)
        region = flood_region(size, shape, wall)
        pencil_fill(canvas, region, texture, cross, shade_fn)
        # A continuous outline round every coloured shape, so there are no gaps where his
        # pencil was faint - drawn now, so anything coloured over this later covers it.
        edge = ImageChops.subtract(region.filter(ImageFilter.MaxFilter(7)), region)
        canvas.paste(Image.new('RGBA', size, INK + (255,)), (0, 0), edge.filter(ImageFilter.GaussianBlur(0.8)))
        return region

    # Behind the body first: legs, swords. Then the body and what sits on it.
    if limbs:
        masks['leg_left'] = colour_in([LEFT_LEG], limb_shade)
        masks['leg_right'] = colour_in([RIGHT_LEG], limb_shade)
        colour_in(SWORD_BLADES, steel_shade)
        colour_in(SWORD_GRIPS, leather_shade)
        colour_in(SWORD_GUARDS, flat(BRASS))
        colour_in(None, flat(BRASS), circles=POMMELS)
    masks['body'] = colour_in([BODY_V], body_shade)
    body_only = canvas.split()[3]
    if face:
        colour_in([MOUTH_TRI], teeth_shade)
        draw_teeth(canvas)
    colour_in([BELT], leather_shade)
    if limbs:
        colour_in([DAGGER], steel_shade)
        colour_in(None, flat(BRASS), circles=[DAGGER_POMMEL])

    if not face:
        return canvas

    # His lines, darkened and thickened, only on or next to the character.
    if limbs:
        keep = ImageChops.lighter(canvas.split()[3].filter(ImageFilter.MaxFilter(25)), arms)
    else:
        keep = ImageChops.subtract(body_only.filter(ImageFilter.MinFilter(3)), arms)
    kept = ImageChops.multiply(strokes, keep)
    lines = grow(kept, LINE_GROW)

    if limbs:
        # The arms: his thick strokes, coloured dark red with an ink edge around them.
        arm_core = ImageChops.multiply(strokes, arms)
        arm_fill = grow(arm_core, 3.0)
        arm_edge = grow(arm_fill, 2.2)
        canvas.paste(Image.new('RGBA', size, INK + (255,)), (0, 0), arm_edge)
        pencil_fill(canvas, arm_fill, texture, cross, limb_shade)
        for side, zone in (('arm_left', LEFT_ARM_ZONE), ('arm_right', RIGHT_ARM_ZONE)):
            masks[side] = ImageChops.multiply(arm_edge, poly_mask(size, [zone]).filter(ImageFilter.MaxFilter(9)))

    # Everything else: his pencil, as dark ink, with slightly soft edges.
    scar_zone = poly_mask(size, [FOREHEAD_SCAR]).filter(ImageFilter.MaxFilter(21))
    other = ImageChops.subtract(ImageChops.subtract(lines, arms), scar_zone)
    canvas.paste(Image.new('RGBA', size, INK + (255,)), (0, 0), other.filter(ImageFilter.GaussianBlur(0.7)))

    # The forehead scar is a few heavy strokes of his: coloured like the arms - his strokes,
    # thickened, in the scar pink, with an ink edge round them.
    scar_core = ImageChops.multiply(strokes, scar_zone)
    scar_fill = grow(scar_core, 2.6)
    canvas.paste(Image.new('RGBA', size, INK + (255,)), (0, 0), grow(scar_fill, 3.4))
    pencil_fill(canvas, scar_fill, texture, cross, flat(SCAR))

    # The new eyes, then the scar around - not across - the right one.
    draw_eye(canvas, RIGHT_EYE, texture)
    draw_eye(canvas, LEFT_EYE, texture)
    draw_scar(canvas, RIGHT_EYE)
    return canvas


def main():
    canvas = render()
    size = canvas.size
    out = os.path.join(SOURCE, 'edgelord_colored.png')
    canvas.save(out)
    preview = Image.new('RGBA', size, (244, 241, 234, 255))
    preview.alpha_composite(canvas)
    preview.convert('RGB').save(os.path.join(SOURCE, 'edgelord_colored_preview.png'))
    print('wrote %s  %dx%d' % (os.path.relpath(out, REPO), size[0], size[1]))


if __name__ == '__main__':
    main()
