# -*- coding: utf-8 -*-
"""
Draws Lugnut - a stand-in until Eric draws him - and rigs him:

    python tools/art/lugnut.py

A cartoon muscle man of a construction worker, three-quarter view facing right: hard hat, open
hi-vis vest over a tank top, tool belt, jeans, work gloves, steel-toe boots, and a scowl, coloured
in coloured pencil inside a black marker outline. Huge up top and short in the leg, because he
fights with his arms and his tools, almost never his feet. Eric picked this look from a drawn
example (2026-10-08) and asked for every detail that would not survive coloured pencil or match
size to be taken out - so nothing here is finer than a brow or a stripe. Keep it that way.

Drawn, rasterised and pencilled as drawn.py describes, so this needs $GODOT_BIN. His tools and
the props for his specials are drawn the same way, in his style, each on the canvas and grip the
stick-figure drawing it replaced had, so his reach is unchanged.

Emits:
    fighters/lug/parts/*.png      the rig's parts, trimmed, with their pivots in rig.json
    fighters/lug/poses/*.png      his tools and his specials' props
    fighters/lug/rig.json
    fighters/lug/source/drawing.png   the whole figure, flexing, as the example was drawn
"""

from __future__ import division, print_function

import json
import math
import os

from drawn import REPO, along, draw_parts, mirrored, place

ROOT = os.path.join(REPO, 'fighters', 'lug')

# Canonical units per drawing unit. His tools are the size they were beside the old stick figure,
# which stood about 590 units tall; at 0.7 he stands the same, so every tool is still the size it
# was beside him - and his reach, which is built on their length, is unchanged.
SCALE = 0.7

C = dict(
    ink='#1e1a22', skin='#e9a274', stubble='#c98d6c', buzz='#3b3338', white='#f6f3ec',
    tank='#f1ede4', vest='#f47a20', reflect='#f9e04b', jeans='#3f63a8', belt='#7f5128',
    pouch='#a26a37', metal='#b9bcc4', glove='#d6a35c', boot='#8a5a32', toe='#b07a45',
    hat='#f6c431', shine='#fff8cc',
    # Outlines a touch heavier than the drawing's, so they hold at match size.
    wo=8, wi=5,
)

# --- the limbs, each hanging straight down from its joint at (0, 0) ---------------------------

UPPER_ARM_LEN = 150.0
FIST = 180.0           # elbow to the middle of the fist: where a tool is held
GLOVE_TOP = 98.0       # elbow to the glove's cuff: below it is hand, above it is arm
THIGH_LEN = 120.0
SHIN_LEN = 112.0
SOLE = 70.0            # ankle to the bottom of the boot

# A big deltoid and bicep, bulging to the front.
UPPER_ARM = '''
<path d="M -54,-6 C -58,-50 -22,-62 2,-62 C 32,-60 58,-42 56,-6
         C 62,30 62,70 52,104 C 46,126 40,142 36,152
         C 32,184 -32,184 -36,152 C -46,132 -66,104 -66,70
         C -66,40 -56,14 -54,-6 Z" fill="{skin}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M -52,24 C -28,46 24,48 54,20" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>
'''

# A thick forearm into a work glove, balled into a fist.
FOREARM = '''
<path d="M -34,-2 C -34,-24 34,-24 36,-2
         C 50,22 52,54 40,82 C 34,98 30,108 28,118 L -28,118
         C -32,104 -46,84 -50,56 C -52,30 -42,10 -34,-2 Z" fill="{skin}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M -36,98 L 36,98 L 44,140 L -44,140 Z" fill="{glove}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M -44,134 C -56,134 -60,142 -60,154 L -60,196 C -60,216 -48,226 -28,226 L 30,226
         C 50,226 60,214 60,194 L 60,152 C 60,140 52,134 42,134 Z" fill="{glove}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
'''

# The thumb across the empty fist. The hand that holds his tools has none: the tool's grip runs
# under it (Eric, 2026-10-08).
THUMB = '''
<path d="M -60,166 C -34,164 -14,172 -8,190" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>
'''

THIGH = '''
<path d="M -38,-8 C -38,-38 38,-38 38,-8 C 44,30 42,80 32,116
         C 30,146 -30,146 -32,116 C -42,80 -44,30 -38,-8 Z" fill="{jeans}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
'''

# The calf bulges at the back.
SHIN = '''
<path d="M -28,-6 C -28,-32 28,-32 28,-6 C 32,24 32,56 30,84 L 32,114 L -32,114 L -30,84
         C -38,56 -38,22 -28,-6 Z" fill="{jeans}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
'''

# A steel-toe work boot from the ankle at (0, 0), sole flat, toe forward. Cut as a foot, so the
# game keeps it flat on the floor.
BOOT = '''
<path d="M -34,-12 L 32,-12 L 34,22 C 54,26 74,34 82,48 L 84,58 L -38,58 L -36,22 Z" fill="{boot}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M 44,30 C 62,32 76,40 82,50 L 84,58 L 46,58 C 44,48 44,38 44,30 Z" fill="{toe}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
<path d="M -34,-12 L 32,-12 L 32,2 L -34,2 Z" fill="{toe}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
<path d="M -40,54 L 86,54 L 86,70 L -40,70 Z" fill="{ink}" stroke="{ink}" stroke-width="4" stroke-linejoin="round"/>
'''

# --- the body and head, drawn where they stand in the figure ---------------------------------

# The torso's joint is the hip; the head's is the base of the neck.
HIP = (400.0, 646.0)
NECK = (426.0, 300.0)
SHOULDER_NEAR, SHOULDER_FAR = (292.0, 352.0), (540.0, 352.0)
# Where the arms hang in the rig: as drawn. Facing right in three-quarter view, his near side is
# on the left, so the arm on the left shoulder is the front arm, drawn over everything, and the
# one on the right is the far arm, behind his chest. Eric's call, 2026-10-08 - after trying them
# hung from the middle of his chest (they hovered there) and the other way round.
RIG_SHOULDER_FRONT, RIG_SHOULDER_BACK = SHOULDER_NEAR, SHOULDER_FAR
HIP_NEAR, HIP_FAR = (372.0, 648.0), (430.0, 648.0)
CROWN_Y = 88.0

TORSO_PATH = ('M 395,290 C 360,300 320,305 290,318 C 262,330 254,360 262,392 '
              'C 270,450 318,500 340,548 C 346,565 346,580 342,600 '
              'C 336,620 334,640 338,662 C 380,676 430,676 468,662 '
              'C 470,640 466,620 460,600 C 456,580 458,565 466,548 '
              'C 488,500 520,470 548,440 C 572,418 582,380 568,352 '
              'C 554,326 520,312 490,304 C 470,298 458,294 450,290 Z')
# The vest, open down the middle over the tank top.
# The outline, open along the bottom so the body runs into the legs without a line across them.
TORSO_LINE = ('M 468,662 C 470,640 466,620 460,600 C 456,580 458,565 466,548 '
              'C 488,500 520,470 548,440 C 572,418 582,380 568,352 C 554,326 520,312 490,304 '
              'C 470,298 458,294 450,290 L 395,290 C 360,300 320,305 290,318 C 262,330 254,360 262,392 '
              'C 270,450 318,500 340,548 C 346,565 346,580 342,600 C 336,620 334,640 338,662')
VEST_NEAR = 'M 150,250 L 358,298 C 380,380 396,470 402,560 L 402,700 L 150,700 Z'
VEST_FAR = 'M 498,298 C 512,340 512,390 500,430 C 488,470 474,520 470,560 L 700,560 L 700,250 Z'
TORSO = '''
<clipPath id="torso"><path d="{tp}"/></clipPath>
<clipPath id="vest"><path d="{vn}"/><path d="{vf}"/></clipPath>
<path d="{tp}" fill="{skin}"/>
<g clip-path="url(#torso)">
  <path d="M 150,250 L 374,296 C 398,356 470,372 504,298 L 700,250 L 700,760 L 150,760 Z" fill="{tank}"/>
  <path d="M 374,296 C 398,356 470,372 504,298" fill="none" stroke="{ink}" stroke-width="{wi}"/>
  <path d="M 340,400 C 360,440 420,454 476,436 C 512,448 548,440 566,412" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="{vn}" fill="{vest}"/>
  <path d="{vf}" fill="{vest}"/>
  <g clip-path="url(#vest)">
    <path d="M 150,428 L 700,418 L 700,450 L 150,460 Z M 150,508 L 700,498 L 700,530 L 150,540 Z" fill="{reflect}"/>
  </g>
  <path d="M 358,298 C 380,380 396,470 402,560 L 402,600" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>
  <path d="M 498,298 C 512,340 512,390 500,430 C 488,470 474,520 470,560 L 470,600" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>
  <path d="M 150,612 L 700,606 L 700,760 L 150,760 Z" fill="{jeans}"/>
  <path d="M 150,580 L 700,574 L 700,616 L 150,622 Z" fill="{belt}" stroke="{ink}" stroke-width="{wi}"/>
</g>
<path d="{tl}" fill="none" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round" stroke-linecap="round"/>
<rect x="414" y="582" width="46" height="34" rx="5" fill="{metal}" stroke="{ink}" stroke-width="{wi}"/>
<path d="M 312,604 L 374,604 L 370,676 C 354,690 330,690 316,676 Z" fill="{pouch}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
<path d="M 312,604 L 374,604 L 372,628 C 352,636 334,636 314,628 Z" fill="{belt}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
'''

HEAD_PATH = ('M 362,150 C 362,105 405,88 440,92 C 478,96 498,124 497,156 L 500,166 '
             'C 508,172 512,180 512,186 L 528,198 L 503,207 C 506,218 510,230 512,242 '
             'C 514,262 494,276 462,276 C 428,276 398,266 384,248 C 368,236 358,200 362,150 Z')
# The tough guy: a heavy brow angled down at the nose, squinting eyes looking where he faces,
# gritted teeth pulled down at the near corner, a stubbled jaw, and a proper ear.
HEAD = '''
<clipPath id="head"><path d="{hp}"/></clipPath>
<path d="M 386,318 L 390,246 L 464,246 L 468,318 Z" fill="{skin}"/>
<path d="M 386,310 L 390,246 M 464,246 L 468,310" fill="none" stroke="{ink}" stroke-width="{wo}" stroke-linecap="round"/>
<path d="{hp}" fill="{skin}"/>
<g clip-path="url(#head)">
  <path d="M 376,212 C 398,236 432,248 470,246 C 488,244 498,232 502,214 L 540,214 L 540,290 L 360,290 Z" fill="{stubble}"/>
  <path d="M 362,172 C 356,112 400,86 442,90 C 482,94 500,120 498,148 C 470,134 430,132 404,142 L 386,180 Z" fill="{buzz}"/>
</g>
<path d="{hp}" fill="none" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M 404,178 C 396,168 380,170 376,184 C 373,195 378,205 381,214 C 384,222 390,226 397,223
         C 401,220 402,214 403,208 C 404,198 406,188 404,178 Z" fill="{skin}"/>
<path d="M 404,178 C 396,168 380,170 376,184 C 373,195 378,205 381,214 C 384,222 390,226 397,223"
      fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>
<path d="M 398,183 C 389,180 384,189 388,198 C 390,203 395,204 397,200" fill="none" stroke="{ink}" stroke-width="3.5" stroke-linecap="round"/>
<path d="M 422,150 L 478,166 L 476,180 L 424,166 Z" fill="{ink}" stroke="{ink}" stroke-width="3" stroke-linejoin="round"/>
<path d="M 482,170 L 500,160 L 502,170 L 484,180 Z" fill="{ink}" stroke="{ink}" stroke-width="3" stroke-linejoin="round"/>
<path d="M 442,186 C 448,178 464,177 474,182 C 466,192 450,193 442,186 Z" fill="{white}" stroke="{ink}" stroke-width="3"/>
<circle cx="468" cy="185" r="4.4" fill="{ink}"/>
<path d="M 439,183 L 475,180" fill="none" stroke="{ink}" stroke-width="5" stroke-linecap="round"/>
<path d="M 485,189 C 488,184 496,183 501,185 C 498,192 490,193 485,189 Z" fill="{white}" stroke="{ink}" stroke-width="3"/>
<circle cx="497" cy="187" r="3.2" fill="{ink}"/>
<path d="M 484,186 L 502,183" fill="none" stroke="{ink}" stroke-width="4" stroke-linecap="round"/>
<path d="M 472,238 C 482,235 494,234 506,236 L 505,245 C 494,244 484,247 474,252 Z" fill="{white}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
<path d="M 468,244 C 465,250 466,256 470,261" fill="none" stroke="{ink}" stroke-width="3.5" stroke-linecap="round"/>
'''

HAT_DOME = 'M 352,138 C 348,82 392,48 434,48 C 478,48 512,80 512,126 L 514,134 L 352,140 Z'
HAT_BRIM = ('M 340,140 C 340,132 348,130 358,130 L 516,128 C 530,128 538,134 538,140 '
            'C 538,146 532,150 522,150 L 352,152 C 344,152 340,148 340,140 Z')
HAT = '''
<path d="{dome}" fill="{hat}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="{brim}" fill="{hat}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M 440,50 C 456,76 466,104 468,130" fill="none" stroke="{ink}" stroke-width="5" stroke-linecap="round"/>
'''

# The hard hat lit up, for as long as it is armour: blocking, and through his smashes and barge
# a small hit bounces off it. He always wears the hat now; this glint is how you see it is on.
HAT_SHINE = '''
<path d="{dome}" fill="none" stroke="{ink}" stroke-width="34" stroke-linejoin="round"/>
<path d="{brim}" fill="none" stroke="{ink}" stroke-width="34" stroke-linejoin="round"/>
<path d="{dome}" fill="none" stroke="{shine}" stroke-width="22" stroke-linejoin="round"/>
<path d="{brim}" fill="none" stroke="{shine}" stroke-width="22" stroke-linejoin="round"/>
''' + HAT + '''
<path d="M 500,22 L 507,42 L 527,49 L 507,56 L 500,76 L 493,56 L 473,49 L 493,42 Z" fill="{shine}" stroke="{ink}" stroke-width="4" stroke-linejoin="round"/>
'''


def fill(svg, colours=C):
    values = dict(colours, tp=TORSO_PATH, tl=TORSO_LINE, hp=HEAD_PATH, vn=VEST_NEAR, vf=VEST_FAR,
                  dome=HAT_DOME, brim=HAT_BRIM)
    return svg.format(**values)


# --- his tools, drawn in his style -----------------------------------------------------------

# Each tool keeps the canvas, grip and length the old stick-figure drawing had, so the game holds
# and measures it exactly as before - his reach and his hitboxes are built on those. Drawn at one
# canonical unit per drawing unit, with outlines about as heavy as his own at that size.
TOOL_C = dict(C, steel='#aab0bc', steel_dark='#6c7280', wood='#c08a52', red='#d2463a',
              red_dark='#a8352b', grey='#5d626e', orange_dark='#c95f16', wo=6, wi=4)

# Held tools are stored tip up, gripped near the bottom, on a 190 x 300 canvas: name: (svg, grip).
HELD = {
    'sledgehammer': ('''
<rect x="84" y="56" width="22" height="236" rx="10" fill="{hat}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="39" y="12" width="112" height="56" rx="8" fill="{steel}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="39" y="12" width="20" height="56" rx="6" fill="{steel_dark}" stroke="{ink}" stroke-width="{wi}"/>
<rect x="131" y="12" width="20" height="56" rx="6" fill="{steel_dark}" stroke="{ink}" stroke-width="{wi}"/>
<rect x="81" y="64" width="28" height="16" rx="4" fill="{grey}" stroke="{ink}" stroke-width="{wi}"/>
''', (95, 270)),
    'pickaxe': ('''
<rect x="84" y="44" width="22" height="248" rx="10" fill="{wood}" stroke="{ink}" stroke-width="{wo}"/>
<path d="M 8,74 C 30,42 60,24 95,22 C 130,24 160,42 182,74 C 150,58 124,50 95,50 C 66,50 40,58 8,74 Z"
      fill="{steel}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<rect x="80" y="16" width="30" height="42" rx="6" fill="{red}" stroke="{ink}" stroke-width="{wi}"/>
''', (95, 270)),
    'shovel': ('''
<rect x="85" y="100" width="20" height="192" rx="9" fill="{wood}" stroke="{ink}" stroke-width="{wo}"/>
<path d="M 52,98 L 52,44 C 52,24 76,8 95,6 C 114,8 138,24 138,44 L 138,98 C 120,106 70,106 52,98 Z"
      fill="{steel}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<rect x="80" y="92" width="30" height="30" rx="6" fill="{vest}" stroke="{ink}" stroke-width="{wi}"/>
''', (95, 270)),
    'pipewrench': ('''
<rect x="79" y="82" width="32" height="210" rx="12" fill="{red}" stroke="{ink}" stroke-width="{wo}"/>
<path d="M 73,94 L 73,24 C 73,18 78,16 84,16 L 134,16 C 141,16 144,21 144,28 L 144,40 C 144,46 140,48 134,48
         L 102,48 L 102,94 Z" fill="{grey}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<rect x="62" y="56" width="56" height="24" rx="8" fill="{steel}" stroke="{ink}" stroke-width="{wi}"/>
''', (95, 270)),
    'crowbar': ('''
<path d="M 95,288 L 95,70 C 95,34 116,16 140,22 C 156,26 162,42 154,58" fill="none" stroke="{ink}" stroke-width="28"
      stroke-linecap="round" stroke-linejoin="round"/>
<path d="M 95,288 L 95,70 C 95,34 116,16 140,22 C 156,26 162,42 154,58" fill="none" stroke="{red}" stroke-width="15"
      stroke-linecap="round" stroke-linejoin="round"/>
''', (95, 270)),
    'jackhammer': ('''
<path d="M 86,86 L 86,22 L 95,4 L 104,22 L 104,86 Z" fill="{steel}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<rect x="86" y="206" width="18" height="34" fill="{grey}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="63" y="78" width="64" height="136" rx="10" fill="{hat}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="63" y="148" width="64" height="20" fill="{grey}" stroke="{ink}" stroke-width="{wi}"/>
<rect x="31" y="227" width="128" height="24" rx="12" fill="{grey}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="31" y="227" width="32" height="24" rx="12" fill="{ink}"/>
<rect x="127" y="227" width="32" height="24" rx="12" fill="{ink}"/>
''', (95, 238)),
    'beam': ('''
<rect x="65" y="6" width="60" height="266" rx="4" fill="{red_dark}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="65" y="6" width="15" height="266" rx="4" fill="{red}" stroke="{ink}" stroke-width="{wi}"/>
<rect x="110" y="6" width="15" height="266" rx="4" fill="{red}" stroke="{ink}" stroke-width="{wi}"/>
<circle cx="95" cy="44" r="8" fill="{white}" stroke="{ink}" stroke-width="{wi}"/>
<circle cx="95" cy="106" r="8" fill="{white}" stroke="{ink}" stroke-width="{wi}"/>
<circle cx="95" cy="168" r="8" fill="{white}" stroke="{ink}" stroke-width="{wi}"/>
<circle cx="95" cy="230" r="8" fill="{white}" stroke="{ink}" stroke-width="{wi}"/>
''', (95, 270)),
    'sign': (('''
<rect x="88" y="116" width="14" height="176" rx="6" fill="{steel}" stroke="{ink}" stroke-width="{wo}"/>
<path d="%s" fill="{red}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="%s" fill="none" stroke="{white}" stroke-width="5" stroke-linejoin="round"/>
<rect x="61" y="56" width="68" height="16" rx="3" fill="{white}"/>
''' % tuple('M ' + ' L '.join('%.1f,%.1f' % (95 + r * math.cos(math.radians(22.5 + 45 * i)),
                                               64 + r * math.sin(math.radians(22.5 + 45 * i))) for i in range(8)) + ' Z'
              for r in (62, 50))), (95, 270)),
}

# The nail gun is held by its own grip, out to the side: a 150 x 190 canvas, muzzle up.
NAILGUN = ('''
<path d="M 42,114 L 78,114 L 132,170 C 137,176 134,186 124,186 L 104,186 Z" fill="{grey}" stroke="{ink}"
      stroke-width="{wo}" stroke-linejoin="round"/>
<rect x="48" y="2" width="24" height="18" rx="4" fill="{grey}" stroke="{ink}" stroke-width="{wi}"/>
<rect x="38" y="12" width="44" height="120" rx="10" fill="{vest}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="38" y="62" width="44" height="16" fill="{grey}" stroke="{ink}" stroke-width="{wi}"/>
<rect x="80" y="30" width="16" height="82" rx="4" fill="{steel}" stroke="{ink}" stroke-width="{wi}"/>
''', (94, 160))

# The sledgehammer in his hand when he is not swinging something else: the prop part, hanging
# DOWN from the grip like a limb, on the 132 x 306 canvas the old one had.
PROP = ('''
<rect x="55" y="2" width="22" height="250" rx="10" fill="{hat}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="13" y="240" width="106" height="56" rx="8" fill="{steel}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="13" y="240" width="20" height="56" rx="6" fill="{steel_dark}" stroke="{ink}" stroke-width="{wi}"/>
<rect x="99" y="240" width="20" height="56" rx="6" fill="{steel_dark}" stroke="{ink}" stroke-width="{wi}"/>
<rect x="52" y="226" width="28" height="16" rx="4" fill="{grey}" stroke="{ink}" stroke-width="{wi}"/>
''', (66, 10))

# The props for his specials, each a whole picture facing right, sized by the move that shows it:
# name: (svg, canvas width, canvas height).
SPECIAL_PROPS = {
    'wheelbarrow': ('''
<path d="M 70,52 L 10,72" fill="none" stroke="{ink}" stroke-width="20" stroke-linecap="round"/>
<path d="M 70,52 L 10,72" fill="none" stroke="{wood}" stroke-width="10" stroke-linecap="round"/>
<path d="M 150,108 L 140,160" fill="none" stroke="{ink}" stroke-width="14" stroke-linecap="round"/>
<path d="M 150,108 L 140,160" fill="none" stroke="{grey}" stroke-width="6" stroke-linecap="round"/>
<path d="M 40,30 L 252,30 L 222,112 L 92,112 Z" fill="{vest}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<rect x="32" y="20" width="228" height="18" rx="8" fill="{orange_dark}" stroke="{ink}" stroke-width="{wo}"/>
<circle cx="226" cy="134" r="30" fill="{grey}" stroke="{ink}" stroke-width="{wo}"/>
<circle cx="226" cy="134" r="11" fill="{steel}" stroke="{ink}" stroke-width="{wi}"/>
''', 300, 170),
    'wreckingball': ('''
<clipPath id="ball"><circle cx="85" cy="92" r="70"/></clipPath>
<circle cx="85" cy="20" r="11" fill="none" stroke="{ink}" stroke-width="12"/>
<circle cx="85" cy="20" r="11" fill="none" stroke="{steel}" stroke-width="5"/>
<circle cx="85" cy="92" r="70" fill="{grey}"/>
<g clip-path="url(#ball)"><rect x="0" y="80" width="170" height="24" fill="{hat}"/></g>
<circle cx="85" cy="92" r="70" fill="none" stroke="{ink}" stroke-width="{wo}"/>
<path d="M 15,80 L 155,80 M 15,104 L 155,104" fill="none" stroke="{ink}" stroke-width="{wi}"/>
<ellipse cx="58" cy="56" rx="15" ry="10" fill="{steel}" transform="rotate(-30 58 56)"/>
''', 170, 170),
    'cone': ('''
<clipPath id="cone"><path d="M 53,10 L 77,10 L 96,124 L 34,124 Z"/></clipPath>
<path d="M 53,10 L 77,10 L 96,124 L 34,124 Z" fill="{vest}"/>
<g clip-path="url(#cone)"><rect x="0" y="42" width="130" height="18" fill="{white}"/><rect x="0" y="82" width="130" height="18" fill="{white}"/></g>
<path d="M 53,10 L 77,10 L 96,124 L 34,124 Z" fill="none" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<rect x="8" y="120" width="114" height="22" rx="6" fill="{orange_dark}" stroke="{ink}" stroke-width="{wo}"/>
''', 130, 150),
    'girder': ('''
<rect x="24" y="28" width="312" height="54" fill="{red_dark}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="10" y="10" width="340" height="22" rx="4" fill="{red}" stroke="{ink}" stroke-width="{wo}"/>
<rect x="10" y="78" width="340" height="22" rx="4" fill="{red}" stroke="{ink}" stroke-width="{wo}"/>
''' + ''.join('<circle cx="%d" cy="55" r="8" fill="{white}" stroke="{ink}" stroke-width="{wi}"/>\n' % x
              for x in range(60, 340, 60)), 360, 110),
}


# --- the figure as drawn ---------------------------------------------------------------------

def flexing(colours=C):
    """The whole figure in the pose Eric picked: a double biceps flex in a big step."""
    ground = 930.0
    ankle_y = ground - SOLE

    def shin_to_floor(knee):
        return math.degrees(math.acos(max(-1.0, min(1.0, (ankle_y - knee[1]) / SHIN_LEN))))

    far_elbow = along(SHOULDER_FAR, -68, UPPER_ARM_LEN)
    near_elbow = along(SHOULDER_NEAR, 78, UPPER_ARM_LEN)
    near_knee = along(HIP_NEAR, 12, THIGH_LEN)
    far_knee = along(HIP_FAR, -30, THIGH_LEN)
    near_shin, far_shin = shin_to_floor(near_knee), shin_to_floor(far_knee)
    arm, forearm = fill(UPPER_ARM, colours), fill(FOREARM + THUMB, colours)
    thigh, shin, boot = fill(THIGH, colours), fill(SHIN, colours), fill(BOOT, colours)
    out = [
        place(mirrored(arm), SHOULDER_FAR, -68),
        place(mirrored(forearm), far_elbow, -172),
        place(thigh, HIP_FAR, -30),
        place(shin, far_knee, far_shin),
        place(boot, along(far_knee, far_shin, SHIN_LEN)),
        place(thigh, HIP_NEAR, 12),
        place(shin, near_knee, near_shin),
        place(boot, along(near_knee, near_shin, SHIN_LEN)),
        fill(TORSO, colours), fill(HEAD, colours), fill(HAT, colours),
        place(arm, SHOULDER_NEAR, 78),
        place(forearm, near_elbow, 188),
    ]
    return '\n'.join(out)


# --- build -------------------------------------------------------------------------------------

def unit(v):
    return round(v * SCALE, 1)


def build():
    parts_dir = os.path.join(ROOT, 'parts')
    poses_dir = os.path.join(ROOT, 'poses')
    source_dir = os.path.join(ROOT, 'source')
    for d in (parts_dir, poses_dir, source_dir):
        if not os.path.isdir(d):
            os.makedirs(d)

    # name: (svg, viewBox, pivot in drawing units, mirrored). Limbs are drawn about their joint.
    limb_box = (-110, -110, 110, 260)
    pieces = {
        'Torso': (TORSO, (230, 260, 620, 720), HIP, False),
        'Head': (HEAD, (330, 60, 560, 340), NECK, False),
        'Hat': (HAT, (320, 20, 560, 180), NECK, False),
        'HatShine': (HAT_SHINE, (300, 0, 580, 190), NECK, False),
        'Arm_Upper': (UPPER_ARM, limb_box, (0, 0), True),
        'ArmFront_Lower': (FOREARM, limb_box, (0, 0), True),
        'ArmBack_Lower': (FOREARM + THUMB, limb_box, (0, 0), True),
        'Leg_Upper': (THIGH, limb_box, (0, 0), False),
        'Leg_Lower': (SHIN, limb_box, (0, 0), False),
        'Foot': (BOOT, limb_box, (0, 0), False),
    }
    cut, drawings = draw_parts(pieces, fill, C, SCALE,
                               extra={'drawing': (flexing, (60, 10, 790, 950), '#f7f2e8')})

    parts = {}
    hand_row = None
    for name, (svg, box, pivot, _) in sorted(pieces.items()):
        img, pivot_px, top = cut[name]
        if name.startswith('Arm') and name.endswith('_Lower'):
            hand_row = round((GLOVE_TOP - box[1]) * SCALE - top, 1)
        # The near and far limbs are the same drawing; the rig darkens the far leg.
        sides = ('Front', 'Back') if name.startswith(('Arm_', 'Leg_', 'Foot')) else ('',)
        for side in sides:
            if name == 'Foot':
                part = 'Foot' + side
            elif side:
                part = name.replace('_', side + '_', 1)
            else:
                part = name
            img.save(os.path.join(parts_dir, part + '.png'))
            parts[part] = {'texture': 'parts/%s.png' % part, 'pivot': [round(pivot_px[0], 1), round(pivot_px[1], 1)]}
            if name.startswith('Arm') and name.endswith('_Lower'):
                parts[part]['hand'] = hand_row

    # His tools and props, drawn and pencilled like him, each on its old canvas.
    tool_pieces = {'tool_nailgun': (NAILGUN[0], (0, 0, 150, 190), NAILGUN[1], False),
                   'PropFront': (PROP[0], (0, 0, 132, 306), PROP[1], False)}
    for name, (svg, grip) in HELD.items():
        tool_pieces['tool_' + name] = (svg, (0, 0, 190, 300), grip, False)
    for name, (svg, w, h) in SPECIAL_PROPS.items():
        tool_pieces[name] = (svg, (0, 0, w, h), (w / 2.0, h / 2.0), False)
    tools, _ = draw_parts(tool_pieces, fill, TOOL_C, 1.0, trim=False)
    poses = {}
    for name in sorted(tool_pieces):
        img, anchor, _ = tools[name]
        if name == 'PropFront':
            img.save(os.path.join(parts_dir, 'PropFront.png'))
            parts['PropFront'] = {'texture': 'parts/PropFront.png', 'pivot': [anchor[0], anchor[1]]}
            continue
        img.save(os.path.join(poses_dir, name + '.png'))
        poses[name] = {'texture': 'poses/%s.png' % name, 'anchor': [round(anchor[0], 1), round(anchor[1], 1)]}

    def at(point, origin):
        return [unit(point[0] - origin[0]), unit(point[1] - origin[1])]

    # Back to front: the far leg and arm, the body, the head, then the near arm on top of it all.
    bones = [
        {'name': 'Hip', 'parent': None, 'offset': [0, 0], 'part': None},
        {'name': 'LegBack_Upper', 'parent': 'Hip', 'offset': at(HIP_FAR, HIP), 'part': 'LegBack_Upper'},
        {'name': 'LegBack_Lower', 'parent': 'LegBack_Upper', 'offset': [0, unit(THIGH_LEN)], 'part': 'LegBack_Lower'},
        {'name': 'LegFront_Upper', 'parent': 'Hip', 'offset': at(HIP_NEAR, HIP), 'part': 'LegFront_Upper'},
        {'name': 'LegFront_Lower', 'parent': 'LegFront_Upper', 'offset': [0, unit(THIGH_LEN)], 'part': 'LegFront_Lower'},
        {'name': 'Torso', 'parent': 'Hip', 'offset': [0, 0], 'part': 'Torso'},
        # The far arm is not shaded: coming from behind his chest is enough (Eric, 2026-10-08).
        {'name': 'ArmBack_Upper', 'parent': 'Torso', 'offset': at(RIG_SHOULDER_BACK, HIP), 'part': 'ArmBack_Upper', 'darken': 1.0},
        {'name': 'ArmBack_Lower', 'parent': 'ArmBack_Upper', 'offset': [0, unit(UPPER_ARM_LEN)], 'part': 'ArmBack_Lower', 'darken': 1.0},
        {'name': 'Head', 'parent': 'Torso', 'offset': at(NECK, HIP), 'part': 'Head'},
        {'name': 'ArmFront_Upper', 'parent': 'Torso', 'offset': at(RIG_SHOULDER_FRONT, HIP), 'part': 'ArmFront_Upper'},
        {'name': 'ArmFront_Lower', 'parent': 'ArmFront_Upper', 'offset': [0, unit(UPPER_ARM_LEN)], 'part': 'ArmFront_Lower'},
        # Whatever he holds goes under his glove, so his fist closes over the grip.
        {'name': 'PropFront', 'parent': 'ArmFront_Lower', 'offset': [0, unit(FIST)], 'part': 'PropFront',
         'behindParent': True},
    ]
    extras = [
        # He wears the hard hat; "hardhat" is it lit up, which the game shows while it is armour.
        {'name': 'hat', 'bone': 'Head', 'part': 'Hat', 'offset': [0, 0], 'always': True},
        {'name': 'hardhat', 'bone': 'Head', 'part': 'HatShine', 'offset': [0, 0]},
        {'name': 'footBack', 'bone': 'LegBack_Lower', 'part': 'FootBack', 'offset': [0, unit(SHIN_LEN)], 'foot': True, 'always': True},
        {'name': 'footFront', 'bone': 'LegFront_Lower', 'part': 'FootFront', 'offset': [0, unit(SHIN_LEN)], 'foot': True, 'always': True},
    ]
    rig = {
        'name': 'Lug',
        # Hip to crown (the hat sits above it), and hip to sole with the legs straight.
        'canonicalHeight': unit(HIP[1] - CROWN_Y),
        'legLength': unit(THIGH_LEN + SHIN_LEN + SOLE),
        'backLimbDarken': 0.7,
        'bones': bones,
        'extras': extras,
        'poses': poses,
        'parts': parts,
    }
    with open(os.path.join(ROOT, 'rig.json'), 'wb') as fh:
        fh.write(json.dumps(rig, indent=2, sort_keys=True).encode('utf-8'))

    drawings['drawing'].convert('RGB').save(os.path.join(source_dir, 'drawing.png'))

    # Parts the old stick figure had and this one does not.
    for stale in ('HardHat.png', 'HardHat.png.import'):
        path = os.path.join(parts_dir, stale)
        if os.path.exists(path):
            os.remove(path)
    print('Lug -> %d parts, %d poses, canonical height %.1f, legs %.1f'
          % (len(parts), len(poses), rig['canonicalHeight'], rig['legLength']))


if __name__ == '__main__':
    build()
