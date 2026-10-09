# -*- coding: utf-8 -*-
"""
Draws Flambe - a stand-in, in place of the stick figure Swift - and rigs him:

    python tools/art/flambe.py

A hotshot punk rocker, three-quarter view facing right: a flame mohawk, a black leather jacket
with spikes on the shoulders and studs on the collar, flames up its hem, a wallet chain, dark red
skinny jeans and chunky boots - and, because he is also somehow a French chef, a red
neckerchief. Confident and defiant: brows down, eyes ahead, a smirk. Lean and long in the leg, because he fights with his feet. Coloured in
coloured pencil inside a black marker outline, with nothing finer than a brow or a stripe, like
Lugnut (Eric's call, 2026-10-08). The folder stays fighters/swift.

Drawn, rasterised and pencilled as drawn.py describes, so this needs $GODOT_BIN. His fire wings
and frying pan are drawn the same way, in his style, each on the canvas and grip the stick-figure
drawing it replaced had.

Emits:
    fighters/swift/parts/*.png      the rig's parts, trimmed, with their pivots in rig.json
    fighters/swift/poses/*.png      his wings and pan
    fighters/swift/rig.json
    fighters/swift/source/drawing.png   the whole figure, as the example is drawn
"""

from __future__ import division, print_function

import json
import math
import os

from PIL import Image

from drawn import REPO, along, draw_parts, mirrored, place

ROOT = os.path.join(REPO, 'fighters', 'swift')

# Canonical units per drawing unit. His wings and pan are the size they were beside the stick
# figure, which stood about 613 units tall; at 0.74 he stands the same.
SCALE = 0.74

C = dict(
    ink='#1e1a22', skin='#efb489', shaved='#c99a82', leather='#3f3a48', lapel='#57506a',
    metal='#c9ccd4', tee='#f1ede4', scarf='#d3313b', band='#d3313b', jeans='#9a2c3e',
    boot='#3f3a48', flame_red='#e2402b', flame_orange='#f7891f', flame_yellow='#fbd23a',
    white='#f6f3ec',
    # Outlines a touch heavier than a kid's marker, so they hold at match size.
    wo=8, wi=5,
)

# --- the limbs, each hanging straight down from its joint at (0, 0) ---------------------------

UPPER_ARM_LEN = 135.0
FIST = 158.0           # elbow to the middle of the fist: where a pan is held
HAND_TOP = 120.0       # elbow to the top of the hand: below it is hand, above it is arm
THIGH_LEN = 170.0
SHIN_LEN = 165.0
SOLE = 68.0            # ankle to the bottom of the boot
STANCE = 48.0          # how much further apart than his hips his feet stand, each way


def spike(angle, radius=36.0, y0=-6.0, half=11.0, length=36.0):
    """A silver cone on the rounded top of a shoulder, pointing out from it at angle (0 = up)."""
    a = math.radians(angle)
    bx, by = radius * math.sin(a), y0 - radius * math.cos(a)
    dx, dy = math.sin(a), -math.cos(a)
    px, py = math.cos(a), math.sin(a)
    return ('<path d="M %.1f,%.1f L %.1f,%.1f L %.1f,%.1f Z" fill="{metal}" stroke="{ink}" '
            'stroke-width="4" stroke-linejoin="round"/>'
            % (bx - px * half, by - py * half, bx + dx * length, by + dy * length, bx + px * half, by + py * half))


# A leather sleeve with three spikes on the shoulder.
UPPER_ARM = (''.join(spike(a) for a in (-58, 0, 58)) + '''
<path d="M -36,-6 C -38,-42 36,-42 36,-6 C 40,40 34,100 28,135
         C 26,160 -26,160 -28,135 C -34,100 -40,40 -36,-6 Z" fill="{leather}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
''')

# The sleeve down to a red studded wristband, and a bare fist.
FOREARM = '''
<path d="M -28,-4 C -28,-24 28,-24 28,-4 C 34,30 32,70 26,104 L -26,104
         C -32,70 -34,30 -28,-4 Z" fill="{leather}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M -30,98 L 30,98 L 32,124 L -32,124 Z" fill="{band}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<circle cx="-12" cy="111" r="6" fill="{metal}" stroke="{ink}" stroke-width="3"/>
<circle cx="12" cy="111" r="6" fill="{metal}" stroke="{ink}" stroke-width="3"/>
<path d="M -32,124 C -42,124 -44,132 -44,142 L -44,176 C -44,192 -34,198 -20,198 L 22,198
         C 38,198 44,190 44,176 L 44,140 C 44,130 38,124 30,124 Z" fill="{skin}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
'''

# The thumb across the empty fist. The hand that holds his pan has none: its handle runs under it.
THUMB = '''
<path d="M -44,150 C -26,148 -12,156 -8,170" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>
'''

# Skinny dark red jeans.
THIGH = '''
<path d="M -32,-6 C -32,-34 32,-34 32,-6 C 34,50 32,120 26,170
         C 24,194 -24,194 -26,170 C -32,120 -34,50 -32,-6 Z" fill="{jeans}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
'''

# The jeans tucked into the shaft of his boot, which is drawn on the shin so it leans with it:
# a boot standing upright on a leaning shin had the shin going into it off-centre (Eric,
# 2026-10-08). Two silver buckles round the shaft.
SHIN = '''
<path d="M -26,-6 C -26,-30 26,-30 26,-6 C 28,50 26,100 25,128 L -25,128
         C -26,100 -28,50 -26,-6 Z" fill="{jeans}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M -31,114 L 31,114 L 32,166 C 32,180 -32,180 -32,166 Z" fill="{boot}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M -33,124 L 33,124 L 33,136 L -33,136 Z M -33,146 L 33,146 L 33,158 L -33,158 Z" fill="{metal}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
'''

# The foot of a chunky combat boot, from the ankle at (0, 0): rounded over the ankle, where it
# meets the shaft, sole flat, toe forward. Cut as a foot, so the game keeps it flat on the floor.
BOOT = '''
<path d="M -32,-6 C -32,-22 32,-22 32,-6 L 33,20 C 54,22 76,30 84,44 L 86,54 L -38,54 L -36,20 Z" fill="{boot}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M -42,50 L 90,50 L 90,68 L -42,68 Z" fill="{ink}" stroke="{ink}" stroke-width="4" stroke-linejoin="round"/>
'''

# --- the body and head, drawn where they stand in the figure ---------------------------------

HIP = (412.0, 572.0)
NECK = (437.0, 332.0)
SHOULDER_NEAR, SHOULDER_FAR = (342.0, 374.0), (512.0, 370.0)
HIP_NEAR, HIP_FAR = (384.0, 574.0), (444.0, 574.0)
CROWN_Y = 152.0

TORSO_PATH = ('M 404,334 C 380,338 350,342 330,350 C 312,358 306,378 310,400 '
              'C 316,440 340,480 352,520 C 356,544 352,566 350,592 '
              'C 380,604 446,604 478,592 C 476,566 472,544 474,520 '
              'C 486,480 516,440 528,410 C 540,384 540,362 526,350 '
              'C 506,342 482,338 462,334 Z')
# The outline, open along the bottom so the body runs into the legs without a line across them.
TORSO_LINE = ('M 478,592 C 476,566 472,544 474,520 C 486,480 516,440 528,410 C 540,384 540,362 526,350 '
              'C 506,342 482,338 462,334 L 404,334 C 380,338 350,342 330,350 C 312,358 306,378 310,400 '
              'C 316,440 340,480 352,520 C 356,544 352,566 350,592')
# The jacket, open down the middle over his tee.
JACKET_NEAR = 'M 250,300 L 414,334 C 420,400 418,480 410,600 L 250,620 Z'
JACKET_FAR = 'M 466,334 C 478,400 482,480 470,600 L 600,620 L 600,300 Z'
TORSO = '''
<clipPath id="torso"><path d="{tp}"/></clipPath>
<clipPath id="jacket"><path d="{jn}"/><path d="{jf}"/></clipPath>
<path d="{tp}" fill="{tee}"/>
<g clip-path="url(#torso)">
  <path d="M 440,372 C 452,392 466,398 462,420 C 474,410 476,396 474,384 C 486,398 488,420 478,440
           C 470,456 446,462 432,452 C 418,442 416,424 424,412 C 426,424 432,428 438,428 C 432,410 432,390 440,372 Z"
        fill="{flame_orange}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
  <path d="M 446,420 C 452,428 460,432 458,444 C 450,450 438,446 438,438 C 442,436 446,430 446,420 Z" fill="{flame_yellow}"/>
  <path d="M 250,546 L 600,546 L 600,566 L 250,566 Z" fill="{ink}"/>
  <path d="M 432,544 L 456,544 L 456,568 L 432,568 Z" fill="{metal}" stroke="{ink}" stroke-width="{wi}"/>
  <path d="{jn}" fill="{leather}"/>
  <path d="{jf}" fill="{leather}"/>
  <g clip-path="url(#jacket)">
    <path d="M 250,600 L 250,560 C 270,540 280,520 284,496 C 296,516 300,534 296,552 C 310,530 318,508 316,484
             C 334,506 340,532 330,556 C 346,540 356,522 358,500 C 374,524 378,550 366,572 C 382,560 392,544 394,526
             C 410,548 410,572 404,600 Z" fill="{flame_orange}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
    <path d="M 460,600 C 456,576 460,556 470,540 C 476,556 476,570 474,580 C 486,566 494,548 494,528
             C 512,550 514,576 506,600 Z" fill="{flame_orange}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
    <path d="M 290,600 C 290,584 294,572 302,562 C 306,576 306,588 304,600 Z M 340,600 C 340,584 346,570 354,560
             C 356,576 356,588 352,600 Z" fill="{flame_yellow}"/>
  </g>
  <path d="M 414,334 C 420,400 418,480 410,600" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>
  <path d="M 466,334 C 478,400 482,480 470,600" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>
  <path d="M 404,336 C 396,380 380,410 362,430 C 380,420 398,400 414,372 Z" fill="{lapel}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
  <path d="M 466,336 C 482,374 498,398 516,412 C 500,398 486,380 476,350 Z" fill="{lapel}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
</g>
<path d="{tl}" fill="none" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round" stroke-linecap="round"/>
<circle cx="390" cy="392" r="6" fill="{metal}" stroke="{ink}" stroke-width="3"/>
<circle cx="378" cy="408" r="6" fill="{metal}" stroke="{ink}" stroke-width="3"/>
<path d="M 372,594 C 366,640 410,656 436,600" fill="none" stroke="{ink}" stroke-width="12" stroke-linecap="round"/>
<path d="M 372,594 C 366,640 410,656 436,600" fill="none" stroke="{metal}" stroke-width="6" stroke-linecap="round" stroke-dasharray="10 5"/>
'''

HEAD_PATH = ('M 380,204 C 380,172 410,152 440,154 C 478,156 498,178 498,206 L 500,220 '
             'L 522,238 L 500,246 C 504,256 508,268 506,282 C 500,298 478,308 452,306 '
             'C 426,306 404,296 394,282 C 382,266 378,236 380,204 Z')
# The flame mohawk, red to yellow, swept back off his head. Drawn first: the skull covers its root.
MOHAWK = '''
<path d="M 384,190 C 378,160 362,134 344,118 C 374,124 390,136 398,150 C 394,118 384,92 366,70
         C 402,86 420,112 422,140 C 424,104 418,76 406,48 C 444,74 452,110 448,140 C 456,114 468,96 488,84
         C 484,112 476,134 474,152 C 484,142 496,138 510,136 C 498,150 492,166 492,184 Z"
      fill="{flame_red}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M 400,182 C 398,160 392,142 382,128 C 402,138 412,152 414,166 C 416,136 412,114 402,94
         C 428,116 436,142 434,166 C 442,146 452,132 468,122 C 464,142 460,160 460,180 Z" fill="{flame_orange}"/>
<path d="M 416,180 C 414,164 412,152 406,140 C 420,150 426,162 426,172 C 430,160 436,152 446,146
         C 444,160 442,170 442,180 Z" fill="{flame_yellow}"/>
'''
# Confident and defiant: brows down hard, eyes open and looking ahead, a one-sided smirk with
# the lips shut. No moustache (Eric, 2026-10-08).
HEAD = MOHAWK + '''
<clipPath id="head"><path d="{hp}"/></clipPath>
<path d="M 414,336 L 420,290 L 458,290 L 464,336 Z" fill="{skin}"/>
<path d="M 414,330 L 420,290 M 458,290 L 464,330" fill="none" stroke="{ink}" stroke-width="{wo}" stroke-linecap="round"/>
<path d="{hp}" fill="{skin}"/>
<g clip-path="url(#head)">
  <path d="M 370,150 L 476,150 C 452,176 424,186 404,206 L 386,236 L 370,240 Z" fill="{shaved}"/>
</g>
<path d="{hp}" fill="none" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>
<path d="M 413,224 C 406,214 392,216 388,228 C 385,238 390,248 393,256 C 395,264 400,268 406,266
         C 410,264 411,258 412,252 C 413,244 414,234 413,224 Z" fill="{skin}"/>
<path d="M 413,224 C 406,214 392,216 388,228 C 385,238 390,248 393,256 C 395,264 400,268 406,266"
      fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round"/>
<path d="M 407,228 C 399,226 395,234 398,242 C 400,247 404,248 406,244" fill="none" stroke="{ink}" stroke-width="3.5" stroke-linecap="round"/>
<circle cx="401" cy="272" r="8" fill="none" stroke="{ink}" stroke-width="9"/>
<circle cx="401" cy="272" r="8" fill="none" stroke="{metal}" stroke-width="4"/>
<path d="M 436,200 L 482,214 L 479,224 L 438,211 Z" fill="{ink}" stroke="{ink}" stroke-width="3" stroke-linejoin="round"/>
<path d="M 486,216 L 503,204 L 506,212 L 489,225 Z" fill="{ink}" stroke="{ink}" stroke-width="3" stroke-linejoin="round"/>
<path d="M 446,237 C 451,227 469,226 479,232 C 472,244 455,245 446,237 Z" fill="{white}" stroke="{ink}" stroke-width="3"/>
<circle cx="473" cy="235" r="4.6" fill="{ink}"/>
<path d="M 446,232 L 480,229" fill="none" stroke="{ink}" stroke-width="4" stroke-linecap="round"/>
<path d="M 488,238 C 491,233 499,232 504,234 C 501,241 493,242 488,238 Z" fill="{white}" stroke="{ink}" stroke-width="3"/>
<circle cx="500" cy="236" r="3.2" fill="{ink}"/>
<path d="M 487,235 L 505,232" fill="none" stroke="{ink}" stroke-width="4" stroke-linecap="round"/>
<path d="M 470,266 C 474,274 484,278 494,278 C 498,278 502,277 505,275" fill="none" stroke="{ink}" stroke-width="{wi}" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M 466,261 C 463,267 464,273 468,278" fill="none" stroke="{ink}" stroke-width="3.5" stroke-linecap="round"/>
<path d="M 410,334 L 466,334 L 452,352 L 440,368 L 426,350 Z" fill="{scarf}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
'''


def fill(svg, colours=C):
    values = dict(colours, tp=TORSO_PATH, tl=TORSO_LINE, hp=HEAD_PATH, jn=JACKET_NEAR, jf=JACKET_FAR)
    return svg.format(**values)


# --- his pan and wings, drawn in his style ---------------------------------------------------

# Each keeps the canvas, grip and anchor the old stick-figure drawing had, so the game holds,
# sizes and measures it exactly as before. Drawn at one canonical unit per drawing unit, with
# outlines about as heavy as his own at that size.
TOOL_C = dict(C, pan='#3c3842', pan_face='#6d6f7a', wood='#c08a52', wo=6, wi=4)

# The flambe pan, held tip up by its handle on a 190 x 300 canvas, fire roaring up off it.
PAN = ('''
<rect x="86" y="148" width="18" height="144" rx="9" fill="{wood}" stroke="{ink}" stroke-width="{wo}"/>
<ellipse cx="95" cy="126" rx="62" ry="34" fill="{pan}" stroke="{ink}" stroke-width="{wo}"/>
<ellipse cx="95" cy="122" rx="50" ry="24" fill="{pan_face}" stroke="{ink}" stroke-width="{wi}"/>
<path d="M 50,124 C 40,96 52,74 62,56 C 64,74 70,84 76,88 C 72,60 82,30 98,6 C 100,34 108,54 118,66
         C 120,52 128,40 140,32 C 136,56 150,90 140,124 Z" fill="{flame_red}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
<path d="M 64,124 C 58,104 66,90 72,80 C 74,94 80,100 86,102 C 86,80 92,60 102,44 C 104,66 112,80 122,88
         C 124,98 130,108 126,124 Z" fill="{flame_orange}"/>
<path d="M 80,124 C 78,112 82,104 88,98 C 90,106 94,110 100,110 C 100,100 104,92 110,86 C 112,100 116,112 112,124 Z"
      fill="{flame_yellow}"/>
<path d="M 33,126 C 33,145 61,160 95,160 C 129,160 157,145 157,126 C 157,136 129,146 95,146 C 61,146 33,136 33,126 Z"
      fill="{pan}" stroke="{ink}" stroke-width="{wi}" stroke-linejoin="round"/>
''', (95, 270))


# His two fire wings, one each side of his back with a gap between them: each a curved leading
# edge sweeping up and out to a point, with five flame feathers hanging off it - red outside,
# orange, then yellow at the heart, each smaller and nearer the root. On the 860 x 330 canvas, hinged
# at (430, 240) where they meet his back; the game flaps each one about it.
WING = ('M 448,246 C 466,170 560,84 706,66 C 676,92 668,116 690,146 C 660,136 646,126 636,120 '
        'C 628,148 630,172 646,198 C 612,182 596,166 588,156 C 578,186 580,214 596,242 '
        'C 562,224 548,204 540,192 C 530,222 532,252 548,282 C 516,262 502,240 496,226 '
        'C 486,252 486,280 498,304 C 474,286 458,266 448,246 Z')
ONE_WING = ('<path d="%s" fill="{flame_red}" stroke="{ink}" stroke-width="{wo}" stroke-linejoin="round"/>'
            '<path d="%s" fill="{flame_orange}" transform="translate(448,246) scale(0.74) translate(-448,-246)"/>'
            '<path d="%s" fill="{flame_yellow}" transform="translate(448,246) scale(0.46) translate(-448,-246)"/>'
            % (WING, WING, WING))
WINGS = (ONE_WING + '<g transform="translate(860,0) scale(-1,1)">' + ONE_WING + '</g>', (430.0, 240.0))


# --- the figure as drawn ---------------------------------------------------------------------

def rocking(colours=C):
    """The whole figure, a hotshot: a big step, one fist punched up in the air, one on his hip."""
    ground = 930.0
    ankle_y = ground - SOLE

    def shin_to_floor(knee):
        return math.degrees(math.acos(max(-1.0, min(1.0, (ankle_y - knee[1]) / SHIN_LEN))))

    near_elbow = along(SHOULDER_NEAR, 140, UPPER_ARM_LEN)
    far_elbow = along(SHOULDER_FAR, -40, UPPER_ARM_LEN)
    near_knee = along(HIP_NEAR, 18, THIGH_LEN)
    far_knee = along(HIP_FAR, -24, THIGH_LEN)
    near_shin, far_shin = shin_to_floor(near_knee), shin_to_floor(far_knee)
    arm, forearm = fill(UPPER_ARM, colours), fill(FOREARM + THUMB, colours)
    thigh, shin, boot = fill(THIGH, colours), fill(SHIN, colours), fill(BOOT, colours)
    out = [
        place(mirrored(arm), SHOULDER_FAR, -40),
        place(mirrored(forearm), far_elbow, 150),
        place(thigh, HIP_FAR, -24),
        place(shin, far_knee, far_shin),
        place(boot, along(far_knee, far_shin, SHIN_LEN)),
        place(thigh, HIP_NEAR, 18),
        place(shin, near_knee, near_shin),
        place(boot, along(near_knee, near_shin, SHIN_LEN)),
        fill(TORSO, colours), fill(HEAD, colours),
        place(mirrored(arm), SHOULDER_NEAR, 140),
        place(mirrored(forearm), near_elbow, 175),
    ]
    return '\n'.join(out)


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
        'Torso': (TORSO, (250, 300, 620, 680), HIP, False),
        'Head': (HEAD, (320, 20, 560, 380), NECK, False),
        'Arm_Upper': (UPPER_ARM, limb_box, (0, 0), True),
        'ArmFront_Lower': (FOREARM, limb_box, (0, 0), True),
        'ArmBack_Lower': (FOREARM + THUMB, limb_box, (0, 0), True),
        'Leg_Upper': (THIGH, limb_box, (0, 0), False),
        'Leg_Lower': (SHIN, limb_box, (0, 0), False),
        'Foot': (BOOT, limb_box, (0, 0), False),
    }
    cut, drawings = draw_parts(pieces, fill, C, SCALE, seed_prefix='flambe/',
                               extra={'drawing': (rocking, (130, 20, 720, 950), '#f7f2e8')})

    parts = {}
    for name, (svg, box, pivot, _) in sorted(pieces.items()):
        img, pivot_px, top = cut[name]
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
                parts[part]['hand'] = round((HAND_TOP - box[1]) * SCALE - top, 1)

    # Nothing in his hand until a move puts his pan there: a blank prop part, the size the old one
    # was. His pan and wings, drawn and pencilled like him, each on its old canvas.
    blank = Image.new('RGBA', (24, 26), (0, 0, 0, 0))
    blank.save(os.path.join(parts_dir, 'PropFront.png'))
    parts['PropFront'] = {'texture': 'parts/PropFront.png', 'pivot': [12.0, 10.0], 'empty': True}
    tools, _ = draw_parts({'tool_pan': (PAN[0], (0, 0, 190, 300), PAN[1], False),
                           'wings': (WINGS[0], (0, 0, 860, 330), WINGS[1], False)},
                          fill, TOOL_C, 1.0, seed_prefix='flambe/', trim=False)
    poses = {}
    for name in ('tool_pan', 'wings'):
        img, anchor, _ = tools[name]
        img.save(os.path.join(poses_dir, name + '.png'))
        poses[name] = {'texture': 'poses/%s.png' % name, 'anchor': [round(anchor[0], 1), round(anchor[1], 1)]}

    def at(point, origin):
        return [unit(point[0] - origin[0]), unit(point[1] - origin[1])]

    # Back to front: the far leg and arm, the body, the head, then the near arm on top of it all.
    # Facing right in three-quarter view, his near side is on the left of the drawing; the far arm
    # comes from behind his chest and is not shaded, like Lugnut's.
    bones = [
        {'name': 'Hip', 'parent': None, 'offset': [0, 0], 'part': None},
        {'name': 'LegBack_Upper', 'parent': 'Hip', 'offset': at(HIP_FAR, HIP), 'part': 'LegBack_Upper'},
        {'name': 'LegBack_Lower', 'parent': 'LegBack_Upper', 'offset': [0, unit(THIGH_LEN)], 'part': 'LegBack_Lower'},
        {'name': 'LegFront_Upper', 'parent': 'Hip', 'offset': at(HIP_NEAR, HIP), 'part': 'LegFront_Upper'},
        {'name': 'LegFront_Lower', 'parent': 'LegFront_Upper', 'offset': [0, unit(THIGH_LEN)], 'part': 'LegFront_Lower'},
        {'name': 'Torso', 'parent': 'Hip', 'offset': [0, 0], 'part': 'Torso'},
        {'name': 'ArmBack_Upper', 'parent': 'Torso', 'offset': at(SHOULDER_FAR, HIP), 'part': 'ArmBack_Upper', 'darken': 1.0},
        {'name': 'ArmBack_Lower', 'parent': 'ArmBack_Upper', 'offset': [0, unit(UPPER_ARM_LEN)], 'part': 'ArmBack_Lower', 'darken': 1.0},
        {'name': 'Head', 'parent': 'Torso', 'offset': at(NECK, HIP), 'part': 'Head'},
        {'name': 'ArmFront_Upper', 'parent': 'Torso', 'offset': at(SHOULDER_NEAR, HIP), 'part': 'ArmFront_Upper'},
        {'name': 'ArmFront_Lower', 'parent': 'ArmFront_Upper', 'offset': [0, unit(UPPER_ARM_LEN)], 'part': 'ArmFront_Lower'},
        # Whatever he holds goes under his fist, so it closes over the grip.
        {'name': 'PropFront', 'parent': 'ArmFront_Lower', 'offset': [0, unit(FIST)], 'part': 'PropFront',
         'behindParent': True},
    ]
    extras = [
        {'name': 'footBack', 'bone': 'LegBack_Lower', 'part': 'FootBack', 'offset': [0, unit(SHIN_LEN)], 'foot': True, 'always': True},
        {'name': 'footFront', 'bone': 'LegFront_Lower', 'part': 'FootFront', 'offset': [0, unit(SHIN_LEN)], 'foot': True, 'always': True},
    ]
    rig = {
        'name': 'Swift',
        # Hip to crown (the mohawk stands above it), and hip to sole with the legs straight.
        'canonicalHeight': unit(HIP[1] - CROWN_Y),
        'legLength': unit(THIGH_LEN + SHIN_LEN + SOLE),
        # Standing, his feet go a stride apart, not each under its hip: a hotshot's stance.
        'stance': unit(STANCE),
        'backLimbDarken': 0.7,
        'bones': bones,
        'extras': extras,
        'poses': poses,
        'parts': parts,
    }
    with open(os.path.join(ROOT, 'rig.json'), 'wb') as fh:
        fh.write(json.dumps(rig, indent=2, sort_keys=True).encode('utf-8'))

    drawings['drawing'].convert('RGB').save(os.path.join(source_dir, 'drawing.png'))
    print('Flambe -> %d parts, %d poses, canonical height %.1f, legs %.1f'
          % (len(parts), len(poses), rig['canonicalHeight'], rig['legLength']))


if __name__ == '__main__':
    build()
