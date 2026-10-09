# -*- coding: utf-8 -*-
"""
Line weight for a kid's drawing: every line of ink made wider by the same amount, in place, so
it can be seen at the size a fighter is drawn in a match.

Elim draws in a thin line on a big canvas. Shrunk to a fighter's size - a tenth or so of the
drawing - a line comes out under one pixel wide, and with no mipmaps it breaks up and shimmers
as the fighter moves. Eric's call (2026-10-04): thicken it, keeping the drawings.

What this does and does not do, so it stays on the right side of rule 1 in CLAUDE.md:
  - every pixel of ink (dark and opaque) stays exactly where it is, and every pixel within
    `radius` of one is inked too - the same line drawn with a fatter pen along the same path
  - nothing is moved, smoothed, straightened, filled in or redrawn; colours other than the ink
    are untouched except where the wider line now covers them
  - it is applied only to the GENERATED parts and poses, by the cut scripts. The drawings in
    source/ are never changed; set the cut script's LINE_BOOST to 0 and re-cut to get the thin
    lines back

The pen is round, not square: the line is grown one pixel at a time, alternating a square step
with a plus-shaped one, which comes out as an octagon - near enough a circle that diagonal lines
thicken as much as straight ones and line ends stay rounded.
"""

from __future__ import division

from PIL import Image, ImageChops, ImageFilter


def is_ink(p):
    r, g, b, a = p
    return a > 128 and r < 90 and g < 90 and b < 90


def thicken(img, radius, ink=(0, 0, 0, 255)):
    """The image padded by `radius` (+2) pixels all round, with every pixel within `radius` of a
    line of ink painted in ink. Returns (image, pad): add `pad` to any point measured on the
    unpadded image - a pivot, an anchor - to find it on the new one."""
    if radius <= 0:
        return img, 0
    pad = radius + 2
    w, h = img.size
    out = Image.new('RGBA', (w + 2 * pad, h + 2 * pad), (0, 0, 0, 0))
    out.paste(img, (pad, pad))

    mask = Image.new('L', out.size, 0)
    src = out.load()
    dst = mask.load()
    for y in range(out.size[1]):
        for x in range(out.size[0]):
            if is_ink(src[x, y]):
                dst[x, y] = 255

    for step in range(radius):
        if step % 2 == 0:
            mask = mask.filter(ImageFilter.MaxFilter(3))
        else:
            mask = ImageChops.lighter(
                ImageChops.lighter(mask, ImageChops.offset(mask, 1, 0)),
                ImageChops.lighter(ImageChops.lighter(ImageChops.offset(mask, -1, 0), ImageChops.offset(mask, 0, 1)),
                                   ImageChops.offset(mask, 0, -1)))

    pen = Image.new('RGBA', out.size, ink)
    return Image.composite(pen, out, mask), pad
