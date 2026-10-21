# -*- coding: utf-8 -*-
"""Where each fighter's eyes and mouth are in their drawing - read off the cut parts, in the
pixels of the part they are on. faces.apply() lifts them out (see faces.py); each cut script
applies its own fighter's spec as its last step.

When a fighter is re-cut and their part moves or changes size, the boxes here move with it: check
them with `--parade --film=face --only=N`, which shows every expression side by side.
"""

# Flambé and Lugnut are not here: we drew them, in SVG, so their faces are drawn as separate
# parts to begin with (FACE in flambe.py and lugnut.py) rather than lifted out of a picture.
FACES = {
    # Circy (Elim's): two long eye strokes and a big smile on the yellow ball.
    'circy': ('Torso', 'Torso', [
        dict(name='EyeLeft', kind='eye', box=(339, 157, 400, 480), rule='ink', fill='flat', tilt=-1),
        dict(name='EyeRight', kind='eye', box=(660, 185, 726, 480), rule='ink', fill='flat', tilt=1),
        dict(name='Mouth', kind='mouth', box=(248, 446, 718, 689), rule='ink', fill='flat', pivot='top', stretch=0.45),
    ]),
    # Triguy (Elim's) is not here: his face is lifted in tools/art/cut_triguy.py itself, before his
    # body is stood on end, because its marks have to stay level while the body under them turns -
    # and there, before the line is widened, both eyes stand clear of his outline (Eric's call,
    # 2026-10-19).
    # EdgeLord: two glowing almond eyes and a toothy grin, on hatched red. Eric's drawing,
    # our colouring. The hatching is borrowed from beside each one to fill behind it.
    'edgelord': ('Torso', 'Torso', [
        dict(name='EyeLeft', kind='eye', box=(212, 264, 282, 330), rule='glow', fill='texture', grow=5, tilt=1,
             seeds=[(235, 295), (225, 285), (255, 312)]),
        dict(name='EyeRight', kind='eye', box=(342, 276, 422, 334), rule='glow', fill='texture', grow=5, tilt=-1,
             seeds=[(380, 300), (400, 295)]),
        dict(name='Mouth', kind='mouth', box=(232, 344, 340, 452), rule='teeth', fill='texture', grow=5, pivot='top', stretch=0.25,
             seeds=[(265, 420), (250, 400), (240, 370)]),
    ]),
    # DoomBot (Eric's), turned toward us (cut_doombot.py, Eric's call 2026-10-21): on his new face,
    # his round grille eye with its dark brow nearer the corner, its mirrored copy as the far eye,
    # and his teeth mirrored into a mouth - taken with the dark lines round them. The red slash
    # with the green tip is an antenna on the side of his head, his ear, not a face: it stays put.
    # The cut prints these boxes after a re-cut. Pencilled grey metal borrowed to fill behind.
    'doombot': ('Head', 'Head', [
        dict(name='EyeNear', kind='eye', box=(225, 210, 323, 295), rule='colour', fill='texture', grow=5, tilt=1,
             seeds=[(272, 249)]),
        dict(name='BrowNear', kind='brow', box=(263, 165, 329, 215), rule='colour', fill='texture', grow=5, tilt=1,
             seeds=[(295, 191)]),
        dict(name='EyeFar', kind='eye', box=(349, 214, 435, 289), rule='colour', fill='texture', grow=5, tilt=-1,
             seeds=[(394, 249)]),
        dict(name='BrowFar', kind='brow', box=(344, 175, 402, 219), rule='colour', fill='texture', grow=5, tilt=-1,
             seeds=[(374, 198)]),
        dict(name='Mouth', kind='mouth', box=(220, 312, 446, 401), rule='colour', fill='texture', grow=5, pivot='top', stretch=0.12,
             seeds=[(246, 357), (300, 357), (366, 357), (420, 357)]),
    ]),
}
