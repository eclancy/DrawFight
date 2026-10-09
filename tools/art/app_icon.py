# -*- coding: utf-8 -*-
"""The application icon: what the Godot project manager and the taskbar show.

Run:
    "$GODOT_BIN" --path . -- --shot=115      -> .shots/latest.png, the title screen
    python tools/art/app_icon.py              -> icon.png at the repo root

IT IS A CROP OF A REAL FRAME OF THE GAME, not a drawing made for the icon. An icon drawn
separately is a second answer to "what does this game look like", and it drifts the moment the
game changes. This one is Lug's face on the ruled paper of the title screen, exactly as the game
renders it.

The title brawl loops, so the capture frame matters: at 115 Lug is standing idle between hits
rather than recoiling. Look at the result before committing it - if a pose change moves him,
adjust FACE below. When a real drawing replaces the stick figures, re-shoot and re-crop; the
icon should be his fighter's face, not a test figure's.

Only Python 2.7 with PIL is installed on this machine, which is all this needs.
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

SHOT = os.path.join(ROOT, ".shots", "latest.png")
OUT_PNG = os.path.join(ROOT, "icon.png")

# Lug's head on the 1920x1080 title screen: centre x, centre y, square side. Wide enough to keep
# the whole hard hat and the nose with a margin of paper, tight enough that the face still reads
# when the project manager shows it at 64px.
FACE = (1562, 404, 184)

SIZE = 256


def main():
    if not os.path.exists(SHOT):
        raise SystemExit("no screenshot at %s - run the --shot command in this file's docstring"
                         % SHOT)

    shot = Image.open(SHOT).convert("RGB")
    if shot.size != (1920, 1080):
        raise SystemExit("expected a 1920x1080 title screenshot, got %dx%d" % shot.size)

    cx, cy, side = FACE
    half = side // 2
    face = shot.crop((cx - half, cy - half, cx - half + side, cy - half + side))

    # A photographic resample, not nearest: the frame is anti-aliased marker, not pixel art.
    face.resize((SIZE, SIZE), Image.LANCZOS).save(OUT_PNG)
    print("wrote %s  %dx%d" % (os.path.relpath(OUT_PNG, ROOT), SIZE, SIZE))


if __name__ == "__main__":
    sys.exit(main())
