# Releasing

Decided 2026-10-01. How a downloadable copy of DrawFight gets to people, and how it keeps
itself up to date.

**In one line:** `bash tools/release/release.sh X.Y.Z` makes a GitHub Release. The next time any
copy of the game starts, it finds the release and updates itself.

## Where things live

| What | Where | Why |
|---|---|---|
| The download page | `ecec.dev/drawfight/download` (`public/drawfight/download/` in the EricClancyEngineeringAndCrafts repo) | the page people are sent |
| The game files | GitHub Releases on `eclancy/DrawFight` | GitHub serves release downloads free with no bandwidth limit, so bots downloading it over and over cost nothing. The droplet's 1 TB a month is left alone. |
| The update check | `ecec.dev/drawfight/version.json` | an nginx 302 to `github.com/eclancy/DrawFight/releases/latest/download/version.json` |

The update address is on ecec.dev, not GitHub, because it is the one address compiled into every
copy ever downloaded. Behind a redirect, the releases can move somewhere else later without
breaking old copies. The download button links to GitHub's `releases/latest/download/...`, which
always points at the newest release, so **neither the page nor ecec.dev needs a deploy for a
release**.

## What a release contains

`tools/release/package.py` makes three files from the export in `build/windows/`:

- **`DrawFight-windows.zip`** (about 70 MB): everything, in a `DrawFight/` folder, with a
  `How to play.txt`. This is the file a new player downloads.
- **`DrawFight-patch.zip`** (about 5 MB): `DrawFight.pck` (every image, sound, setting and scene)
  and `DrawFight.dll` with its two JSON files (all the C#). These are the only files that change
  when the game changes and Godot does not. The rest of the full zip is the Godot engine and the
  .NET runtime.
- **`version.json`**: the version, the Godot version it was built with, and the URLs of both
  zips.

## How a copy updates

`UpdateScreen` is the first screen of an exported Windows build. It never runs in the editor or
from `godot --path .`.

1. It fetches `version.json` (5-second timeout) and compares versions number by number, so
   0.10.0 beats 0.9.0. The game's own version is `application/config/version` in
   `project.godot`, which the release script sets.
2. If the release was built with the same Godot version as this copy, it downloads the patch;
   otherwise it downloads the full zip. It unpacks to `user://update/`.
3. Windows will not let a running program overwrite its own files. So the game writes
   `apply.cmd`, starts it, and quits. The script waits for the game to close, `robocopy`s the new
   files over the old ones, and starts the game again.

Anything that goes wrong, including no internet, GitHub being down, or a folder the game cannot
write to, just goes on to the title screen. **An update is never worth not being able to play.**
The current version shows in the bottom-left corner of the title screen.

Tested end to end on 2026-10-01: a 0.0.9 build updated itself to 0.1.0 from a local server, and
its `.pck` and DLL came out byte-identical to the 0.1.0 export. With the server down, it went
straight to the title.

## Making a release

```sh
bash tools/release/release.sh 0.2.0
```

This needs a clean working tree. The script sets the version, builds, exports, packages, commits
"Release v0.2.0", tags it, pushes, and runs `gh release create`. It takes about a minute.

Version numbers: bump the last number for fixes and balance changes, and the middle one for a new
fighter or stage.

**One-time setup on a new machine:** install Godot's export templates for the exact editor
version. They come from the `godotengine/godot-builds` GitHub release, file
`Godot_v<version>-stable_mono_export_templates.tpz`, about 1.2 GB. Only the `windows_*` files are
needed, unpacked into `%APPDATA%\Godot\export_templates\<version>.stable.mono\`. The export
fails with "No export template found" until they are there.

**Testing an update without publishing:** export an older version number to `build/test/`, serve
`build/release/` with any local web server, and run the old copy with `DRAWFIGHT_UPDATE_URL`
pointing at your `version.json`. Run a build with `--no-update` to skip the check entirely.

## Things to know

- **Windows SmartScreen** warns "Windows protected your PC" the first time, because the exe is
  not code-signed. `How to play.txt` and the download page tell people to click "More info", then
  "Run anyway". Signing costs money every year, and is not worth it for family and friends.
- **Upgrading Godot** changes the engine version, so every copy downloads the full zip once.
  Install the new export templates before releasing.
- **The game must be somewhere it can write to.** Unzipped into Program Files, it cannot update
  itself and silently stays on its version. `How to play.txt` says to keep it in Documents or on
  the Desktop.
- **Only Windows is exported.** A Mac build needs Apple notarization to open without a fight.
