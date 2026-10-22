# -*- coding: utf-8 -*-
"""
Draws the thirty items - everything a fighter can pick up - in the coloured pencil of Lugnut's
and Flambe's tools:

    python tools/art/items.py

Needs $GODOT_BIN, which rasterises the SVG (drawn.py); takes about 10 seconds.

Every item is drawn as SVG at one drawing unit to one pixel, coloured in pencil inside a dark
indigo outline, and written trimmed to assets/items/<id>.png. Items are not characters, so
nothing here is black: the outline is indigo and every fill stays well above the 30% value line
(.ai/art-direction.md) - an ink bottle's ink and a marker's cap are deep blue and purple. The
outline is there because an item is real: it can hit you. Detail stays bold, like Lugnut's, so it
holds when the game shrinks a drawing to a third of its size.

assets/items/items.json says, for each drawing, in pixels of its PNG:
    grip  where a hand holds it - or, for something only thrown, placed or eaten, its centre
    head  its business end (the pencil point, the roller drum, the stamp face, the lens...), or
          its centre when it has none
A held item is drawn UPRIGHT, grip at the bottom and business end at the top, so the game can
hang it from a fist and point it along the forearm. Anything else is drawn as it stands on the
floor. Some items have extra pieces, each its own entry: pencil_stub, scissors_open, the four
loose crayons (crayon_red ...), tack, popup_book_open.
"""

from __future__ import division, print_function

import colorsys
import json
import math
import os
import random
import time

from drawn import REPO, draw_parts

OUT = os.path.join(REPO, 'assets', 'items')

# The ink is indigo, not black: characters own black. Every fill is bright and light.
C = dict(
    ink='#34386e',
    white='#fdfcf7', paper='#fdfcf7', paper_shade='#e3e6ef', rule='#9cc0e8', margin='#f19cb0',
    yellow='#f8cd38', yellow_shade='#e8b02a', yellow_light='#fde27a',
    wood='#f0cc96', tan='#e6bd7c', wood_dark='#cf9b5a', knob='#e2ac68',
    graphite='#7b80a8',
    pink='#f49ab3', pink_light='#f9c3d2', pink_dark='#e27b9b',
    silver='#c9ced9', silver_dark='#a3aabb', silver_light='#e6e9f0',
    red='#e5493d', red_dark='#c23a33', red_light='#f6a59b',
    orange='#f88c25',
    blue='#3f82dc', blue_light='#a9c9f3', sky='#9cc8f0',
    deep_blue='#3550c0', deep_blue_light='#7088dc',
    green='#40ad58', green_light='#a8deb2', bright_green='#4cc95e',
    purple='#9058d0', purple_light='#c4a3ee', violet='#a66ae0',
    paint_pink='#f06aa8', glitter='#f483c6',
    teal='#2fb5ad', teal_dark='#22898a', teal_light='#93ddd6',
    cream='#f7efd8', cream_shade='#e6d9b6',
    glass='#d6ecf7', lens='#bfe2f5', box_tint='#bfe3f5',
    cork='#d9ab6c', cardboard='#ecd8aa', door='#cf9a5e', roof='#9a6ad6', window='#9cc8f0',
    gold='#ffc62a', gold_light='#ffe57a',
    murky1='#c9a9c9', murky2='#d8c690', murky3='#a9c6a2', murky4='#a0b6d6',
    balloon='#f2582c',
    trace='#dbe5f1', trace_back='#c3d0e4', trace_line='#8d9fc6',
    canvas_side='#e4dcc8', foam='#e6e7ee',
    wo=6, wi=4,
)

MARGIN = 30.0


def fill(svg, colours=C):
    return svg.format(**colours)


# --- drawing helpers ---------------------------------------------------------------------------

def num(v):
    return ('%.1f' % v).rstrip('0').rstrip('.')


def P(d, colour, w='wo', extra=''):
    """A shape, coloured, inside the outline."""
    return ('<path d="%s" fill="{%s}" stroke="{ink}" stroke-width="{%s}" stroke-linejoin="round" '
            'stroke-linecap="round"%s/>\n' % (d, colour, w, extra))


def F(d, colour, extra=''):
    """Colour with no outline."""
    return '<path d="%s" fill="{%s}"%s/>\n' % (d, colour, extra)


def L(d, w='wi', colour='ink', extra=''):
    """A line: ink unless coloured. w is a palette key or a number."""
    width = '{%s}' % w if isinstance(w, str) else num(w)
    return ('<path d="%s" fill="none" stroke="{%s}" stroke-width="%s" stroke-linecap="round" '
            'stroke-linejoin="round"%s/>\n' % (d, colour, width, extra))


def tube(d, colour, width, rim=6):
    """A wire or a stick: a coloured stroke inside an ink one."""
    return L(d, width + 2 * rim) + L(d, width, colour)


def clip(name, d, body):
    return '<clipPath id="%s"><path d="%s"/></clipPath><g clip-path="url(#%s)">\n%s</g>\n' % (name, d, name, body)


def rect(x0, y0, x1, y1, r=0.0):
    if r <= 0:
        return 'M %s,%s L %s,%s L %s,%s L %s,%s Z' % tuple(num(v) for v in (x0, y0, x1, y0, x1, y1, x0, y1))
    v = [num(a) for a in (x0 + r, y0, x1 - r, y0, x1, y0, x1, y0 + r, x1, y1 - r, x1, y1, x1 - r, y1,
                          x0 + r, y1, x0, y1, x0, y1 - r, x0, y0 + r, x0, y0, x0 + r, y0)]
    return ('M %s,%s L %s,%s Q %s,%s %s,%s L %s,%s Q %s,%s %s,%s L %s,%s Q %s,%s %s,%s L %s,%s Q %s,%s %s,%s Z'
            % tuple(v))


def ellipse(cx, cy, rx, ry=None):
    ry = rx if ry is None else ry
    return 'M %s,%s A %s,%s 0 1 0 %s,%s A %s,%s 0 1 0 %s,%s Z' % tuple(
        num(v) for v in (cx - rx, cy, rx, ry, cx + rx, cy, rx, ry, cx - rx, cy))


def poly(points, closed=True):
    d = 'M ' + ' L '.join('%s,%s' % (num(x), num(y)) for x, y in points)
    return d + (' Z' if closed else '')


def star(cx, cy, ro, ri, n=5, turn=-90.0):
    pts = []
    for i in range(2 * n):
        a = math.radians(turn + i * 180.0 / n)
        r = ro if i % 2 == 0 else ri
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return poly(pts)


def sparkle(cx, cy, r):
    """A four-point twinkle, pinched in at the waist."""
    k = 0.22 * r
    return ('M %s,%s Q %s,%s %s,%s Q %s,%s %s,%s Q %s,%s %s,%s Q %s,%s %s,%s Z' % tuple(num(v) for v in (
        cx, cy - r, cx + k, cy - k, cx + r, cy, cx + k, cy + k, cx, cy + r, cx - k, cy + k, cx - r, cy,
        cx - k, cy - k, cx, cy - r)))


def smooth(points):
    """A closed curve through the points (Catmull-Rom as cubic Beziers)."""
    n = len(points)
    d = 'M %s,%s' % (num(points[0][0]), num(points[0][1]))
    for i in range(n):
        p0, p1, p2, p3 = points[i - 1], points[i], points[(i + 1) % n], points[(i + 2) % n]
        c1 = (p1[0] + (p2[0] - p0[0]) / 6.0, p1[1] + (p2[1] - p0[1]) / 6.0)
        c2 = (p2[0] - (p3[0] - p1[0]) / 6.0, p2[1] - (p3[1] - p1[1]) / 6.0)
        d += ' C %s,%s %s,%s %s,%s' % tuple(num(v) for v in (c1[0], c1[1], c2[0], c2[1], p2[0], p2[1]))
    return d + ' Z'


def blob(cx, cy, r, seed, lumps=7, wobble=0.18):
    """A lumpy dab of paint, the same every time."""
    rnd = random.Random(seed)
    pts = []
    for i in range(lumps):
        a = 2 * math.pi * i / lumps + rnd.uniform(-0.2, 0.2)
        rr = r * (1.0 - wobble + rnd.uniform(0, 2 * wobble))
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    return smooth(pts)


def turned(p, deg):
    """A point turned deg about the origin, the way SVG rotate() turns it (clockwise on screen)."""
    a = math.radians(deg)
    return (p[0] * math.cos(a) - p[1] * math.sin(a), p[0] * math.sin(a) + p[1] * math.cos(a))


# Digits as marker strokes, since Godot's SVG renderer has no fonts. A 12 x 20 cell from (x, y).
DIGITS = {
    '1': 'M {a},{b} L {c},{y} L {c},{e}',
    '2': 'M {x},{b} C {x},{yo} {r},{yo} {r},{f} C {r},{g} {x},{h} {x},{e} L {r},{e}',
    '3': 'M {x},{y1} L {r},{y1} L {c},{i} C {r1},{i} {r2},{e} {m},{e} C {d},{e} {x},{e1} {x},{h1}',
    '4': 'M {n},{e} L {n},{y} L {x},{j} L {r1},{j}',
    '5': 'M {r},{y} L {a},{y} L {x1},{i} C {k},{f} {r1},{g} {r},{h1} C {k2},{e} {d},{e2} {x},{k3}',
}


def digit(ch, x, y):
    v = dict(x=x, y=y, a=x + 2, b=y + 5, c=x + 7, e=y + 20, yo=y - 1, r=x + 12, f=y + 6, g=y + 11,
             h=y + 15, y1=y + 1, i=y + 8, r1=x + 13, r2=x + 14, m=x + 6, d=x + 3, e1=y + 19, h1=y + 16,
             n=x + 9, j=y + 14, x1=x + 1, k=x + 10, k2=x + 11, e2=y + 21, k3=y + 17)
    return DIGITS[ch].format(**dict((key, num(val)) for key, val in v.items()))


# --- the items ---------------------------------------------------------------------------------
# Each returns (svg, (width, height), grip, head) in drawing units = pixels. The canvas is that
# size with MARGIN all round, and the PNG is trimmed to what is drawn.

def pencil(length):
    """The giant pencil, point up, eraser down. length is how much painted body it has left."""
    b = 62 + length
    body = 'M 14,%d L 14,62 Q 20.5,76 27,62 Q 40,84 53,62 Q 59.5,76 66,62 L 66,%d Z' % (b, b)
    s = P('M 14,62 L 40,6 L 66,62 L 66,84 L 14,84 Z', 'wood')
    s += P('M 29.8,28 L 40,6 L 50.2,28 Q 40,33 29.8,28 Z', 'graphite', 'wi')
    s += F(body, 'yellow')
    s += clip('pbody', body, F(rect(8, 40, 27, b + 4), 'yellow_light') + F(rect(53, 40, 72, b + 4), 'yellow_shade'))
    s += L('M 27,64 L 27,%d M 53,64 L 53,%d' % (b, b))
    s += L(body, 'wo')
    s += P('M 15,%d L 65,%d L 65,%d Q 65,%d 51,%d L 29,%d Q 15,%d 15,%d Z'
           % (b + 20, b + 20, b + 48, b + 62, b + 62, b + 62, b + 62, b + 48), 'pink')
    s += P(rect(11, b - 6, 69, b + 32, 4), 'silver')
    s += L('M 11,%d L 69,%d M 11,%d L 69,%d' % (b + 7, b + 7, b + 19, b + 19))
    grip = (40, b - 24) if length > 60 else (40, b + 8)
    return s, (80, b + 64), grip, (40, 8)


def eraser():
    """A pink block in three-quarter view, a paper sleeve round its lower half."""
    s = F(poly([(8, 24), (22, 10), (98, 10), (84, 24)]), 'pink_light')
    s += F(poly([(84, 24), (98, 10), (98, 132), (84, 146)]), 'pink_dark')
    s += F(rect(8, 24, 84, 146), 'pink')
    s += F(rect(8, 92, 84, 132), 'sky') + F(poly([(84, 92), (98, 78), (98, 118), (84, 132)]), 'blue_light')
    s += L('M 22,104 L 70,104 M 22,118 L 58,118', 'wi', 'white')
    s += L('M 8,24 L 84,24 L 98,10 M 84,24 L 84,146 M 8,92 L 84,92 L 98,78 M 8,132 L 84,132 L 98,118')
    s += L(poly([(8, 24), (22, 10), (98, 10), (98, 132), (84, 146), (8, 146)]), 'wo')
    return s, (106, 154), (46, 122), (53, 17)


def roller():
    """A paint roller: the drum across the top, wet with blue, on a wire arm into a red handle."""
    s = tube('M 178,46 L 198,46 Q 206,46 206,58 L 206,108 Q 206,120 194,125 L 112,158 Q 100,164 100,176 L 100,192',
             'silver', 8)
    for x, ln in ((58, 18), (118, 26), (150, 12)):
        s += P('M %d,70 L %d,%d Q %d,%d %d,%d L %d,70 Z' % (x, x, 70 + ln, x + 6, 82 + ln, x + 12, 70 + ln, x + 12),
               'blue', 'wi')
    s += P(rect(22, 16, 180, 76, 14), 'blue')
    s += F(rect(34, 24, 168, 34, 5), 'blue_light')
    s += P(ellipse(176, 46, 9, 26), 'silver', 'wi')
    s += P(rect(88, 180, 112, 198, 3), 'silver', 'wi')
    s += P(rect(84, 194, 116, 300, 15), 'red')
    s += L('M 84,226 L 116,226 M 84,242 L 116,242 M 84,258 L 116,258')
    s += L('M 94,206 L 94,216 M 94,272 L 94,286', 6, 'red_light')
    return s, (214, 306), (100, 252), (100, 46)


def metre_stick():
    """A wooden ruler with a brass cap at each end, numbered up from the hand."""
    s = P(rect(10, 10, 66, 310, 4), 'tan')
    s += F(rect(56, 14, 63, 306), 'wood_dark')
    s += L('M 47,40 C 45,90 49,150 46,210 C 44,250 47,280 47,290', 4, 'wood_dark')
    for i, y in enumerate(range(24, 300, 26)):
        s += L('M 10,%d L %d,%d' % (y, 30 if i % 2 == 0 else 21, y), 4.5)
    for n, y in (('4', 76), ('3', 128), ('2', 180), ('1', 232)):
        s += L(digit(n, 33, y - 10), 4.5)
    s += P(rect(8, 6, 68, 20, 3), 'yellow_shade', 'wi')
    s += P(rect(8, 300, 68, 314, 3), 'yellow_shade', 'wi')
    s += L(rect(10, 10, 66, 310, 4), 'wo')
    return s, (76, 320), (38, 280), (38, 8)


def paintbrush():
    """A fat brush, bristles up and dipped in orange."""
    bristles = ('M 24,116 L 24,96 C 18,72 22,44 32,26 C 36,18 38,12 40,8 C 42,12 44,18 48,26 '
                'C 58,44 62,72 56,96 L 56,116 Z')
    s = F(bristles, 'cream')
    s += clip('dip', bristles, F('M 0,0 L 80,0 L 80,60 Q 70,70 62,62 Q 52,74 40,64 Q 28,76 18,62 L 0,66 Z', 'orange')
              + L('M 10,58 L 18,62 Q 28,76 40,64 Q 52,74 62,62 L 70,58', 'wi'))
    s += L('M 34,104 L 35,82 M 46,104 L 45,82')
    s += L(bristles, 'wo')
    s += P('M 22,166 L 58,166 C 60,190 58,214 54,240 C 52,262 50,284 48,296 Q 40,306 32,296 '
           'C 30,284 28,262 26,240 C 22,214 20,190 22,166 Z', 'red')
    s += L('M 31,180 C 30,210 32,240 34,268', 6, 'red_light')
    s += P(rect(20, 108, 60, 172, 4), 'silver')
    s += L('M 20,150 L 60,150 M 20,160 L 60,160')
    s += L('M 28,118 L 28,140', 5, 'silver_light')
    return s, (80, 306), (40, 236), (40, 10)


# Kids' safety scissors, about the pivot at (0, 0): blades up, loops down. Each half is a blade
# and the loop on the other side, since that is how a pair is made - so opening them swings the
# blades apart one way and the loops apart the other.
SCISSOR_A_BLADE = 'M -15,-2 L -15,-92 C -15,-116 -7,-130 4,-130 C 9,-108 9,-50 7,-2 Z'
SCISSOR_B_BLADE = 'M 15,-2 L 15,-92 C 15,-116 7,-130 -4,-130 C -9,-108 -9,-50 -7,-2 Z'
SCISSOR_A_LOOP = ((30, 72), 21, 26)
SCISSOR_B_LOOP = ((-33, 76), 24, 30)
SCISSOR_TIP = 130.0


def scissor_half(blade, loop, shine_x):
    (cx, cy), rx, ry = loop
    arm = 'M 0,6 L %s,%s' % (num(cx * 0.8), num(cy - ry * 0.8))
    ring = '<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="none" stroke="{%s}" stroke-width="%s"/>\n'
    s = P(blade, 'silver')
    s += L('M %d,-20 L %d,-96' % (shine_x, shine_x), 5, 'silver_light')
    s += L(arm, 30) + ring % (cx, cy, rx, ry, 'ink', 26)
    s += L(arm, 18, 'blue') + ring % (cx, cy, rx, ry, 'blue', 14)
    return s


def scissors(spread):
    """spread: how far each half turns from closed, in degrees."""
    pivot = (90.0, 140.0)
    s = '<g transform="translate(%s,%s)">\n' % (num(pivot[0]), num(pivot[1]))
    s += '<g transform="rotate(%s)">%s</g>\n' % (num(spread), scissor_half(SCISSOR_B_BLADE, SCISSOR_B_LOOP, 8))
    s += '<g transform="rotate(%s)">%s</g>\n' % (num(-spread), scissor_half(SCISSOR_A_BLADE, SCISSOR_A_LOOP, -8))
    s += P(ellipse(0, 0, 9), 'silver_dark', 'wi') + '</g>\n'
    tip_a, tip_b = turned((0, -SCISSOR_TIP), -spread), turned((0, -SCISSOR_TIP), spread)
    loop_a, loop_b = turned(SCISSOR_A_LOOP[0], -spread), turned(SCISSOR_B_LOOP[0], spread)
    head = (pivot[0] + (tip_a[0] + tip_b[0]) / 2, pivot[1] + (tip_a[1] + tip_b[1]) / 2 + 4)
    grip = (pivot[0] + (loop_a[0] + loop_b[0]) / 2, pivot[1] + (loop_a[1] + loop_b[1]) / 2)
    return s, (180, 260), grip, head


def stamp():
    """A rubber stamp held by its knob, face up - it is swung face-down onto people."""
    s = P(rect(64, 80, 96, 116, 3), 'wood_dark')
    s += P(rect(60, 98, 100, 112, 5), 'red', 'wi')
    s += P(ellipse(80, 148, 36), 'knob')
    s += F(ellipse(66, 134, 9, 6), 'yellow_light')
    s += P(rect(10, 38, 150, 86, 6), 'tan')
    s += P(rect(50, 46, 110, 78, 3), 'white', 'wi')
    s += P(star(80, 63, 13, 6), 'red', 'wi')
    s += P(rect(16, 28, 144, 40), 'foam', 'wi')
    s += P(rect(18, 10, 142, 30, 3), 'red')
    return s, (160, 188), (80, 148), (80, 14)


def drip(x, top, w, length, colour):
    """Paint running down from a rim: coloured, outlined down its sides and round its end only."""
    shape = 'M %s,%s L %s,%s Q %s,%s %s,%s L %s,%s Z' % tuple(num(v) for v in (
        x, top, x, top + length, x + w / 2.0, top + length + w * 0.9, x + w, top + length, x + w, top))
    side = 'M %s,%s L %s,%s Q %s,%s %s,%s L %s,%s' % tuple(num(v) for v in (
        x, top + 6, x, top + length, x + w / 2.0, top + length + w * 0.9, x + w, top + length, x + w, top + 6))
    return F(shape, colour) + L(side, 'wi')


def paint_bucket():
    """A tin pail, full to the brim with blue and running over."""
    s = tube('M 26,74 C 20,0 150,0 144,74', 'silver', 6)
    s += P('M 22,70 L 148,70 L 140,150 Q 85,162 30,150 Z', 'silver')
    s += L('M 26,96 Q 85,106 144,96 M 30,136 Q 85,146 140,136')
    s += L('M 36,104 L 38,130', 6, 'silver_light')
    s += P(ellipse(85, 70, 64, 15), 'silver_light')
    s += P(ellipse(85, 70, 55, 10), 'blue', 'wi')
    s += F('M 32,76 Q 85,92 138,76 L 138,82 Q 85,98 32,82 Z', 'blue')
    for x, w, ln in ((44, 13, 22), (78, 15, 40), (114, 12, 14)):
        s += drip(x, 80, w, ln, 'blue')
    s += L('M 32,79 Q 85,95 138,79', 'wi')
    s += P(ellipse(24, 74, 6), 'silver_dark', 'wi') + P(ellipse(146, 74, 6), 'silver_dark', 'wi')
    return s, (170, 162), (85, 106), (85, 106)


def crayon_up(x, top, colour):
    d = 'M %d,78 L %d,%d L %d,%d Q %d,%d %d,%d L %d,%d L %d,78 Z' % (
        x, x, top + 20, x + 4, top + 7, x + 9, top, x + 14, top + 7, x + 18, top + 20, x + 18)
    return P(d, colour, 'wi') + L('M %d,%d L %d,%d' % (x, top + 20, x + 18, top + 20))


def crayon_box():
    """The classic yellow and green box, no name on it, crayons poking out of the top."""
    s = P(poly([(18, 70), (24, 36), (136, 36), (142, 70)]), 'yellow_shade')
    for x, top, colour in ((20, 12, 'red'), (40, 20, 'orange'), (60, 8, 'green'), (80, 16, 'blue'),
                           (100, 10, 'purple'), (120, 18, 'paint_pink')):
        s += crayon_up(x, top, colour)
    front = rect(14, 60, 146, 170, 4)
    s += F(front, 'yellow')
    s += clip('box', front,
              F('M 0,98 L 80,132 L 160,98 L 160,124 L 80,158 L 0,124 Z', 'green') + F(rect(0, 60, 160, 74), 'green'))
    s += L('M 14,98 L 80,132 L 146,98 M 14,124 L 80,158 L 146,124 M 14,74 L 146,74')
    s += L(front, 'wo')
    return s, (160, 174), (80, 112), (80, 112)


def crayon(colour):
    """One crayon on its side, tip to the right, in its paper wrapper."""
    light = colour + '_light'
    s = P('M 112,10 L 128,15 Q 144,24 128,33 L 112,38 Z', colour)
    s += P('M 14,10 L 116,10 L 116,38 L 14,38 Q 8,38 8,32 L 8,16 Q 8,10 14,10 Z', colour)
    s += F(rect(28, 10, 102, 38), light)
    s += F(rect(36, 10, 44, 38), colour) + F(rect(86, 10, 94, 38), colour)
    s += L('M 28,10 L 28,38 M 102,10 L 102,38')
    s += L('M 14,10 L 116,10 L 116,38 L 14,38 Q 8,38 8,32 L 8,16 Q 8,10 14,10 Z', 'wo')
    return s, (150, 48), (76, 24), (142, 24)


def glue_bottle():
    """A white school glue bottle with its pointed orange cap."""
    body = ('M 16,90 C 16,70 34,62 40,58 L 70,58 C 76,62 94,70 94,90 L 94,166 Q 94,180 80,180 '
            'L 30,180 Q 16,180 16,166 Z')
    s = P('M 40,42 L 52,10 Q 55,4 58,10 L 70,42 Z', 'orange')
    s += L('M 45,28 L 65,28')
    s += F(body, 'white')
    s += clip('label', body, F(rect(0, 98, 110, 152), 'yellow_light'))
    s += L('M 16,98 L 94,98 M 16,152 L 94,152')
    s += P('M 55,106 C 64,120 69,127 69,133 C 69,142 62,147 55,147 C 48,147 41,142 41,133 '
           'C 41,127 46,120 55,106 Z', 'white', 'wi')
    s += L('M 26,108 L 26,140', 6, 'paper_shade')
    s += L(body, 'wo')
    s += P(rect(32, 38, 78, 60, 5), 'orange')
    return s, (110, 186), (55, 118), (55, 118)


def ink_bottle():
    """A squat glass bottle of deep blue ink, corked."""
    glass = ('M 14,82 C 14,66 30,60 48,56 L 48,40 L 92,40 L 92,56 C 110,60 126,66 126,82 L 126,138 '
             'Q 126,152 112,152 L 28,152 Q 14,152 14,138 Z')
    s = P('M 50,40 L 50,16 Q 50,6 60,6 L 80,6 Q 90,6 90,16 L 90,40 Z', 'cork')
    s += F(ellipse(62, 18, 4), 'wood_dark') + F(ellipse(78, 28, 4), 'wood_dark') + F(ellipse(68, 34, 3), 'wood_dark')
    s += F(glass, 'glass')
    s += clip('ink', glass, F(rect(0, 78, 140, 160), 'deep_blue') + F(ellipse(70, 79, 60, 6), 'deep_blue_light'))
    s += P(rect(40, 96, 100, 132, 4), 'cream', 'wi')
    s += P('M 70,102 C 77,112 81,117 81,121 C 81,127 76,130 70,130 C 64,130 59,127 59,121 '
           'C 59,117 63,112 70,102 Z', 'deep_blue', 'wi')
    s += L('M 26,94 L 26,136', 7, 'white')
    s += L(glass, 'wo')
    s += P(rect(44, 34, 96, 46, 5), 'glass', 'wi')
    return s, (140, 156), (70, 98), (70, 98)


def paper_airplane():
    """A folded dart from ruled paper, side on, nose to the right: the wing up top and the keel
    hanging under it. Drawn as before and turned over top to bottom - the first way up read as
    a plane flying upside down (Eric, 2026-10-15)."""
    near = poly([(6, 34), (170, 52), (40, 72)])
    keel = poly([(46, 68), (170, 52), (86, 102)])
    s = P(poly([(28, 8), (170, 52), (58, 40)]), 'paper_shade')
    s += F(keel, 'paper_shade')
    s += clip('keel', keel, ''.join(L('M 0,%d L 180,%d' % (y, y - 14), 4.5, 'rule') for y in (86, 98)))
    s += L(keel, 'wo')
    s += F(near, 'paper')
    lines = ''.join(L('M 0,%d L 180,%d' % (y, y + 18), 4.5, 'rule') for y in (43, 53, 63))
    s += clip('wing', near, lines + L('M 34,20 L 30,90', 4.5, 'margin'))
    s += L(near, 'wo')
    s = '<g transform="matrix(1 0 0 -1 0 110)">' + s + '</g>'
    return s, (176, 110), (88, 56), (170, 58)


def paper_ball():
    """A drawing scrunched into a ball: creased facets and a bit of the crayon still showing."""
    # Points out round the edge, with a crease running in from every notch between them to one of
    # a few knots in the middle, so it puckers like scrunched paper rather than cut like a gem.
    rnd = random.Random(13)
    v = []
    n = 18
    for i in range(n):
        a = 2 * math.pi * i / n + rnd.uniform(-0.06, 0.06)
        r = (61 if i % 2 == 0 else 53) + rnd.uniform(-3, 3)
        v.append((75 + r * math.cos(a), 75 + r * math.sin(a)))
    outline = poly(v)
    knots = [(62, 62), (92, 70), (80, 98), (54, 92), (84, 44)]

    def nearest(p):
        return min(knots, key=lambda k: (k[0] - p[0]) ** 2 + (k[1] - p[1]) ** 2)

    shade, creases = '', []
    for i in range(1, n, 2):
        knot = nearest(v[i])
        creases.append(poly([v[i], knot], closed=False))
        nxt = v[(i + 2) % n]
        if nearest(nxt) == knot and i % 4 == 1:
            shade += F(poly([knot, v[i], v[(i + 1) % n], nxt]), 'paper_shade')
    creases.append(poly([knots[4], knots[0], knots[3], knots[2], knots[1], knots[0]], closed=False))
    scribble = L('M 40,96 L 56,74 L 62,104 L 80,76 L 88,108 L 104,84', 7, 'red')
    s = F(outline, 'paper') + clip('ball', outline, shade + scribble)
    s += L(' '.join(creases), 'wi') + L(outline, 'wo')
    return s, (150, 150), (75, 75), (75, 75)


def palette():
    """A painter's palette with a thumb hole and five dabs of paint."""
    board = ('M 40,34 C 66,8 136,6 162,40 C 180,64 170,110 128,122 C 96,130 78,116 60,120 '
             'C 34,126 10,112 12,86 C 14,70 30,68 36,62 C 44,54 30,46 40,34 Z')
    s = P(board + ' ' + ellipse(48, 92, 12, 10), 'tan', extra=' fill-rule="evenodd"')
    for i, (x, y, r, colour) in enumerate(((64, 42, 15, 'red'), (102, 30, 15, 'yellow'), (138, 44, 15, 'blue'),
                                           (150, 82, 14, 'green'), (118, 102, 14, 'purple'))):
        s += P(blob(x, y, r, 50 + i), colour, 'wi')
        s += F(ellipse(x - r * 0.35, y - r * 0.35, 3.5), 'white')
    return s, (180, 132), (92, 68), (92, 68)


def spray_can():
    """A purple spray can, nozzle up."""
    s = P(rect(38, 16, 62, 42, 5), 'silver_light')
    s += P(ellipse(50, 26, 4.5), 'purple', 'wi')
    s += P('M 14,66 C 14,46 32,38 42,38 L 58,38 C 68,38 86,46 86,66 Z', 'silver')
    s += P(rect(14, 60, 86, 192, 6), 'purple')
    s += F(rect(14, 100, 86, 140), 'yellow')
    s += L('M 14,100 L 86,100 M 14,140 L 86,140')
    s += P(blob(50, 120, 13, 7, lumps=8, wobble=0.3), 'purple', 'wi')
    s += L('M 26,74 L 26,92 M 26,150 L 26,176', 7, 'purple_light')
    s += P(rect(12, 58, 88, 68, 4), 'silver', 'wi')
    s += P(rect(12, 186, 88, 198, 4), 'silver', 'wi')
    return s, (100, 204), (50, 160), (50, 16)


def paint_tube():
    """A squeezy tube of green paint, cap up, crimped end down."""
    body = 'M 14,60 L 96,60 C 98,110 100,160 102,198 L 8,198 C 10,160 12,110 14,60 Z'
    s = P(rect(38, 6, 72, 40, 5), 'white')
    s += L('M 47,12 L 47,34 M 55,12 L 55,34 M 63,12 L 63,34')
    s += P('M 36,36 L 74,36 L 96,62 L 14,62 Z', 'silver')
    s += F(body, 'silver_light')
    s += clip('label', body, F(rect(0, 82, 110, 178), 'bright_green')
              + L('M 0,112 C 30,98 60,128 110,108', 10, 'white'))
    s += L('M 10,82 L 100,82 M 8,178 L 102,178')
    s += P(blob(56, 146, 13, 11), 'green', 'wi')
    s += L(body, 'wo')
    s += P(rect(4, 194, 106, 226, 3), 'silver')
    s += L(' '.join('M %d,200 L %d,220' % (x, x) for x in range(16, 100, 12)))
    return s, (110, 232), (55, 208), (55, 8)


def airbrush():
    """A chunky airbrush: nozzle up, a cup of pink paint on its side, the grip down below."""
    s = P(rect(48, 4, 62, 18, 3), 'silver_dark', 'wi')
    s += P('M 42,48 L 50,14 L 60,14 L 68,48 Z', 'silver_light')
    s += P(rect(66, 82, 88, 96, 3), 'silver', 'wi')
    s += P('M 76,52 L 118,52 L 112,96 Q 110,106 100,106 L 94,106 Q 84,106 82,96 Z', 'silver')
    s += P(ellipse(97, 53, 20, 6), 'paint_pink', 'wi')
    s += drip(108, 57, 8, 16, 'paint_pink')
    s += P(rect(20, 92, 46, 106, 6), 'red')
    s += P(rect(40, 40, 70, 186, 10), 'silver')
    s += L('M 49,56 L 49,170', 6, 'silver_light')
    s += P(rect(48, 226, 62, 254, 3), 'silver_dark', 'wi')
    s += P(rect(34, 166, 76, 236, 14), 'blue')
    s += L('M 34,190 L 76,190 M 34,204 L 76,204 M 34,218 L 76,218')
    return s, (126, 260), (55, 204), (55, 6)


def marker():
    """A fat felt-tip, cap off, purple, tip up."""
    body = rect(16, 58, 74, 226, 14)
    s = P('M 32,42 L 32,22 Q 32,8 45,8 Q 58,8 58,22 L 58,42 Z', 'purple')
    s += P('M 20,64 L 30,36 L 60,36 L 70,64 Z', 'silver_light')
    s += F(body, 'purple')
    s += clip('label', body, F(rect(0, 104, 90, 160), 'white') + F(rect(0, 204, 90, 230), 'violet'))
    s += L('M 26,132 C 34,118 42,146 50,130 C 56,120 62,136 66,128', 8, 'purple')
    s += L('M 16,104 L 74,104 M 16,160 L 74,160 M 16,204 L 74,204 M 16,74 L 74,74')
    s += L('M 26,82 L 26,96 M 26,168 L 26,196', 6, 'purple_light')
    s += L(body, 'wo')
    return s, (90, 232), (45, 186), (45, 10)


def foil_shield():
    """
    A paper plate wrapped in kitchen foil - the shield every kid has made - so it is a shield and
    a mirror at once: crinkled silver, a fluted rim and a gleam across it. Seen face on, held up in front of him.
    """
    cx = cy = 84.0
    rim = []
    for i in range(36):
        a = 2 * math.pi * i / 36
        r = 76.0 if i % 2 == 0 else 71.0
        rim.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    s = P(smooth(rim), 'silver')
    s += P(ellipse(cx, cy, 56), 'silver_light', 'wi')
    # Crinkles in the foil: short zigzags, the same every time.
    rnd = random.Random(77)
    for i in range(14):
        a = rnd.uniform(0, 2 * math.pi)
        r0 = rnd.uniform(14, 48)
        x, y = cx + r0 * math.cos(a), cy + r0 * math.sin(a)
        pts = [(x, y)]
        for k in range(3):
            x += rnd.uniform(-9, 9)
            y += rnd.uniform(-9, 9)
            pts.append((x, y))
        s += L(poly(pts, False), 2.5, 'silver_dark')
    for i in range(18):
        a = 2 * math.pi * (i + 0.5) / 18
        s += L('M %s,%s L %s,%s' % (num(cx + 58 * math.cos(a)), num(cy + 58 * math.sin(a)),
                                    num(cx + 72 * math.cos(a)), num(cy + 72 * math.sin(a))), 2.5, 'silver_dark')
    # The gleam.
    s += L('M 44,62 C 50,46 62,38 78,34', 10, 'white')
    s += L('M 40,80 C 40,76 41,73 42,70', 8, 'white')
    return s, (168, 168), (cx, cy), (cx, cy)


def sketchbook():
    """A closed spiral-bound sketchbook standing up, a smiley drawn on the cover."""
    s = P(rect(22, 14, 138, 178, 4), 'cream')
    s += P(rect(16, 8, 132, 172, 6), 'blue')
    s += P(rect(50, 20, 118, 42, 3), 'white', 'wi')
    s += L('M 58,31 C 70,25 82,37 94,29 C 100,26 106,31 110,30', 'wi')
    s += P(ellipse(78, 106, 34), 'yellow')
    s += F(ellipse(66, 96, 4.5, 7), 'ink') + F(ellipse(90, 96, 4.5, 7), 'ink')
    s += L('M 58,112 Q 78,134 98,112', 'wo')
    for y in range(22, 166, 16):
        s += L('M 28,%d C 14,%d 4,%d 10,%d C 14,%d 22,%d 26,%d' % (y - 4, y - 6, y - 2, y + 4, y + 7, y + 6, y + 3), 12)
        s += L('M 28,%d C 14,%d 4,%d 10,%d C 14,%d 22,%d 26,%d' % (y - 4, y - 6, y - 2, y + 4, y + 7, y + 6, y + 3), 4,
               'silver')
    return s, (144, 182), (77, 178), (77, 8)


def glitter_bomb():
    """A glittery bauble with a fuse: pink, a purple swirl, sparkles all over."""
    ball = ellipse(75, 104, 60)
    s = tube('M 75,40 C 75,26 86,24 90,14 C 92,8 98,6 104,8', 'tan', 5)
    s += F(ball, 'glitter')
    band = F('M 0,94 Q 40,78 75,96 Q 110,114 150,96 L 150,122 Q 110,140 75,122 Q 40,104 0,120 Z', 'violet')
    band += L('M 0,94 Q 40,78 75,96 Q 110,114 150,96 M 150,122 Q 110,140 75,122 Q 40,104 0,120')
    dots = ''
    for i, (x, y) in enumerate(((36, 70), (64, 132), (92, 60), (120, 92), (30, 120), (86, 150), (118, 140),
                                (58, 104), (104, 112), (48, 150))):
        dots += F(ellipse(x, y, 4), ('white', 'yellow', 'purple_light')[i % 3])
    s += clip('ball', ball, band + dots)
    s += L('M 32,96 Q 34,66 60,52', 8, 'white')
    s += P(sparkle(56, 80, 15), 'white', 'wi') + P(sparkle(104, 130, 14), 'yellow', 'wi')
    s += P(sparkle(108, 72, 10), 'white', 'wi') + P(sparkle(46, 132, 9), 'yellow', 'wi')
    s += L(ball, 'wo')
    s += P(rect(60, 36, 90, 50, 4), 'silver')
    return s, (150, 168), (75, 104), (75, 104)


def paint_balloon():
    """A water balloon full of orange-red paint, knotted at the bottom."""
    s = P('M 58,134 L 72,134 L 76,150 Q 65,157 54,150 Z', 'balloon')
    s += L('M 65,154 C 58,160 74,164 64,170')
    s += P('M 65,10 C 108,10 120,52 116,80 C 112,112 88,134 70,138 L 60,138 C 42,134 18,112 14,80 '
           'C 10,52 22,10 65,10 Z', 'balloon')
    s += F(ellipse(40, 50, 9, 18), 'white', ' transform="rotate(28 40 50)"')
    s += F(ellipse(58, 28, 5), 'white')
    s += L('M 96,100 Q 90,116 76,124', 6, 'orange')
    return s, (130, 172), (65, 78), (65, 78)


def tack_head(x, y, colour):
    return P(ellipse(x, y, 13, 9), colour, 'wi') + F(ellipse(x - 4, y - 3, 4, 2.5), 'white')


TACK_COLOURS = ('red', 'yellow', 'blue', 'green', 'orange', 'paint_pink', 'purple')


def thumbtacks():
    """A little clear box of coloured thumbtacks, lid up."""
    s = P(rect(22, 10, 148, 48, 6), 'box_tint', extra=' fill-opacity="0.6"')
    s += P(rect(14, 46, 156, 128, 6), 'box_tint', extra=' fill-opacity="0.45"')
    s += tube('M 58,52 L 66,28', 'silver', 3, rim=3) + tube('M 116,54 L 106,32', 'silver', 3, rim=3)
    i = 0
    for row, y in enumerate((52, 64, 82, 98, 114)):
        xs = range(30, 141, 26) if row % 2 == 0 else range(42, 141, 26)
        for x in xs:
            s += tack_head(x, y, TACK_COLOURS[i % len(TACK_COLOURS)])
            i += 3
    s += P(rect(14, 66, 156, 128, 6), 'box_tint', extra=' fill-opacity="0.5"')
    s += L('M 26,80 L 26,114 M 40,80 L 40,90', 6, 'white')
    return s, (170, 136), (85, 88), (85, 88)


def tack():
    """One thumbtack, point down."""
    s = P('M 30,40 L 40,40 L 37,84 L 35,96 L 33,84 Z', 'silver', 'wi')
    s += P('M 8,36 C 8,12 62,12 62,36 Q 62,44 54,44 L 16,44 Q 8,44 8,36 Z', 'red')
    s += F(ellipse(24, 26, 7, 4), 'white')
    return s, (70, 100), (35, 52), (35, 52)


def rinse_cup():
    """A jar of muddy rainbow rinse water with a brush left standing in it."""
    jar = 'M 18,72 L 18,170 Q 18,186 34,186 L 106,186 Q 122,186 122,170 L 122,72 Z'
    brush = (P('M -8,-34 L -8,-14 Q 0,8 8,-14 L 8,-34 Z', 'cream', 'wi')
             + P(rect(-9, -60, 9, -30, 3), 'silver', 'wi')
             + P('M -8,-58 L -9,-168 Q 0,-180 9,-168 L 8,-58 Z', 'blue', 'wi'))
    s = F(jar, 'glass', ' fill-opacity="0.5"')
    s += '<g transform="translate(58,170) rotate(22)">%s</g>\n' % brush
    water = 'M 18,94 Q 44,86 70,94 Q 96,102 122,94 L 122,190 L 18,190 Z'
    bands = ''.join(F(rect(0, y, 140, y + 26), c) for y, c in ((86, 'murky1'), (112, 'murky2'), (138, 'murky3'),
                                                                (164, 'murky4')))
    s += clip('jar', jar, clip('water', water, bands.replace('/>', ' fill-opacity="0.82"/>')))
    s += L('M 18,94 Q 44,86 70,94 Q 96,102 122,94', 'wi')
    s += L('M 32,108 L 32,166', 7, 'white')
    s += L(jar, 'wo')
    s += P(rect(12, 58, 128, 76, 6), 'glass', extra=' fill-opacity="0.6"')
    return s, (140, 192), (70, 126), (70, 126)


def popup_book():
    """A closed red hardcover with a gold star on it."""
    s = P(rect(24, 18, 150, 158, 4), 'cream')
    s += L('M 142,30 L 142,150 M 34,151 L 140,151', 3, 'cream_shade')
    s += P(rect(14, 10, 142, 150, 6), 'red')
    s += F(rect(17, 13, 34, 147), 'red_dark')
    s += L('M 34,12 L 34,148')
    s += L('M 18,30 L 32,30 M 18,130 L 32,130', 6, 'yellow')
    s += P(star(88, 82, 40, 17), 'yellow')
    s += F(star(84, 78, 22, 9), 'yellow_light')
    return s, (160, 166), (82, 84), (82, 84)


def open_book():
    """The pop-up book lying open: red covers, cream page edges and two ruled pages. Every pop-up
    stands up out of the middle of it."""
    s = P('M 4,160 L 125,172 L 246,160 L 246,170 L 125,184 L 4,170 Z', 'red')
    s += P('M 6,154 C 50,148 100,152 125,166 L 125,174 C 100,160 50,156 6,162 Z', 'cream_shade', 'wi')
    s += P('M 244,154 C 200,148 150,152 125,166 L 125,174 C 150,160 200,156 244,162 Z', 'cream_shade', 'wi')
    s += P('M 18,128 C 52,120 100,124 125,140 L 125,166 C 100,152 50,148 6,154 Z', 'paper')
    s += P('M 232,128 C 198,120 150,124 125,140 L 125,166 C 150,152 200,148 244,154 Z', 'paper')
    s += L('M 24,138 C 54,132 90,134 112,146 M 18,148 C 50,142 90,144 112,156', 4, 'rule')
    s += L('M 226,138 C 196,132 160,134 138,146 M 232,148 C 200,142 160,144 138,156', 4, 'rule')
    return s


def popup_book_open():
    """The pop-up book lying open, a cardboard bouncy castle standing up out of the middle."""
    s = open_book()
    for cx in (74, 176):
        s += P(rect(cx - 14, 72, cx + 14, 150), 'cardboard')
        s += P(rect(cx - 7, 96, cx + 7, 118, 7), 'window', 'wi')
    s += P('M 90,150 L 90,56 L 100,56 L 100,66 L 110,66 L 110,56 L 120,56 L 120,66 L 130,66 L 130,56 '
           'L 140,56 L 140,66 L 150,66 L 150,56 L 160,56 L 160,150 Z', 'cardboard')
    s += P('M 113,150 L 113,124 Q 113,110 125,110 Q 137,110 137,124 L 137,150 Z', 'door', 'wi')
    s += P(ellipse(125, 86, 9, 11), 'window', 'wi')
    for cx in (74, 176):
        s += P('M %d,76 L %d,26 L %d,76 Z' % (cx - 20, cx, cx + 20), 'roof')
        s += L('M %d,26 L %d,8' % (cx, cx))
        s += P('M %d,8 L %d,14 L %d,20 Z' % (cx, cx + 18, cx), 'red', 'wi')
    return s, (250, 188), (125, 182), (125, 100)


def popup_book_croc():
    """The pop-up book open on a cardboard crocodile, facing right, its lower jaw out over the
    page - the upper jaw is its own piece (popup_book_croc_jaw), hinged at the back of the mouth,
    so it can snap shut."""
    s = ''
    # The tail, curling back over the left page.
    s += P('M 70,128 C 40,128 22,112 26,92 C 30,78 44,80 44,92 C 44,104 56,110 74,108 Z', 'green')
    # The body, a fat hump on stubby legs, standing up out of the fold.
    s += P('M 58,150 L 58,108 C 60,84 92,72 124,74 C 150,76 164,88 168,104 L 168,150 Z', 'green')
    s += F('M 70,140 C 90,128 140,126 160,138 L 160,148 L 70,148 Z', 'green_light')
    for x in (76, 104, 132):
        s += P('M %d,96 L %d,84 L %d,96 Z' % (x - 9, x, x + 9), 'bright_green', 'wi')
    s += P(rect(70, 138, 88, 156, 4), 'green', 'wi')
    s += P(rect(140, 138, 158, 156, 4), 'green', 'wi')
    # The lower jaw, teeth up.
    s += P('M 160,108 L 240,114 C 246,116 246,126 240,128 L 160,128 Z', 'green')
    for x in (176, 192, 208, 224):
        s += P('M %d,114 L %d,104 L %d,114 Z' % (x - 6, x, x + 6), 'white', 'wi')
    s += F('M 166,118 L 236,121 L 236,124 L 166,124 Z', 'pink_light')
    return open_book() + s, (250, 188), (125, 182), (162, 108)


def popup_book_croc_jaw():
    """The crocodile's upper jaw: hinged at its back-bottom corner (the grip), teeth down, an eye
    on top - it swings up open and snaps down."""
    s = P('M 6,46 C 6,22 20,10 38,10 C 54,10 62,18 68,24 L 86,28 C 96,30 98,44 90,48 L 8,52 Z', 'green')
    s += P(ellipse(82, 33, 5, 4), 'green_light', 'wi')
    for x in (24, 42, 60, 78):
        s += P('M %d,50 L %d,62 L %d,50 Z' % (x - 6, x, x + 6), 'white', 'wi')
    s += P(ellipse(34, 18, 13, 11), 'white', 'wi')
    s += F(ellipse(38, 19, 6, 7), 'ink')
    s += F(ellipse(40, 16, 2, 2), 'white')
    return s, (102, 66), (8, 50), (90, 44)


def popup_book_cloud():
    """The pop-up book open on a cardboard storm cloud on a stick, standing up out of the fold.
    The lightning is the game's to draw: it strikes from under the cloud."""
    s = tube('M 125,152 L 125,64', 'cardboard', 8, 5)
    s += P(blob(125, 46, 34, 7, 8, 0.1), 'murky4')
    cloud = ('M 34,64 C 18,64 14,40 34,36 C 34,16 62,8 76,22 C 86,2 122,0 132,18 C 146,4 182,8 '
             '182,30 C 204,28 214,56 192,66 C 190,78 170,80 160,72 C 146,84 118,84 108,72 '
             'C 92,84 64,82 58,70 C 50,76 36,74 34,64 Z')
    s += P(cloud, 'trace_back')
    s += F('M 50,58 C 70,66 90,62 108,56 C 126,64 150,64 170,56 C 180,62 190,60 194,54 L 194,62 '
           'C 170,76 120,78 96,70 C 76,76 56,72 50,64 Z', 'murky4')
    s += L('M 52,40 C 60,30 74,30 80,36 M 120,26 C 130,18 146,20 150,30', 4, 'white')
    return open_book() + s, (250, 188), (125, 182), (125, 74)


def small_pencil():
    """A pencil 90 long, tip at (0, 0), for the pencil case."""
    return (P('M -11,24 L 0,0 L 11,24 L 11,30 L -11,30 Z', 'wood', 'wi')
            + P('M -4.5,10 L 0,0 L 4.5,10 Q 0,12 -4.5,10 Z', 'graphite', 'wi')
            + P('M -11,100 L -11,24 Q 0,34 11,24 L 11,100 Z', 'yellow')
            + L('M 0,30 L 0,100'))


def pencil_case():
    """A teal canvas pencil case, zipped most of the way, a pencil poking out of the gap."""
    case = ('M 14,52 C 14,40 24,36 40,36 L 160,36 C 176,36 186,40 186,52 L 188,106 C 188,118 178,122 164,122 '
            'L 36,122 C 22,122 12,118 12,106 Z')
    s = P(case, 'teal')
    s += L('M 24,58 L 24,108 Q 24,112 30,112 L 170,112 Q 176,112 176,106 L 176,58', 4, 'teal_light',
           ' stroke-dasharray="10 8"')
    s += P('M 26,46 Q 52,30 82,46 Q 52,60 26,48 Z', 'teal_dark', 'wi')
    s += clip('poke', 'M 0,0 L 200,0 L 200,46 L 82,46 Q 52,60 26,48 L 0,48 Z',
              '<g transform="translate(34,4) rotate(-25)">%s</g>\n' % small_pencil())
    s += L('M 82,46 Q 52,60 26,48', 'wi')
    s += L('M 82,46 L 172,46', 18) + L('M 82,46 L 172,46', 10, 'silver_light')
    s += L('M 86,46 L 170,46', 4, 'silver_dark', ' stroke-dasharray="4 4"')
    s += P(rect(78, 38, 96, 54, 4), 'silver', 'wi')
    s += P(rect(81, 50, 93, 78, 5), 'silver', 'wi') + F(ellipse(87, 70, 3), 'silver_dark')
    return s, (200, 128), (100, 80), (100, 80)


def canvas():
    """A small blank canvas on a tabletop A-frame easel."""
    s = tube('M 85,30 L 85,196', 'wood_dark', 10)
    s += tube('M 82,16 L 26,198', 'wood', 12) + tube('M 88,16 L 144,198', 'wood', 12)
    s += P(rect(132, 44, 144, 140, 2), 'canvas_side', 'wi')
    s += P(rect(30, 40, 138, 138, 2), 'white')
    s += P(rect(20, 134, 150, 150, 3), 'wood')
    s += P(rect(76, 28, 94, 46, 3), 'wood', 'wi')
    s += P(ellipse(85, 14, 8), 'wood_dark', 'wi')
    return s, (170, 204), (85, 200), (85, 120)


def gold_star():
    """A gold star sticker."""
    s = P(star(85, 90, 76, 33), 'gold')
    s += F(star(80, 84, 44, 19), 'gold_light')
    s += L('M 60,72 Q 66,60 76,56', 7, 'white')
    s += F(ellipse(88, 54, 4), 'white')
    return s, (170, 166), (85, 90), (85, 90)


def white_out():
    """A bottle of correction fluid: white, a blue-and-white label, a blue cap with the brush in it."""
    body = ('M 16,92 C 16,74 30,66 40,62 L 70,62 C 80,66 94,74 94,92 L 94,160 Q 94,174 80,174 '
            'L 30,174 Q 16,174 16,160 Z')
    s = P('M 34,66 L 38,16 Q 40,8 48,8 L 62,8 Q 70,8 72,16 L 76,66 Z', 'blue')
    s += L('M 46,20 L 45,58 M 55,18 L 55,58 M 64,20 L 65,58', 'wi')
    s += F(body, 'white')
    s += clip('label', body, F(rect(0, 104, 110, 152), 'blue') + L('M 0,130 C 30,112 60,146 110,124', 9, 'white'))
    s += L('M 16,104 L 94,104 M 16,152 L 94,152')
    s += L(body, 'wo')
    s += P(rect(30, 58, 80, 70, 4), 'silver_light', 'wi')
    return s, (110, 180), (55, 118), (55, 118)


def magnifying_glass():
    """A magnifying glass: red rim and handle, light blue glass."""
    s = P(rect(60, 156, 90, 284, 14), 'red')
    s += L('M 68,170 L 68,262', 6, 'red_light')
    s += P(rect(66, 124, 84, 162, 3), 'silver', 'wi')
    s += F(ellipse(75, 72, 56), 'lens', ' fill-opacity="0.8"')
    s += L('M 40,66 A 36,36 0 0 1 64,38', 9, 'white') + F(ellipse(90, 40, 4.5), 'white')
    s += L(ellipse(75, 72, 56), 26) + L(ellipse(75, 72, 56), 14, 'red')
    return s, (150, 290), (75, 230), (75, 72)


def tracing_paper():
    """A sheet of tracing paper, a star traced on it, one corner curling up."""
    sheet = 'M 12,12 L 138,12 L 138,124 C 126,132 108,146 102,166 L 12,166 Z'
    s = P(sheet, 'trace', extra=' fill-opacity="0.75"')
    s += L(star(70, 82, 40, 17), 4, 'trace_line')
    s += P('M 138,124 C 126,132 108,146 102,166 C 120,166 134,150 138,124 Z', 'trace_back', 'wi',
           ' fill-opacity="0.9"')
    return s, (150, 178), (75, 89), (75, 89)



def guitar():
    """Flambe's electric guitar for the show (a story prop, never dropped in a match): a red
    flying V with flames up its wings, held up by the neck, body to the sky (Eric, 2026-10-22)."""
    body = ('M 75,226 L 10,58 C 4,40 12,26 30,26 L 54,26 L 75,104 L 96,26 L 120,26 '
            'C 138,26 146,40 140,58 Z')
    s = P(body, 'red')
    s += clip('vflame', body,
              F('M 16,140 C 22,100 18,76 30,46 C 36,74 46,84 44,112 C 54,96 54,74 62,62 '
                'C 72,96 68,124 75,160 L 16,160 Z', 'orange')
              + F('M 134,140 C 128,100 132,76 120,46 C 114,74 104,84 106,112 C 96,96 96,74 88,62 '
                  'C 78,96 82,124 75,160 L 134,160 Z', 'orange')
              + F('M 34,150 C 38,124 36,108 44,92 C 50,112 56,124 56,150 Z', 'yellow')
              + F('M 116,150 C 112,124 114,108 106,92 C 100,112 94,124 94,150 Z', 'yellow'))
    s += L(body, 'wo')
    s += P(rect(62, 140, 88, 156, 3), 'silver', 'wi')
    s += P(rect(62, 168, 88, 184, 3), 'silver', 'wi')
    s += P(rect(64, 196, 86, 204, 2), 'silver_dark', 'wi')
    s += P(rect(66, 200, 84, 334, 3), 'wood')
    for y in range(222, 330, 16):
        s += L('M 66,%d L 84,%d' % (y, y), 3, 'silver_dark')
    for x in (70, 74, 78, 82):
        s += L('M %d,146 L %d,338' % (x, x), 1.5, 'silver_light')
    s += P('M 64,330 L 86,330 L 92,372 Q 75,382 58,372 Z', 'red')
    for y in (342, 356):
        s += P(ellipse(54, y, 5), 'silver', 'wi') + P(ellipse(96, y, 5), 'silver', 'wi')
    return s, (150, 384), (75, 300), (75, 70)


ITEMS = [
    ('pencil', pencil(174)), ('pencil_stub', pencil(30)),
    ('eraser', eraser()),
    ('roller', roller()),
    ('metre_stick', metre_stick()),
    ('paintbrush', paintbrush()),
    ('scissors', scissors(0.0)), ('scissors_open', scissors(16.0)),
    ('stamp', stamp()),
    ('paint_bucket', paint_bucket()),
    ('crayon_box', crayon_box()),
    ('crayon_red', crayon('red')), ('crayon_blue', crayon('blue')),
    ('crayon_green', crayon('green')), ('crayon_yellow', crayon('yellow')),
    ('glue_bottle', glue_bottle()),
    ('ink_bottle', ink_bottle()),
    ('paper_airplane', paper_airplane()),
    ('paper_ball', paper_ball()),
    ('palette', palette()),
    ('spray_can', spray_can()),
    ('paint_tube', paint_tube()),
    ('airbrush', airbrush()),
    ('marker', marker()),
    ('foil_shield', foil_shield()),
    ('glitter_bomb', glitter_bomb()),
    ('paint_balloon', paint_balloon()),
    ('thumbtacks', thumbtacks()), ('tack', tack()),
    ('rinse_cup', rinse_cup()),
    ('popup_book', popup_book()), ('popup_book_open', popup_book_open()),
    ('popup_book_croc', popup_book_croc()), ('popup_book_croc_jaw', popup_book_croc_jaw()),
    ('popup_book_cloud', popup_book_cloud()),
    ('pencil_case', pencil_case()),
    ('canvas', canvas()),
    ('gold_star', gold_star()),
    ('white_out', white_out()),
    ('magnifying_glass', magnifying_glass()),
    ('tracing_paper', tracing_paper()),
    ('guitar', guitar()),
]


# --- build -------------------------------------------------------------------------------------

def value_of(hex_colour):
    r, g, b = [int(hex_colour[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return colorsys.rgb_to_hsv(r, g, b)[2]


def check_palette():
    """Nothing outside a character goes near black - the indigo outline is the darkest thing here."""
    dark = sorted(k for k, v in C.items() if isinstance(v, str) and value_of(v) < 0.40)
    if dark:
        raise SystemExit('too dark for an item (characters own black): %s' % ', '.join(dark))


def build():
    started = time.time()
    check_palette()
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    pieces = {}
    for name, (svg, (w, h), grip, head) in ITEMS:
        pieces[name] = (svg, (-MARGIN, -MARGIN, w + MARGIN, h + MARGIN), grip, False)
    cut, _ = draw_parts(pieces, fill, C, 1.0, seed_prefix='items/')

    table = {}
    for name, (svg, size, grip, head) in ITEMS:
        img, grip_px, _ = cut[name]
        head_px = (grip_px[0] + head[0] - grip[0], grip_px[1] + head[1] - grip[1])
        for label, (x, y) in (('grip', grip_px), ('head', head_px)):
            if not (0 <= x <= img.size[0] and 0 <= y <= img.size[1]):
                print('warning: %s %s (%.1f, %.1f) is off its %dx%d drawing' % (name, label, x, y, img.size[0], img.size[1]))
        img.save(os.path.join(OUT, name + '.png'))
        table[name] = {
            'texture': name + '.png',
            'size': [img.size[0], img.size[1]],
            'grip': [round(grip_px[0], 1), round(grip_px[1], 1)],
            'head': [round(head_px[0], 1), round(head_px[1], 1)],
        }
    with open(os.path.join(OUT, 'items.json'), 'wb') as fh:
        fh.write(json.dumps(table, indent=2, sort_keys=True).encode('utf-8'))
    print('items -> %d drawings in %s (%.1f s)' % (len(table), os.path.relpath(OUT, REPO), time.time() - started))


if __name__ == '__main__':
    build()
