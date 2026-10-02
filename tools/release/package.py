# -*- coding: utf-8 -*-
"""Turn an export in build/windows/ into the three files a GitHub Release carries.

    python tools/release/package.py 0.2.0

Writes build/release/:

  DrawFight-windows.zip   everything, in a DrawFight/ folder - what a new player downloads
  DrawFight-patch.zip     just the game: the .pck and the game's own assembly. About 5 MB,
                          against about 70 MB for the full zip, because the engine and the .NET
                          runtime only change when Godot is upgraded.
  version.json            what the game's update check reads (see scripts/UpdateScreen.cs)

Normally run by tools/release/release.sh rather than by hand. See .ai/releasing.md.
"""
from __future__ import print_function

import io
import json
import os
import sys
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EXPORT = os.path.join(ROOT, "build", "windows")
OUT = os.path.join(ROOT, "build", "release")
REPO = "eclancy/DrawFight"
DATA = "data_DrawFight_windows_x86_64"

# The engine the export was made with. A patch is only offered to a copy on the same engine;
# anything else downloads the full zip. Read from the editor binary's name rather than
# hard-coded, so upgrading Godot cannot leave this behind.
GODOT = os.environ.get("GODOT_BIN", "")

# What the patch carries: everything that changes when the game changes and the engine does
# not. The .pck is every scene, image, sound and setting; the DrawFight.* files are the C#.
PATCH = ["DrawFight.pck", DATA + "/DrawFight.dll", DATA + "/DrawFight.deps.json",
         DATA + "/DrawFight.runtimeconfig.json"]

HOW_TO_PLAY = u"""DrawFight
=========

1. Double-click DrawFight.exe.

2. The first time, Windows may say "Windows protected your PC". That is because this game
   is not from a big company that paid for a signature. Click "More info", then "Run anyway".

3. Plug in a controller or two (Xbox pads work best), or use the keyboard.

The game updates itself: every time it starts, it checks for a new version and installs it.
Keep this folder somewhere you can write to, like Documents or the Desktop, so it can.

Made at https://ecec.dev - draw your own fighter at https://ecec.dev/drawfight
"""


def engine_version():
    name = os.path.basename(GODOT)
    # Godot_v4.5.1-stable_mono_win64_console.exe -> 4.5.1
    if name.startswith("Godot_v"):
        return name[len("Godot_v"):].split("-")[0]
    raise SystemExit("set GODOT_BIN to the Godot editor, so the engine version is known")


def add_tree(z, src, prefix):
    for dirpath, _dirs, files in os.walk(src):
        for f in sorted(files):
            full = os.path.join(dirpath, f)
            rel = os.path.relpath(full, src).replace(os.sep, "/")
            z.write(full, prefix + rel)


def main(argv):
    if not argv:
        print(__doc__)
        return 1
    version = argv[0]
    if not os.path.isfile(os.path.join(EXPORT, "DrawFight.pck")):
        raise SystemExit("no export in build/windows - run the Godot export first")
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    full = os.path.join(OUT, "DrawFight-windows.zip")
    z = zipfile.ZipFile(full, "w", zipfile.ZIP_DEFLATED, allowZip64=True)
    add_tree(z, EXPORT, "DrawFight/")
    z.writestr("DrawFight/How to play.txt", HOW_TO_PLAY.replace(u"\n", u"\r\n").encode("utf-8"))
    z.close()

    patch = os.path.join(OUT, "DrawFight-patch.zip")
    z = zipfile.ZipFile(patch, "w", zipfile.ZIP_DEFLATED)
    for rel in PATCH:
        z.write(os.path.join(EXPORT, rel.replace("/", os.sep)), rel)
    z.close()

    base = "https://github.com/%s/releases/download/v%s/" % (REPO, version)
    info = {
        "version": version,
        "engine": engine_version(),
        "patch": base + "DrawFight-patch.zip",
        "full": base + "DrawFight-windows.zip",
    }
    with io.open(os.path.join(OUT, "version.json"), "w", encoding="utf-8") as f:
        f.write(unicode(json.dumps(info, indent=2, sort_keys=True)) + u"\n")

    for name in ("DrawFight-windows.zip", "DrawFight-patch.zip", "version.json"):
        print("%-24s %6.1f MB" % (name, os.path.getsize(os.path.join(OUT, name)) / 1048576.0))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
