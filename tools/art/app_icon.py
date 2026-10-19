# -*- coding: utf-8 -*-
"""The application icon: what the Godot project manager, the taskbar and the installer show.

Run:
    "$GODOT_BIN" --path . -- --icon --shot=40   -> .shots/latest.png, the icon frame
    python tools/art/app_icon.py                -> icon.png and icon.ico at the repo root

IT IS A CROP OF A REAL FRAME OF THE GAME, not a drawing made for the icon. An icon drawn
separately is a second answer to "what does this game look like", and it drifts the moment the
game changes. This one is Circy - Elim's drawing, the first fighter a kid drew - standing idle,
exactly as the game renders him, on the game's lined notepad page with its red margin (Eric's
call, 2026-10-19; before that he stood on plain paper in his parade cell, and before 2026-10-10
the icon was Lug's face). The frame is IconFrame (--icon): its top-left 512x512 square, which
it lays out for this. The page's lines there are drawn bolder than the menus', so they still
read at 32 pixels.

icon.ico is the same picture at the sizes Windows asks for; the installer uses it
(tools/release/installer.iss). Look at the result before committing it.

Only Python 2.7 with PIL is installed on this machine, which is all this needs.
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

SHOT = os.path.join(ROOT, ".shots", "latest.png")
OUT_PNG = os.path.join(ROOT, "icon.png")
OUT_ICO = os.path.join(ROOT, "icon.ico")

# IconFrame.Square, on the 1920x1080 screenshot.
SQUARE = (0, 0, 512, 512)

SIZE = 256
ICO_SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]


def main():
    if not os.path.exists(SHOT):
        raise SystemExit("no screenshot at %s - run the --shot command in this file's docstring"
                         % SHOT)

    shot = Image.open(SHOT).convert("RGB")
    if shot.size != (1920, 1080):
        raise SystemExit("expected a 1920x1080 screenshot of --icon, got %dx%d" % shot.size)

    # A photographic resample, not nearest: the frame is anti-aliased marker, not pixel art.
    icon = shot.crop(SQUARE).resize((SIZE, SIZE), Image.LANCZOS)
    icon.save(OUT_PNG)
    icon.save(OUT_ICO, sizes=ICO_SIZES)
    print("wrote %s and %s" % (os.path.relpath(OUT_PNG, ROOT), os.path.relpath(OUT_ICO, ROOT)))


if __name__ == "__main__":
    sys.exit(main())
