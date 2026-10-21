# -*- coding: utf-8 -*-
"""
Draws DoomBot Mk. II's scrap armour and hangs it on DoomBot's rig:

    python tools/art/mk2_armour.py

Mk. II is DoomBot - Eric's drawing, every line of it - with upgrades bolted on: whatever the
factory had lying about, none of it matching (Eric's call, 2026-10-15). A saw blade for a crest
on a riveted helmet band, two exhaust stacks behind his shoulder, a belly plate cut from a road
barrier and stencilled II, a spiked pauldron on his far shoulder, a spiked band round his near
arm, spiked bracers, and knee caps over riveted shin guards.

Nothing of DoomBot's is redrawn or covered over for good. The armour is its own parts, drawn and
pencilled like the stand-ins (drawn.py, so this needs $GODOT_BIN), hung on his parts as rig.json
extras of the outfit "mk2" - hidden on DoomBot, worn by Mk. II (FighterData.Outfit). It keeps
clear of his face, so every expression still reads, and of the socket on his side, which his
near arm comes out of and his missile is fired from: his near arm gets a band below it rather
than a pauldron over it.

Each piece is drawn about the joint of the part it hangs on, in that part's own pixels, so it
goes back on at offset zero and moves exactly with him. cut_doombot.py calls apply() after a
re-cut, which puts the armour back on without drawing it again.

Emits:
    fighters/doombot/parts/Mk2*.png    the armour
    fighters/doombot/rig.json          its parts and extras, outfit "mk2"
"""

from __future__ import division, print_function

import json
import math
import os

from drawn import REPO, draw_parts

ROOT = os.path.join(REPO, 'fighters', 'doombot')
OUTFIT = 'mk2'

C = dict(
    ink='#1d1b21', rust='#b85f2e', rust_dark='#86421f', copper='#c98b45',
    steel='#a3abb5', steel_dark='#646c78', steel_light='#d3d8de', hazard='#f2c232',
    red='#d8433b', bolt='#ece8de',
    # About as heavy as the marker ring round his own parts.
    wo=10, wi=6,
)


def pts(points):
    return ' '.join('%.1f,%.1f' % p for p in points)


def poly(points, colour, width='{wo}'):
    return '<polygon points="%s" fill="{%s}" stroke="{ink}" stroke-width="%s" stroke-linejoin="round"/>' % (
        pts(points), colour, width)


def rivet(x, y, r=7):
    return '<circle cx="%.1f" cy="%.1f" r="%.1f" fill="{bolt}" stroke="{ink}" stroke-width="4"/>' % (x, y, r)


def rivets(a, b, n, r=7):
    return ''.join(rivet(a[0] + (b[0] - a[0]) * i / (n - 1), a[1] + (b[1] - a[1]) * i / (n - 1), r) for i in range(n))


def spike(b1, b2, tip):
    return poly([b1, tip, b2], 'steel_light', '{wi}')


def clip(polygon, x0, y0, x1, y1):
    """A convex polygon cut to a box (Sutherland-Hodgman), for stripes that stop at a plate's edge."""
    def cut(points, inside, cross):
        out = []
        for i, p in enumerate(points):
            q = points[(i + 1) % len(points)]
            if inside(p):
                out.append(p)
                if not inside(q):
                    out.append(cross(p, q))
            elif inside(q):
                out.append(cross(p, q))
        return out

    def at_x(x):
        return lambda p, q: (x, p[1] + (q[1] - p[1]) * (x - p[0]) / (q[0] - p[0]))

    def at_y(y):
        return lambda p, q: (p[0] + (q[0] - p[0]) * (y - p[1]) / (q[1] - p[1]), y)

    for inside, cross in ((lambda p: p[0] >= x0, at_x(x0)), (lambda p: p[0] <= x1, at_x(x1)),
                          (lambda p: p[1] >= y0, at_y(y0)), (lambda p: p[1] <= y1, at_y(y1))):
        polygon = cut(polygon, inside, cross)
        if not polygon:
            break
    return polygon


def hazard(x0, y0, x1, y1, width=34.0):
    """Yellow and black barrier stripes, slanting, cut to the box, inside a marker outline."""
    out = '<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="{hazard}"/>' % (x0, y0, x1 - x0, y1 - y0)
    h = y1 - y0
    x = x0 - h
    while x < x1:
        stripe = clip([(x, y1), (x + width, y1), (x + width + h, y0), (x + h, y0)], x0, y0, x1, y1)
        if len(stripe) >= 3:
            out += '<polygon points="%s" fill="{ink}"/>' % pts(stripe)
        x += width * 2
    return out + '<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="none" stroke="{ink}" stroke-width="{wi}"/>' % (
        x0, y0, x1 - x0, y1 - y0)


# Stencilled letters as paint strokes, since Godot's SVG renderer has no fonts: each a list of
# strokes in a 12 x 20 cell. Only ones that read the same mirrored - he faces both ways.
LETTERS = {
    'I': [[(6, 0), (6, 20)]],
}


def stencil(text, x, y, height, colour='red', width=9):
    k = height / 20.0
    out = ''
    for ch in text:
        if ch == ' ':
            x += 7 * k
            continue
        for stroke in LETTERS[ch]:
            out += ('<polyline points="%s" fill="none" stroke="{%s}" stroke-width="%d" stroke-linecap="round" '
                    'stroke-linejoin="round"/>' % (pts([(x + px * k, y + py * k) for px, py in stroke]), colour, width))
        x += (10 if ch == 'I' else 18) * k
    return out


def pipe(base, top, width, bands):
    """An exhaust stack from base to top: a rust pipe with copper bands, a rim, and soot inside."""
    dx, dy = top[0] - base[0], top[1] - base[1]
    length = math.hypot(dx, dy)
    ux, uy = dx / length, dy / length
    nx, ny = -uy * width / 2.0, ux * width / 2.0

    def at(t, grow=0.0):
        cx, cy = base[0] + dx * t, base[1] + dy * t
        k = 1.0 + grow
        return (cx + nx * k, cy + ny * k), (cx - nx * k, cy - ny * k)

    a0, b0 = at(0.0)
    a1, b1 = at(1.0)
    out = poly([a0, a1, b1, b0], 'rust')
    for t in bands:
        p0, q0 = at(t - 0.035, 0.08)
        p1, q1 = at(t + 0.035, 0.08)
        out += poly([p0, p1, q1, q0], 'copper', '{wi}')
    r0, s0 = at(1.0 - 22.0 / length, 0.3)
    r1, s1 = at(1.0, 0.3)
    out += poly([r0, r1, s1, s0], 'steel_dark')
    angle = math.degrees(math.atan2(dy, dx)) + 90.0
    out += ('<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="9" transform="rotate(%.1f %.1f %.1f)" fill="{ink}"/>'
            % (top[0], top[1], width * 0.55, angle, top[0], top[1]))
    return out


def saw_blade(cx, cy, r_in, r_out, teeth):
    points = []
    for i in range(teeth):
        a = 2.0 * math.pi * i / teeth
        b = 2.0 * math.pi * (i + 0.55) / teeth
        points.append((cx + math.cos(a) * r_in, cy + math.sin(a) * r_in))
        points.append((cx + math.cos(b) * r_out, cy + math.sin(b) * r_out))
    return (poly(points, 'steel_light')
            + '<circle cx="%.1f" cy="%.1f" r="%.1f" fill="none" stroke="{ink}" stroke-width="{wi}"/>' % (cx, cy, r_in - 22)
            + '<circle cx="%.1f" cy="%.1f" r="16" fill="{steel_dark}" stroke="{ink}" stroke-width="{wi}"/>' % (cx, cy))


# --- the pieces, each about its part's joint, in that part's pixels -----------------------------

# The head: a saw blade standing up between his antennas, in a riveted band round the top of his
# head - above his brows, so his face is exactly as drawn. The band goes round the corner of his
# turned head (cut_doombot.py), with a seam where his side meets his face.
HEAD_CORNER_X = -44
HELMET = (saw_blade(6, -346, 84, 106, 20)
          + poly([(-224, -346), (228, -344), (228, -288), (-222, -292)], 'rust')
          + poly([(-222, -306), (228, -304), (228, -288), (-222, -292)], 'steel_dark', '{wi}')
          + '<path d="M %d,-344 L %d,-290" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>'
            % (HEAD_CORNER_X, HEAD_CORNER_X)
          + rivets((-200, -330), (206, -328), 6)
          + '<path d="M 38,-330 C 54,-318 74,-318 88,-326" fill="none" stroke="{ink}" stroke-width="{wi}" '
            'stroke-linecap="round"/>')

# Behind his shoulder, leaning back: two stacks, so a hot DoomBot looks like a furnace.
STACKS = (pipe((-262, -520), (-324, -846), 50, (0.55, 0.82))
          + pipe((-212, -560), (-252, -934), 62, (0.45, 0.78)))
EXHAUST = [('exhaust', (-252, -934)), ('exhaust2', (-324, -846))]

# Across the bottom of his body, below everything drawn on it: a steel plate across his front with
# a strip of road barrier along it, stencilled II - which, unlike MK II, still reads facing the
# other way - and a rusty plate that does not match round his side.
BELLY_CORNER_X = -68
BELLY = (poly([(-286, -136), (BELLY_CORNER_X, -142), (BELLY_CORNER_X, 24), (-284, 18)], 'rust')
         + rivet(-266, -116) + rivet(-88, -122) + rivet(-266, 0) + rivet(-88, 6)
         + poly([(BELLY_CORNER_X + 6, -142), (248, -138), (250, 20), (BELLY_CORNER_X + 6, 24)], 'steel')
         + hazard(-46, -60, 234, 0)
         + stencil('II', 63, -130, 58, width=16)
         + rivet(-44, -120) + rivet(230, -116) + rivet(-44, 6) + rivet(232, 2))

# His far shoulder (no socket there): a domed pauldron with spikes, bulging out the far side,
# where it shows past his body.
PAULDRON = (poly([(-96, 84), (-104, 20), (-80, -40), (-30, -72), (12, -78), (62, -70), (104, -38),
                  (126, 16), (124, 84), (80, 66), (20, 60), (-50, 64)], 'rust')
            + spike((-34, -66), (0, -76), (-24, -148))
            + spike((26, -76), (60, -68), (54, -154))
            + spike((84, -56), (110, -28), (146, -104))
            + poly([(-90, 80), (-44, 62), (40, 60), (122, 80), (112, 122), (60, 104), (-40, 104), (-84, 122)],
                   'steel_dark')
            + rivets((-70, 50), (100, 50), 5))

# His near arm comes out of the socket on his side, so its armour is a band further down, clear
# of the socket however the arm swings, with spikes on the outside.
BAND = (spike((-94, 146), (-94, 178), (-152, 156))
        + spike((-94, 192), (-94, 224), (-148, 204))
        + poly([(-96, 120), (98, 116), (100, 236), (-94, 240)], 'steel_dark')
        + poly([(-80, 138), (84, 134), (86, 218), (-78, 222)], 'rust', '{wi}')
        + rivet(-62, 154) + rivet(68, 150) + rivet(-60, 204) + rivet(70, 200))


def bracer(x0, x1, y0, y1, spikes_out):
    """A riveted bracer round a forearm, spiked along the outside (-1: the left, 1: the right)."""
    edge = x0 if spikes_out < 0 else x1
    out = ''
    for top in (y0 + 14, y0 + 54):
        out += spike((edge, top), (edge, top + 28), (edge + spikes_out * 50, top + 8))
    out += poly([(x0, y0), (x1, y0 - 2), (x1 + 2, y1), (x0 - 2, y1 + 2)], 'rust')
    mid = (y0 + y1) / 2.0
    out += poly([(x0 + 4, mid - 11), (x1 - 4, mid - 12), (x1 - 4, mid + 10), (x0 + 4, mid + 11)], 'steel_dark', '{wi}')
    out += rivet(x0 + 18, y0 + 16) + rivet(x1 - 18, y0 + 15) + rivet(x0 + 18, y1 - 14) + rivet(x1 - 18, y1 - 15)
    return out


BRACER_FRONT = bracer(-100, 104, 74, 170, -1)
BRACER_BACK = bracer(-76, 82, 70, 176, 1)

# Over each knee a cap with a spike pointing the way he faces, and a riveted guard down the shin
# that stops short of his boot.
GREAVE = (poly([(-80, 52), (78, 48), (72, 228), (-74, 232)], 'rust')
          + '<path d="M 0,70 L -2,212" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>'
          + rivet(-58, 72) + rivet(56, 68) + rivet(-54, 212) + rivet(52, 208)
          + spike((48, -20), (52, 28), (112, 2))
          + '<circle cx="2" cy="4" r="60" fill="{steel}" stroke="{ink}" stroke-width="{wo}"/>'
          + '<circle cx="2" cy="4" r="34" fill="none" stroke="{ink}" stroke-width="{wi}"/>'
          + rivet(2, 4, 12))

# name: (svg, viewBox about the joint). Untrimmed, so the joint is always at -viewBox origin.
PIECES = {
    'Mk2Helmet': (HELMET, (-245, -470, 250, -255)),
    'Mk2Stacks': (STACKS, (-370, -980, -150, -500)),
    'Mk2Belly': (BELLY, (-305, -155, 305, 36)),
    'Mk2Pauldron': (PAULDRON, (-115, -165, 160, 135)),
    'Mk2Band': (BAND, (-165, 100, 115, 255)),
    'Mk2BracerFront': (BRACER_FRONT, (-165, 55, 120, 190)),
    'Mk2BracerBack': (BRACER_BACK, (-95, 50, 150, 195)),
    'Mk2Greave': (GREAVE, (-95, -70, 125, 245)),
}

# Back to front within each part: the stacks go behind his body; the rest sit on top of the part
# they are bolted to. (extra name, bone, piece, behind)
WEAR = [
    ('mk2Stacks', 'Torso', 'Mk2Stacks', True),
    ('mk2Belly', 'Torso', 'Mk2Belly', False),
    ('mk2Helmet', 'Head', 'Mk2Helmet', False),
    ('mk2Pauldron', 'ArmBack_Upper', 'Mk2Pauldron', False),
    ('mk2Band', 'ArmFront_Upper', 'Mk2Band', False),
    ('mk2BracerFront', 'ArmFront_Lower', 'Mk2BracerFront', False),
    ('mk2BracerBack', 'ArmBack_Lower', 'Mk2BracerBack', False),
    ('mk2GreaveFront', 'LegFront_Lower', 'Mk2Greave', False),
    ('mk2GreaveBack', 'LegBack_Lower', 'Mk2Greave', False),
]


def fill(svg, colours=C):
    return svg.format(**colours)


def apply(root=ROOT):
    """Puts the armour (already drawn) back in rig.json: its parts, and its extras, hidden until worn."""
    rig_path = os.path.join(root, 'rig.json')
    with open(rig_path, 'rb') as fh:
        rig = json.loads(fh.read().decode('utf-8'))
    for name in [n for n in rig['parts'] if n.startswith('Mk2')]:
        del rig['parts'][name]
    extras = [e for e in rig.get('extras', []) if e.get('outfit') != OUTFIT]
    for name, (_, box) in PIECES.items():
        if not os.path.exists(os.path.join(root, 'parts', name + '.png')):
            raise SystemExit('mk2_armour: parts/%s.png is missing - run python tools/art/mk2_armour.py' % name)
        rig['parts'][name] = {'texture': 'parts/%s.png' % name, 'pivot': [float(-box[0]), float(-box[1])]}
    for name, bone, piece, behind in WEAR:
        extra = {'name': name, 'bone': bone, 'part': piece, 'offset': [0, 0], 'onPart': True, 'outfit': OUTFIT}
        if behind:
            extra['behind'] = True
        extras.append(extra)
    rig['extras'] = extras
    # The tops of the stacks, where his heat smoke comes out (Fighter.DrawHeatSmoke).
    points = [p for p in rig.get('points', []) if p.get('outfit') != OUTFIT]
    for name, at in EXHAUST:
        points.append({'name': name, 'bone': 'Torso', 'offset': list(at), 'outfit': OUTFIT})
    rig['points'] = points
    with open(rig_path, 'wb') as fh:
        fh.write(json.dumps(rig, indent=2, sort_keys=True).encode('utf-8'))
    print('mk2 armour: %d pieces on %s' % (len(WEAR), rig.get('name', root)))


def build():
    pieces = dict((name, (svg, box, (0, 0), False)) for name, (svg, box) in PIECES.items())
    cut, _ = draw_parts(pieces, fill, C, 1.0, seed_prefix='mk2', trim=False)
    for name in sorted(PIECES):
        cut[name][0].save(os.path.join(ROOT, 'parts', name + '.png'))
    apply()


if __name__ == '__main__':
    build()
