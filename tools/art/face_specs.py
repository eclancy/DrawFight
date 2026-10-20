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
    # DoomBot (Eric's): a red slash of a brow over a green eye, a round red eye, and the yellow
    # teeth - taken with the dark lines round them. Pencilled grey metal borrowed to fill behind.
    'doombot': ('Head', 'Head', [
        dict(name='BrowFront', kind='brow', box=(80, 172, 158, 286), rule='colour', fill='texture', grow=5, tilt=-1,
             seeds=[(110, 230)]),
        dict(name='EyeFront', kind='eye', box=(132, 280, 182, 318), rule='colour', fill='texture', grow=5, tilt=-1,
             seeds=[(155, 298)]),
        dict(name='EyeBack', kind='eye', box=(282, 208, 372, 292), rule='colour', fill='texture', grow=5, tilt=1,
             seeds=[(325, 250)]),
        dict(name='BrowBack', kind='brow', box=(318, 172, 368, 214), rule='colour', fill='texture', grow=5, tilt=1,
             seeds=[(348, 196)]),
        dict(name='Mouth', kind='mouth', box=(226, 322, 424, 398), rule='colour', fill='texture', grow=5, pivot='top', stretch=0.12,
             seeds=[(300, 360), (245, 360), (385, 365), (410, 365)]),
    ]),
}
