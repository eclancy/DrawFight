# -*- coding: utf-8 -*-
"""
Side-by-side mockups of DoomBot's turn - nothing here touches his parts or rig. Run with the only
Python on this machine:

    python tools/art/doombot_front_mockup.py

Writes .shots/doombot_v3_mockups.png   the photo's head and torso beside the turn at three
                                       front:side proportions (60/40, 70/30, 80/20)
       .shots/doombot_v3_small.png     all four as whole fighters at about match size

The turn itself - his drawn side kept and squeezed, a new front facing us carrying copies of his
own eye, teeth and controls - is cut_doombot.py's (turn_head, turn_torso), so what this shows is
exactly what the cut builds. Eric picked A, 60/40 (2026-10-21), which is cut_doombot.FRONT_SHARE;
this is kept for trying another proportion. Arms and legs are as drawn, moved to where they now
attach (ghosted on the big sheet, with a marker at each joint).
"""

from __future__ import division, print_function

import os

from PIL import Image, ImageDraw, ImageEnhance, ImageFont

import cut_doombot as cd

SHOTS = os.path.join(cd.REPO, '.shots')
VERSIONS = [('A 60/40', 0.60), ('B 70/30', 0.70), ('C 80/20', 0.80)]


def darker(piece, k):
    rgb = ImageEnhance.Brightness(piece.convert('RGB')).enhance(k).convert('RGBA')
    rgb.putalpha(piece.split()[3])
    return rgb


# Everything is placed on a canvas PAD wider than the photo on every side, so a limb moved out
# past the edge of the photo is neither cut off nor wrapped round by the ring.
PAD = 160


def padded(layers):
    """(piece, dx, dy) layers composited in order onto the padded canvas."""
    out = Image.new('RGBA', (cd.WORK[0] + 2 * PAD, cd.WORK[1] + 2 * PAD), (0, 0, 0, 0))
    for piece, dx, dy in layers:
        layer = Image.new('RGBA', out.size, (0, 0, 0, 0))
        layer.paste(piece, (PAD + int(round(dx)), PAD + int(round(dy))))
        out = Image.alpha_composite(out, layer)
    return out


class Drawing(object):
    """Everything cut once from the photo."""

    def __init__(self):
        photo = Image.open(cd.PHOTO).convert('RGB').resize(cd.WORK, Image.LANCZOS)
        look = cd.levelled(photo)
        self.photo, self.look = photo, look
        self.head = cd.cut(photo, cd.HEAD, look)
        self.torso_raw = cd.cut(photo, cd.TORSO, look)
        self.torso = cd.fill_covered(cd.cut(photo, cd.TORSO, look), look)

        def limb(*polys):
            out = Image.new('RGBA', cd.WORK, (0, 0, 0, 0))
            for p in polys:
                out = Image.alpha_composite(out, cd.cut(photo, p, look))
            return out

        self.near_arm = limb(cd.RIGHT_UPPER, cd.RIGHT_LOWER)
        self.far_arm = darker(limb(cd.LEFT_UPPER, cd.LEFT_LOWER), 0.85)
        self.back_leg = limb(cd.BACK_LEG, cd.BACK_BOOT)
        self.front_leg = limb(cd.FRONT_LEG, cd.FRONT_BOOT)


def original(d):
    """The photo's DoomBot as drawn: the body, then the head and the arm across it."""
    body = Image.alpha_composite(d.torso_raw, d.head)
    joints = {'neck': cd.NECK, 'arm': cd.SOCKET[0], 'arm (behind)': cd.LEFT_SHOULDER,
              'leg': cd.BACK_HIP, 'leg ': cd.FRONT_HIP}
    return body, [(d.far_arm, 0, 0), (d.back_leg, 0, 0), (d.front_leg, 0, 0)], [(d.near_arm, 0, 0)], joints


def turned(d, share):
    """DoomBot's head and torso turned toward us, as cut_doombot.py builds them."""
    torso, t = cd.turn_torso(d.torso, d.look, share)
    head, h = cd.turn_head(d.head, d.photo, d.look, share)
    body = Image.alpha_composite(torso, head)
    sx, sy = t['side'].map(cd.SOCKET[0])
    far_x = t['front'][1] - (cd.FAR_ARM_EDGE - cd.LEFT_SHOULDER[0])
    bh, fh = t['across'](cd.BACK_HIP[0]), t['across'](cd.FRONT_HIP[0])
    joints = {'neck': h['neck'], 'arm': (sx, sy), 'arm (behind)': (far_x, cd.LEFT_SHOULDER[1]),
              'leg': (bh, cd.BACK_HIP[1]), 'leg ': (fh, cd.FRONT_HIP[1])}
    back = [(d.far_arm, far_x - cd.LEFT_SHOULDER[0], 0),
            (d.back_leg, bh - cd.BACK_HIP[0], 0),
            (d.front_leg, fh - cd.FRONT_HIP[0], 0)]
    front = [(d.near_arm, sx - cd.SOCKET[0][0], sy - cd.SOCKET[0][1])]
    return body, back, front, joints


def font(size, bold=False):
    for name in ('arialbd.ttf' if bold else 'arial.ttf', 'DejaVuSans.ttf'):
        try:
            return ImageFont.truetype(os.path.join(os.environ.get('WINDIR', 'C:/Windows'), 'Fonts', name), size)
        except IOError:
            pass
    return ImageFont.load_default()


PAPER = (238, 233, 222, 255)
CELL = (387, 160, 1067, 1390)


def big_cell(body, limbs_back, limbs_front, joints, label):
    """Head and torso large, the limbs ghosted where they attach, a marker at each joint."""
    fig = cd.ringed(padded([(body, 0, 0)]), below=PAD + cd.ANTENNA_BASE_Y)
    ghost = padded(limbs_back + limbs_front)
    ghost.putalpha(ghost.split()[3].point(lambda v: int(v * 0.3)))
    back = Image.new('RGBA', fig.size, PAPER)
    back = Image.alpha_composite(back, ghost)
    back = Image.alpha_composite(back, fig)
    draw = ImageDraw.Draw(back)
    f = font(22)
    for name, (x, y) in sorted(joints.items()):
        x, y = x + PAD, y + PAD
        r = 11
        draw.ellipse([x - r, y - r, x + r, y + r], outline=(20, 150, 160, 255), width=4)
        draw.line([(x - 5, y), (x + 5, y)], fill=(20, 150, 160, 255), width=3)
        draw.line([(x, y - 5), (x, y + 5)], fill=(20, 150, 160, 255), width=3)
        draw.text((x + 15, y - 13), name.strip(), font=f, fill=(20, 120, 130, 255))
    cell = back.crop((CELL[0] + PAD, CELL[1] + PAD, CELL[2] + PAD, CELL[3] + PAD))
    out = Image.new('RGBA', (cell.size[0], cell.size[1] + 80), PAPER)
    out.paste(cell, (0, 80))
    ImageDraw.Draw(out).text((24, 18), label, font=font(48, True), fill=(40, 34, 44, 255))
    return out


def whole(body, limbs_back, limbs_front):
    """The whole fighter, ringed, as the game would draw him."""
    fig = cd.ringed(padded(limbs_back + [(body, 0, 0)] + limbs_front), below=PAD + cd.ANTENNA_BASE_Y)
    return fig.crop(fig.getbbox())


def main():
    if not os.path.isdir(SHOTS):
        os.makedirs(SHOTS)
    d = Drawing()
    looks = [('original', original(d))] + [(label, turned(d, share)) for label, share in VERSIONS]

    cells = [big_cell(b, lb, lf, j, label) for label, (b, lb, lf, j) in looks]
    gap = 24
    w = sum(c.size[0] for c in cells) + gap * (len(cells) + 1)
    h = max(c.size[1] for c in cells) + gap * 2
    sheet = Image.new('RGBA', (w, h), (214, 208, 196, 255))
    x = gap
    for c in cells:
        sheet.paste(c, (x, gap))
        x += c.size[0] + gap
    sheet.convert('RGB').save(os.path.join(SHOTS, 'doombot_v3_mockups.png'))

    # At match size: about 150 pixels tall, on paper and on a lavender night, then twice that
    # (pixels doubled, nothing smoothed) to look at closely.
    tall = 150
    smalls = []
    for label, (b, lb, lf, j) in looks:
        fig = whole(b, lb, lf)
        k = tall / float(fig.size[1])
        smalls.append((label, fig.resize((int(round(fig.size[0] * k)), tall), Image.LANCZOS)))
    col = max(s.size[0] for _, s in smalls) + 60
    grounds = [PAPER, (150, 140, 182, 255)]
    rows = []
    for zoom in (1, 2):
        for ground in grounds:
            row = Image.new('RGBA', (col * len(smalls) * zoom, (tall + 50) * zoom), ground)
            dr = ImageDraw.Draw(row)
            for i, (label, s) in enumerate(smalls):
                img = s if zoom == 1 else s.resize((s.size[0] * 2, s.size[1] * 2), Image.NEAREST)
                row.alpha_composite(img, (i * col * zoom + 20 * zoom, 34 * zoom))
                dr.text((i * col * zoom + 10, 6), label, font=font(16 * zoom, True), fill=(30, 26, 34, 255))
            rows.append(row)
    sw = max(r.size[0] for r in rows)
    small = Image.new('RGBA', (sw, sum(r.size[1] for r in rows)), (214, 208, 196, 255))
    y = 0
    for r in rows:
        small.paste(r, (0, y))
        y += r.size[1]
    small.convert('RGB').save(os.path.join(SHOTS, 'doombot_v3_small.png'))
    print('wrote .shots/doombot_v3_mockups.png %s and .shots/doombot_v3_small.png %s' % (sheet.size, small.size))


if __name__ == '__main__':
    main()
