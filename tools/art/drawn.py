# -*- coding: utf-8 -*-
"""
What the stand-ins we draw ourselves have in common - Lugnut (lugnut.py) and Flambe (flambe.py):
each, and every weapon and prop of theirs, is drawn as SVG, part by part, rasterised by Godot,
and coloured in coloured pencil inside a black marker outline. Not a script to run.

A part is drawn in its canonical orientation (see fighters/README.md): a limb hangs straight
down from its joint at (0, 0); the body and head are drawn where they stand in the figure, about
their joints. Godot rasterises (svg_raster.gd), because PIL cannot draw a curve - so anything
using this needs $GODOT_BIN.
"""

from __future__ import division, print_function

import json
import math
import os
import random
import shutil
import subprocess
import tempfile
import zlib

from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))

PAD = 6

# Paper, showing through the pencil.
PAPER = (250, 247, 239)


def ink_only(colours):
    """
    The same palette with only the black marker left: every colour white, so a line hidden under
    a part drawn over it stays hidden. Rendered with it, a drawing says which pixels are ink.
    """
    return dict((k, '#ffffff' if isinstance(v, str) and k != 'ink' else v) for k, v in colours.items())


def mirrored(svg):
    """Turned to face the other way, about x = 0."""
    return '<g transform="scale(-1,1)">%s</g>' % svg


def svg_doc(body, box, scale, background=None):
    x0, y0, x1, y1 = box
    bg = '<rect x="%g" y="%g" width="%g" height="%g" fill="%s"/>' % (x0, y0, x1 - x0, y1 - y0, background) if background else ''
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="%.1f" height="%.1f" viewBox="%g %g %g %g">%s%s</svg>'
            % ((x1 - x0) * scale, (y1 - y0) * scale, x0, y0, x1 - x0, y1 - y0, bg, body))


def rasterise(docs):
    """SVG documents to PIL images, through Godot. docs: {name: svg text}."""
    godot = os.environ.get('GODOT_BIN')
    if not godot or not os.path.exists(godot):
        raise SystemExit('this needs $GODOT_BIN to rasterise the drawing (see CLAUDE.md)')
    tmp = tempfile.mkdtemp(prefix='drawn_')
    try:
        jobs = []
        for name, text in docs.items():
            svg = os.path.join(tmp, name + '.svg')
            with open(svg, 'wb') as fh:
                fh.write(text.encode('utf-8'))
            jobs.append([svg.replace('\\', '/'), os.path.join(tmp, name + '.png').replace('\\', '/')])
        job_file = os.path.join(tmp, 'jobs.json')
        with open(job_file, 'wb') as fh:
            fh.write(json.dumps(jobs).encode('utf-8'))
        subprocess.check_call([godot, '--headless', '--script', os.path.join(HERE, 'svg_raster.gd'), '--', job_file],
                              cwd=REPO)
        images = {}
        for name in docs:
            images[name] = Image.open(os.path.join(tmp, name + '.png')).convert('RGBA')
            images[name].load()
        return images
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def trimmed(img, pivot_px):
    """Cuts away the empty canvas, keeping PAD; returns the image, where the pivot is now, and the top cut."""
    box = img.split()[3].getbbox()
    x0, y0 = max(0, box[0] - PAD), max(0, box[1] - PAD)
    x1, y1 = min(img.size[0], box[2] + PAD), min(img.size[1], box[3] + PAD)
    return img.crop((x0, y0, x1, y1)), (pivot_px[0] - x0, pivot_px[1] - y0), y0


def along(p, deg, length):
    """Where a limb hanging from p and turned deg (clockwise) ends, length down it."""
    r = math.radians(deg)
    return (p[0] - math.sin(r) * length, p[1] + math.cos(r) * length)


def place(svg, at, deg=0.0):
    return '<g transform="translate(%.1f,%.1f) rotate(%.1f)">%s</g>' % (at[0], at[1], deg, svg)


# --- coloured pencil ---------------------------------------------------------------------------

# Coloured in coloured pencil inside a black marker outline, the way the drawing guide asks a kid
# to do it (Eric's call, 2026-10-08). Not in rows of strokes: the subtle unevenness of real
# coloured pencil (Eric, 2026-10-15) - pressed harder and softer in broad swells, coloured in a
# patch at a time with short strokes whose way wanders from patch to patch, a faint mottle where
# the pigment catches the paper's grain, and a few specks of bare paper where it skipped the
# tooth. The marker stays solid.

def seed_of(name):
    return zlib.crc32(name.encode('utf-8')) & 0xffffffff


def soft_noise(rnd, size, cell, lo, hi):
    """Smooth random values from lo to hi, changing over about <cell> pixels: a coarse grid, blown up smoothly."""
    w, h = size
    gw, gh = w // cell + 3, h // cell + 3
    grid = Image.new('L', (gw, gh))
    grid.putdata([rnd.randint(lo, hi) for _ in range(gw * gh)])
    big = grid.resize((gw * cell, gh * cell), Image.BICUBIC)
    ox, oy = rnd.randint(0, cell), rnd.randint(0, cell)
    return big.crop((ox, oy, ox + w, oy + h))


def pencil_coverage(size, seed):
    """
    How much colour the pencil laid down at each pixel, 0-255. Pressed harder and softer in broad
    swells; coloured a patch at a time, the short strokes in each going one way and the way
    changing from patch to patch, so nothing lines up into rows; a faint mottle of pigment on the
    paper's grain; and a few specks where the pencil skipped the tooth. Every mark is a few pixels
    across and low in contrast, so at match size it is texture, not detail, and nothing finer than
    a pixel shimmers as he moves. Seeded, so a re-cut comes out the same.
    """
    w, h = size
    rnd = random.Random(seed)
    pressure = soft_noise(rnd, size, 64, 210, 255)

    marks = Image.new('L', (w, h), 255)
    draw = ImageDraw.Draw(marks)
    slant = rnd.uniform(25.0, 60.0)
    spacing = 62.0
    for r in range(int(h / spacing) + 2):
        for c in range(int(w / spacing) + 2):
            cx = (c - 0.5 + rnd.uniform(-0.4, 0.4)) * spacing
            cy = (r - 0.5 + rnd.uniform(-0.4, 0.4)) * spacing
            angle = slant + rnd.uniform(-40.0, 40.0)
            for _ in range(rnd.randint(22, 34)):
                a = math.radians(angle + rnd.uniform(-7.0, 7.0))
                along = rnd.uniform(-spacing * 0.6, spacing * 0.6)
                across = rnd.uniform(-spacing * 0.6, spacing * 0.6)
                x = cx + math.cos(a) * along - math.sin(a) * across
                y = cy + math.sin(a) * along + math.cos(a) * across
                half = rnd.uniform(10.0, 24.0)
                dx, dy = math.cos(a) * half, math.sin(a) * half
                draw.line([(x - dx, y - dy), (x + dx, y + dy)], fill=rnd.randint(206, 236), width=rnd.choice((2, 3, 3, 4)))
    marks = marks.filter(ImageFilter.GaussianBlur(0.9))

    grain = soft_noise(rnd, size, 6, 208, 255)
    tooth = soft_noise(rnd, size, 5, 0, 255).point(lambda v: 165 if v < 18 else 228 if v < 36 else 255)
    tooth = tooth.filter(ImageFilter.GaussianBlur(0.7))
    cover = ImageChops.multiply(ImageChops.multiply(pressure, grain), ImageChops.multiply(marks, tooth))
    # Never so light the colour washes out: a kid colouring in presses on.
    return cover.point(lambda v: 50 + v * 205 // 255)


def pencilled(colour, ink, seed):
    """
    The flat-coloured drawing with its colour put down in pencil. <ink> is the same drawing with
    only its marker showing (ink_only): wherever that is black the pixel keeps its colour, so the
    outline and every marker line stay solid; everywhere else the colour is laid over the paper.
    """
    rgb = colour.convert('RGB')
    coverage = pencil_coverage(colour.size, seed)
    paper = Image.new('RGB', colour.size, PAPER)
    textured = Image.composite(rgb, paper, coverage)
    marker = ink.convert('L').point(lambda v: max(0, min(255, (255 - v) * 255 // 220)))
    out = Image.composite(rgb, textured, marker)
    return Image.merge('RGBA', out.split() + (colour.split()[3],))


# --- cutting -----------------------------------------------------------------------------------

def face_parts(face, box, neck, scale, colours, fill, seed_prefix, parts_dir, parts):
    """
    Each feature of the face drawn as its own part, on the head's canvas, so the game can move it
    (FighterRig.SetFace) - the same drawing as before, only no longer stuck to the head. Returns
    the rig.json extras that hang them back on the head where they were drawn.
    """
    pieces = dict(('Face' + name, (svg, box, centre, False)) for name, (kind, svg, centre, tilt) in face.items())
    cut, _ = draw_parts(pieces, fill, colours, scale, seed_prefix=seed_prefix)
    extras = []
    for name, (kind, svg, centre, tilt) in sorted(face.items()):
        img, pivot_px, _ = cut['Face' + name]
        img.save(os.path.join(parts_dir, 'Face%s.png' % name))
        parts['Face' + name] = {'texture': 'parts/Face%s.png' % name, 'pivot': [round(pivot_px[0], 1), round(pivot_px[1], 1)]}
        extras.append({'name': 'face' + name, 'bone': 'Head', 'part': 'Face' + name,
                       'offset': [round((centre[0] - neck[0]) * scale, 1), round((centre[1] - neck[1]) * scale, 1)],
                       'always': True, 'onPart': True, 'face': kind, 'tilt': tilt})
    return extras


def draw_parts(pieces, fill, colours, scale, extra=None, seed_prefix='', trim=True):
    """
    Renders and pencils every piece. pieces: {name: (svg, viewBox, pivot, mirrored)}, the pivot in
    drawing units. extra: {name: (svg-with-colours function, viewBox, background)} for whole
    drawings, like the figure as a kid would have drawn it. Returns {name: (image, pivot in
    pixels, viewBox top in pixels)} for the pieces and {name: image} for the extras. The pencil
    strokes are seeded from seed_prefix and the name, so two fighters' parts differ. Untrimmed,
    each piece keeps its whole viewBox as its canvas - for a held weapon, whose size the game
    measures it by.
    """
    blank = ink_only(colours)
    docs = {}
    for name, (svg, box, _, flip) in pieces.items():
        for suffix, palette in (('', colours), ('@ink', blank)):
            body = fill(svg, palette)
            docs[name + suffix] = svg_doc(mirrored(body) if flip else body, box, scale)
    for name, (draw, box, background) in (extra or {}).items():
        docs[name] = svg_doc(draw(colours), box, scale, background=background)
        docs[name + '@ink'] = svg_doc(draw(blank), box, scale, background='#ffffff')
    images = rasterise(docs)
    for name in [n for n in docs if not n.endswith('@ink')]:
        images[name] = pencilled(images[name], images[name + '@ink'], seed_of(seed_prefix + name))

    cut = {}
    for name, (svg, box, pivot, _) in pieces.items():
        pivot_px = ((pivot[0] - box[0]) * scale, (pivot[1] - box[1]) * scale)
        cut[name] = trimmed(images[name], pivot_px) if trim else (images[name], pivot_px, 0)
    return cut, dict((name, images[name]) for name in (extra or {}))
