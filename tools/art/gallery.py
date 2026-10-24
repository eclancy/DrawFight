# -*- coding: utf-8 -*-
"""
Copies the drawings sent in for the game into assets/gallery/, for the gallery on the options
screen (GalleryScreen). Run with the only Python on this machine:

    python tools/art/gallery.py

The drawings' master copies are under fighters/<name>/source/, and those folders are left out of
the downloadable game (export_presets.cfg) - so the gallery has its own copies, made here. Each is
the drawing exactly as drawn, only made smaller (no longer than MAX_SIDE pixels): never cleaned
up, cropped or redrawn. A drawing with see-through paper stays a PNG; a photo becomes a JPEG.

Writes assets/gallery/gallery.txt, one picture a line - "file|fighter" - in the order the gallery
shows them, the fighter by story id so the game credits whoever drew them. It ends on
for_my_nephew.webp, Eric's picture for his nephew, which is kept here as it is (not generated).
Then run "$GODOT_BIN" --headless --path . --import.
"""
import io
import os
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'assets', 'gallery')
MAX_SIDE = 1600

# Whose drawings, in the order they came: Elim's Circy and Triguy, then Eric's EdgeLord and DoomBot.
# The two stand-ins we drew ourselves (Flambe and Lugnut) were not sent in, so they are not here.
DRAWINGS = [
    ('circy', [
        'circytcircle.png', 'circytcirclesmall.png', 'circytcircletall.png', 'circytcircleshoulderbash.png',
        'grapplinghook.png', 'lasorbeam.png', 'circytcirclebombframe1.png', 'circytcirclebombframe3.png',
        'circytcirclebombframe4BOOM.png', 'bombpart2shrinking.png',
    ]),
    ('triguy', [
        'triguy.png', 'idle.png', 'taunt_pose.png', 'taunt_pog.png', 'taunt_surprised.png', 'crying.png',
        'spike.png', 'trampoline.png', 'outline.png',
    ]),
    ('edgelord', ['edgelord.webp']),
    ('doombot', ['doombot.jpg']),
]

LAST = 'for_my_nephew.webp'


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    lines = []
    for fighter, files in DRAWINGS:
        for name in files:
            src = os.path.join(ROOT, 'fighters', fighter, 'source', name)
            img = Image.open(src)
            img.load()
            scale = min(1.0, float(MAX_SIDE) / max(img.size))
            if scale < 1.0:
                img = img.resize((int(img.size[0] * scale), int(img.size[1] * scale)), Image.LANCZOS)
            stem = '%s_%s' % (fighter, os.path.splitext(name)[0].lower())
            if img.mode in ('RGBA', 'LA', 'P'):
                out = stem + '.png'
                img.convert('RGBA').save(os.path.join(OUT, out), optimize=True)
            else:
                out = stem + '.jpg'
                img.convert('RGB').save(os.path.join(OUT, out), quality=90)
            lines.append('%s|%s' % (out, fighter))
            print('%-40s %dx%d' % (out, img.size[0], img.size[1]))
    if not os.path.exists(os.path.join(OUT, LAST)):
        raise SystemExit('missing %s - it is not generated; put it back in assets/gallery/' % LAST)
    lines.append('%s|' % LAST)
    with io.open(os.path.join(OUT, 'gallery.txt'), 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(u'# Written by tools/art/gallery.py: one picture a line, "file|fighter" (story id; none for the last).\n')
        for line in lines:
            fh.write(line.decode('utf-8') if isinstance(line, bytes) else unicode(line))
            fh.write(u'\n')
    print('%d pictures' % len(lines))


if __name__ == '__main__':
    main()
