# -*- coding: utf-8 -*-
"""
MOCKUPS ONLY - nothing here touches DoomBot's parts or rig. Run with the only Python on this machine:

    python tools/art/doombot_front_mockup.py

Writes .shots/doombot_v3_mockups.png   the photo's head and torso beside three three-quarter
                                       versions (front:side 60/40, 70/30, 80/20)
       .shots/doombot_v3_small.png     all four as whole fighters at about match size

WHY. Eric wants DoomBot turned three-quarters toward the camera - all of his face and torso
seen from the front - while still facing right. His drawing is a SIDE view (Eric, 2026-10-21):
the red slash with the green tip is an antenna on the side of his head, the round grille eye is
his right eye, and the teeth run to the front edge of his face. The torso is drawn the same way:
the near arm comes out of a round shoulder socket set INTO the face of the body, which is what a
side shows (a front would have both arms off its edges), and the far arm pokes out of the front
edge. So both boxes are turned the same way: his drawn side kept, narrowed and set back on the
left, and a new front panel facing us on the right.

WHAT IS HIS AND WHAT IS NEW (rule 1, at Eric's explicit request). Every mark of his is copied,
mirrored, cropped or scaled, never redrawn:
  - the side of each box is the drawing left of the corner, squeezed narrower and tapered a
    little toward the far edge, with the antenna-ear and the shoulder socket and hatch on it;
  - the head's front is his own grey pencil (a clean patch of his head, tiled and mirrored),
    carrying his eye as drawn, a mirrored and slightly smaller copy of it (brow and all) as the
    far eye, and his teeth - the left half of them, mirrored - as a mouth across the front;
  - the chest's front is his grey pencil from inside the hatch, carrying the block of controls
    exactly as drawn (buttons, slits, keypad, gauge, wires, dial and the black slot);
  - the only new lines are the edges of the new front panels and the corner between side and
    front, in his marker colour at his line width.
Arms and legs are as drawn, only moved to where they now attach (ghosted on the big sheet, with
markers at each joint).
"""

from __future__ import division, print_function

import os

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont

import cut_doombot as cd

SHOTS = os.path.join(cd.REPO, '.shots')

# --- Where things are on the photo, in cut_doombot's half-size pixels ---------------------------

# The head: his drawn side, from its slanted back edge to just short of the teeth, where the
# front of his face would begin; the top and bottom lines there; and its front edge.
HEAD_LEFT, HEAD_CORNER, HEAD_FRONT = 525, 725, 930
HEAD_ROWS = (322, 617)
HEAD_TOP, HEAD_BOTTOM = 342, 610
# The antennae, above the head's top line (lifted off and stood back on the new top).
ANTENNAE = [(540, 170), (960, 170), (960, 338), (540, 318)]
BACK_ANTENNA_BASE = (695, 325)
FRONT_ANTENNA_BASE = (815, 331)
# His eye with the dark brow over it; his teeth from their left end to the middle of the third
# gap (mirrored about it, that is six teeth closed at both ends); the clean grey between the
# antenna-ear and the eye; and the short neck.
EYE_BOX = (786, 366, 895, 499)
TEETH_HALF = (727, 517, 852, 603)
HEAD_TEXTURE = (700, 362, 786, 505)
NECK_BOX = (632, 617, 762, 650)

# The torso: the hatch seam is the corner - the socket and the hatch on his side, the controls
# on his front.
TORSO_LEFT, TORSO_CORNER, TORSO_FRONT = 470, 680, 945
TORSO_ROWS = (626, 1368)
TORSO_TOP, TORSO_BOTTOM = 640, 1353
CONTROLS = (679, 742, 938, 1170)
TORSO_TEXTURE = (700, 1182, 928, 1338)
FAR_ARM_ROOT = (928, 760)

# How the versions are built: the share of each box's width that is front, how much wider than
# the drawn profile the turned box is, how much the far edge of the side shrinks, and the far eye.
VERSIONS = [('A 60/40', 0.60), ('B 70/30', 0.70), ('C 80/20', 0.80)]
WIDEN = 1.12
TAPER = 0.035
FAR_EYE = 0.88
LINE_W = 6


def mark_mask(photo, box):
    """His marks inside `box` - the dark lines and the coloured pencil - as a soft mask, so a
    copy of an eye takes the eye and not the grey round it."""
    crop = photo.crop(box)
    mask = Image.new('L', crop.size, 0)
    src, dst = crop.load(), mask.load()
    for y in range(crop.size[1]):
        for x in range(crop.size[0]):
            r, g, b = src[x, y]
            if (r + g + b) / 3.0 < 112 or max(r, g, b) - min(r, g, b) > 32:
                dst[x, y] = 255
    return mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(1.2))


def feathered(size, inset=5, blur=2.5):
    mask = Image.new('L', size, 0)
    ImageDraw.Draw(mask).rectangle([inset, inset, size[0] - 1 - inset, size[1] - 1 - inset], fill=255)
    return mask.filter(ImageFilter.GaussianBlur(blur))


def tiled(patch, size):
    """His pencil grey, tiled with every other copy mirrored so no seam lines up."""
    out = Image.new('RGBA', size, (0, 0, 0, 0))
    pw, ph = patch.size
    for j in range(0, size[1], ph):
        for i in range(0, size[0], pw):
            p = patch
            if (i // pw) % 2:
                p = p.transpose(Image.FLIP_LEFT_RIGHT)
            if (j // ph) % 2:
                p = p.transpose(Image.FLIP_TOP_BOTTOM)
            out.paste(p, (i, j))
    return out


class Side(object):
    """The drawn side of a box, from x0 to x1, squeezed to `width` and set so its right edge -
    the corner - lands at `corner`, its back edge a little shorter than the corner."""

    def __init__(self, x0, x1, rows, width, corner):
        self.x0, self.x1, self.rows, self.width, self.corner = x0, x1, rows, width, corner
        self.t = TAPER * (rows[1] - rows[0])

    def render(self, piece):
        y0, y1 = self.rows
        t = self.t
        quad = (self.x0, y0 - t, self.x0, y1 + t, self.x1, y1 + 0.0, self.x1, y0 + 0.0)
        return piece.transform((int(round(self.width)), y1 - y0), Image.QUAD, quad, Image.BICUBIC)

    def paste(self, canvas, piece):
        img = self.render(piece)
        canvas.alpha_composite(img, (int(round(self.corner - self.width)), self.rows[0]))

    def map(self, p):
        x, y = p
        y0, y1 = self.rows
        u = (x - self.x0) / float(self.x1 - self.x0)
        top, bottom = y0 - self.t * (1 - u), y1 + self.t * (1 - u)
        v = (y - top) / (bottom - top)
        return (self.corner - self.width + u * self.width, y0 + v * (y1 - y0))


def line_ink(look):
    """His marker colour: the darkest of the head's own front edge."""
    px = sorted(look.crop((923, 400, 935, 580)).getdata(), key=sum)
    px = px[:len(px) // 5]
    return tuple(int(sum(p[i] for p in px) / len(px)) for i in range(3)) + (255,)


def panel_lines(draw, x0, x1, top, bottom, ink):
    h = LINE_W / 2.0
    draw.line([(x0, top), (x1, top)], fill=ink, width=LINE_W)
    draw.line([(x1, top - h), (x1, bottom + h)], fill=ink, width=LINE_W)
    draw.line([(x0, bottom), (x1, bottom)], fill=ink, width=LINE_W)
    draw.line([(x0, top - h), (x0, bottom + h)], fill=ink, width=LINE_W)


def masked(img, mask):
    out = img.convert('RGBA')
    out.putalpha(ImageChops.multiply(out.split()[3], mask))
    return out


def darker(piece, k):
    rgb = ImageEnhance.Brightness(piece.convert('RGB')).enhance(k).convert('RGBA')
    rgb.putalpha(piece.split()[3])
    return rgb


def shifted(piece, dx, dy):
    """A piece and where it moves to: placed later on a padded canvas, so a limb moved out past
    the edge of the photo is not cut off (nor wrapped round by the ring)."""
    return (piece, dx, dy)


PAD = 160


def padded(layers):
    """(piece, dx, dy) layers composited in order onto a canvas PAD wider on every side."""
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
        self.ink = line_ink(look)
        head = cd.cut(photo, cd.HEAD, look)
        ant = cd.poly_mask(cd.WORK, ANTENNAE)
        self.head = head
        self.antennae = masked(head, ant)
        self.head_body = masked(head, ImageChops.invert(ant))
        self.torso_raw = cd.cut(photo, cd.TORSO, look)
        self.torso = cd.fill_covered(cd.cut(photo, cd.TORSO, look), look)

        def limb(*polys):
            out = Image.new('RGBA', cd.WORK, (0, 0, 0, 0))
            for p in polys:
                out = Image.alpha_composite(out, cd.cut(photo, p, look))
            return out

        self.near_arm = limb(cd.RIGHT_UPPER, cd.RIGHT_LOWER)
        self.far_arm = limb(cd.LEFT_UPPER, cd.LEFT_LOWER)
        self.back_leg = limb(cd.BACK_LEG, cd.BACK_BOOT)
        self.front_leg = limb(cd.FRONT_LEG, cd.FRONT_BOOT)

        self.eye = masked(look.crop(EYE_BOX), mark_mask(photo, EYE_BOX))
        half = look.crop(TEETH_HALF).convert('RGBA')
        hw, hh = half.size
        mouth = Image.new('RGBA', (hw * 2 - 2, hh), (0, 0, 0, 0))
        mouth.paste(half, (0, 0))
        mouth.paste(half.transpose(Image.FLIP_LEFT_RIGHT), (hw - 2, 0))
        self.mouth = mouth
        self.head_grey = look.crop(HEAD_TEXTURE).convert('RGBA')
        self.torso_grey = look.crop(TORSO_TEXTURE).convert('RGBA')
        self.neck = self.head.crop(NECK_BOX)


def original(d):
    """The photo's DoomBot as drawn: the body, then the head and the arm across it."""
    body = Image.alpha_composite(d.torso_raw, d.head)
    joints = {'neck': cd.NECK, 'arm': cd.SOCKET[0], 'arm (behind)': FAR_ARM_ROOT,
              'leg': cd.BACK_HIP, 'leg ': cd.FRONT_HIP}
    limbs_back = [shifted(darker(d.far_arm, 0.85), 0, 0), shifted(d.back_leg, 0, 0), shifted(d.front_leg, 0, 0)]
    return body, limbs_back, [shifted(d.near_arm, 0, 0)], joints


def turned(d, front_share):
    """DoomBot's head and torso turned three-quarters toward us."""
    canvas = Image.new('RGBA', cd.WORK, (0, 0, 0, 0))

    def split(left, right, corner_drawn):
        total = (right - left) * WIDEN
        wf, ws = total * front_share, total * (1 - front_share)
        middle = (left + right) / 2.0
        corner = middle - total / 2.0 + ws
        return corner, wf, ws

    # The torso.
    tc, twf, tws = split(TORSO_LEFT, TORSO_FRONT, TORSO_CORNER)
    tside = Side(TORSO_LEFT - 20, TORSO_CORNER, TORSO_ROWS, tws + 20 * tws / (TORSO_CORNER - TORSO_LEFT), tc)
    tside.paste(canvas, d.torso)
    tf0, tf1 = int(round(tc)), int(round(tc + twf))
    front = tiled(d.torso_grey, (tf1 - tf0, TORSO_BOTTOM - TORSO_TOP))
    canvas.alpha_composite(front, (tf0, TORSO_TOP))
    block = d.look.crop(CONTROLS).convert('RGBA')
    block.putalpha(feathered(block.size))
    bx = tf0 + int(round((twf - block.size[0]) / 2.0))
    canvas.alpha_composite(block, (bx, CONTROLS[1]))
    draw = ImageDraw.Draw(canvas)
    panel_lines(draw, tf0, tf1, TORSO_TOP, TORSO_BOTTOM, d.ink)

    # The neck, under the middle of the head, behind both boxes.
    hc, hwf, hws = split(HEAD_LEFT, HEAD_FRONT, HEAD_CORNER)
    neck_x = hc + hwf * 0.3 - (NECK_BOX[2] - NECK_BOX[0]) / 2.0
    under = Image.new('RGBA', cd.WORK, (0, 0, 0, 0))
    under.paste(d.neck, (int(round(neck_x)), NECK_BOX[1] - 3))
    canvas = Image.alpha_composite(under, canvas)

    # The head: the drawn side with its antenna-ear, then the new face.
    hside = Side(HEAD_LEFT - 8, HEAD_CORNER, HEAD_ROWS, hws + 8 * hws / (HEAD_CORNER - HEAD_LEFT), hc)
    hside.paste(canvas, d.head_body)
    hf0, hf1 = int(round(hc)), int(round(hc + hwf))
    canvas.alpha_composite(tiled(d.head_grey, (hf1 - hf0, HEAD_BOTTOM - HEAD_TOP)), (hf0, HEAD_TOP))
    # His right eye nearer the corner, as drawn; the far eye a mirrored copy, a little smaller.
    eye = d.eye
    ecy = 453 - EYE_BOX[1]
    near_cx = hf0 + hwf * 0.28
    canvas.alpha_composite(eye, (int(round(near_cx - (836 - EYE_BOX[0]))), EYE_BOX[1]))
    far = eye.transpose(Image.FLIP_LEFT_RIGHT)
    far = far.resize((int(far.size[0] * FAR_EYE), int(far.size[1] * FAR_EYE)), Image.LANCZOS)
    far_cx = hf0 + hwf * 0.72
    fcx_in = (EYE_BOX[2] - 836) * FAR_EYE
    canvas.alpha_composite(far, (int(round(far_cx - fcx_in)), int(round(453 - ecy * FAR_EYE))))
    # His teeth, mirrored into a mouth across the front.
    mw = int(round(hwf * 0.8))
    mouth = d.mouth.resize((mw, d.mouth.size[1]), Image.LANCZOS)
    mouth.putalpha(ImageChops.multiply(mouth.split()[3], feathered(mouth.size, inset=1, blur=1.2)))
    canvas.alpha_composite(mouth, (int(round(hf0 + (hwf - mw) / 2.0)), TEETH_HALF[1]))
    draw = ImageDraw.Draw(canvas)
    panel_lines(draw, hf0, hf1, HEAD_TOP, HEAD_BOTTOM, d.ink)

    # The antennae, stood back on the top: the back one over the side, the front one over the face.
    bx, by = hside.map(BACK_ANTENNA_BASE)
    back = masked(d.antennae, cd.poly_mask(cd.WORK, [(540, 170), (760, 170), (760, 340), (540, 340)]))
    canvas = Image.alpha_composite(canvas, padded([(back, bx - BACK_ANTENNA_BASE[0], by - BACK_ANTENNA_BASE[1])]).crop((PAD, PAD, PAD + cd.WORK[0], PAD + cd.WORK[1])))
    fx = hf0 + hwf * (FRONT_ANTENNA_BASE[0] - HEAD_CORNER) / float(HEAD_FRONT - HEAD_CORNER)
    fy = HEAD_TOP - LINE_W / 2.0
    frontant = masked(d.antennae, cd.poly_mask(cd.WORK, [(760, 170), (960, 170), (960, 340), (760, 340)]))
    canvas = Image.alpha_composite(canvas, padded([(frontant, fx - FRONT_ANTENNA_BASE[0], fy - FRONT_ANTENNA_BASE[1])]).crop((PAD, PAD, PAD + cd.WORK[0], PAD + cd.WORK[1])))

    # Where the limbs now attach: the near arm out of the socket on the side, the far arm behind
    # the front edge, the legs spread under the whole turned box.
    sx, sy = tside.map(cd.SOCKET[0])
    new_left, new_right = tc - tws, tc + twf

    def hip(p):
        return (new_left + (p[0] - TORSO_LEFT) / float(TORSO_FRONT - TORSO_LEFT) * (new_right - new_left), p[1])

    bh, fh = hip(cd.BACK_HIP), hip(cd.FRONT_HIP)
    far_root = (tf1 - 10, FAR_ARM_ROOT[1])
    joints = {'neck': (neck_x + (NECK_BOX[2] - NECK_BOX[0]) / 2.0, cd.NECK[1]), 'arm': (sx, sy),
              'arm (behind)': far_root, 'leg': bh, 'leg ': fh}
    limbs_back = [shifted(darker(d.far_arm, 0.85), far_root[0] - FAR_ARM_ROOT[0], 0),
                  shifted(d.back_leg, bh[0] - cd.BACK_HIP[0], 0),
                  shifted(d.front_leg, fh[0] - cd.FRONT_HIP[0], 0)]
    limbs_front = [shifted(d.near_arm, sx - cd.SOCKET[0][0], sy - cd.SOCKET[0][1])]
    return canvas, limbs_back, limbs_front, joints


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
