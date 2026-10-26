# -*- coding: utf-8 -*-
"""Turn the exports in build/windows/ and build/linux/ into the files a GitHub Release carries.

    python tools/release/package.py 0.2.0

Writes build/release/:

  DrawFight-windows.zip   everything, in a DrawFight/ folder - the portable version, for anyone
                          who does not want DrawFight-Setup.exe (which release.sh builds next,
                          with Inno Setup, from tools/release/installer.iss)
  DrawFight-patch.zip     just the game: the .pck and the game's own assembly. About 5 MB,
                          against about 70 MB for the full zip, because the engine and the .NET
                          runtime only change when Godot is upgraded.
  DrawFight-linux.tar.gz  the Linux version, in a DrawFight/ folder with install.sh (which puts
                          it in ~/.local/share and on the app menu) - from build/linux/
  DrawFight-linux-patch.zip  the Linux patch: its .pck and the game's assembly
  version.json            what the game's update check reads (see scripts/UpdateScreen.cs)

Normally run by tools/release/release.sh rather than by hand. See .ai/releasing.md.
"""
from __future__ import print_function

import io
import json
import os
import sys
import tarfile
import time
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EXPORT = os.path.join(ROOT, "build", "windows")
LINUX = os.path.join(ROOT, "build", "linux")
LINUX_DATA = "data_DrawFight_linuxbsd_x86_64"
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


HOW_TO_PLAY_LINUX = u"""DrawFight
=========

1. Open a terminal in this folder and run:  ./install.sh
   That puts the game in ~/.local/share/DrawFight and adds it to your app menu.
   (Or just run ./DrawFight.x86_64 from here - keep the folder somewhere you can write to.)

2. Plug in a controller or two (Xbox pads work best), or use the keyboard.

The game updates itself: every time it starts, it checks for a new version and installs it.
To remove it: ~/.local/share/DrawFight/install.sh --uninstall  (your saves are kept)

Made at https://ecec.dev - draw your own fighter at https://ecec.dev/drawfight
"""


def add_tar_file(t, data, name, executable=False):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mode = 0o755 if executable else 0o644
    info.mtime = int(time.time())
    t.addfile(info, io.BytesIO(data))


def linux_package(version):
    """The Linux download and patch. Every file gets its permissions set here, on Windows: the
    game binary and install.sh executable, the rest readable."""
    tar_path = os.path.join(OUT, "DrawFight-linux.tar.gz")
    t = tarfile.open(tar_path, "w:gz")
    for dirpath, _dirs, files in os.walk(LINUX):
        for f in sorted(files):
            full = os.path.join(dirpath, f)
            rel = os.path.relpath(full, LINUX).replace(os.sep, "/")
            with open(full, "rb") as fh:
                data = fh.read()
            add_tar_file(t, data, "DrawFight/" + rel, executable=rel == "DrawFight.x86_64")
    with open(os.path.join(ROOT, "tools", "release", "install.sh"), "rb") as fh:
        add_tar_file(t, fh.read().replace(b"\r\n", b"\n"), "DrawFight/install.sh", executable=True)
    with open(os.path.join(ROOT, "icon.png"), "rb") as fh:
        add_tar_file(t, fh.read(), "DrawFight/icon.png")
    add_tar_file(t, HOW_TO_PLAY_LINUX.encode("utf-8"), "DrawFight/How to play.txt")
    t.close()

    patch = os.path.join(OUT, "DrawFight-linux-patch.zip")
    z = zipfile.ZipFile(patch, "w", zipfile.ZIP_DEFLATED)
    for rel in ["DrawFight.pck", LINUX_DATA + "/DrawFight.dll", LINUX_DATA + "/DrawFight.deps.json",
                LINUX_DATA + "/DrawFight.runtimeconfig.json"]:
        z.write(os.path.join(LINUX, rel.replace("/", os.sep)), rel)
    z.close()


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

    linux = os.path.isfile(os.path.join(LINUX, "DrawFight.pck"))
    if linux:
        linux_package(version)

    base = "https://github.com/%s/releases/download/v%s/" % (REPO, version)
    info = {
        "version": version,
        "engine": engine_version(),
        "patch": base + "DrawFight-patch.zip",
        "full": base + "DrawFight-windows.zip",
        # The updater ignores this; it is here so anything reading version.json can offer the
        # installer. release.sh builds it right after this script.
        "installer": base + "DrawFight-Setup.exe",
    }
    # A Linux copy reads its own pair; Windows copies never look at it.
    if linux:
        info["linux"] = {"patch": base + "DrawFight-linux-patch.zip", "full": base + "DrawFight-linux.tar.gz"}
    with io.open(os.path.join(OUT, "version.json"), "w", encoding="utf-8") as f:
        f.write(unicode(json.dumps(info, indent=2, sort_keys=True)) + u"\n")

    names = ["DrawFight-windows.zip", "DrawFight-patch.zip", "version.json"]
    if linux:
        names += ["DrawFight-linux.tar.gz", "DrawFight-linux-patch.zip"]
    for name in names:
        print("%-26s %6.1f MB" % (name, os.path.getsize(os.path.join(OUT, name)) / 1048576.0))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
