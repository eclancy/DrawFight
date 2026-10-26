#!/usr/bin/env bash
# Publish a new version of DrawFight. Every copy of the game picks it up the next time it starts.
#
#   bash tools/release/release.sh 0.2.0 ["What changed, for players"]
#
# The optional second argument heads the release notes, and ecec.dev/games/drawfight shows it
# under "What's new". The download instructions are always appended below it.
#
# Commits the version bump, tags it, exports the Windows and Linux builds, and makes a GitHub
# Release with the installer (DrawFight-Setup.exe, built by Inno Setup from
# tools/release/installer.iss), the full zip (the portable version), the small patch zip, the
# Linux download (DrawFight-linux.tar.gz, with an install.sh) and its patch, and version.json. ecec.dev/drawfight/version.json redirects
# to the newest release's version.json, so nothing on ecec.dev needs deploying. See
# .ai/releasing.md.
set -euo pipefail

VERSION="${1:-}"
CHANGES="${2:-}"
HOW_TO="Download **DrawFight-Setup.exe** and run it, then play from the Start menu or the desktop. It keeps itself up to date from then on. No installer wanted? **DrawFight-windows.zip** is the portable version: unzip it somewhere you can write to, like Documents, and run DrawFight.exe. On Linux, download **DrawFight-linux.tar.gz**, unpack it and run **./install.sh** - it adds DrawFight to your app menu and keeps itself up to date too. Download page: https://ecec.dev/drawfight/download"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
	echo "usage: bash tools/release/release.sh X.Y.Z [\"what changed\"]" >&2
	exit 1
fi

cd "$(dirname "$0")/../.."
: "${GODOT_BIN:?GODOT_BIN is not set - see .claude/settings.json}"
# Inno Setup compiles the installer. Its per-user install puts ISCC here; set ISCC otherwise.
ISCC="${ISCC:-$LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe}"
if [ ! -f "$ISCC" ]; then
	echo "Inno Setup not found at $ISCC - install it (winget install JRSoftware.InnoSetup) or set ISCC" >&2
	exit 1
fi

if [ -n "$(git status --porcelain)" ]; then
	echo "commit or stash your changes first - a release is built from what is committed" >&2
	exit 1
fi
if git rev-parse -q --verify "refs/tags/v$VERSION" >/dev/null; then
	echo "v$VERSION already exists" >&2
	exit 1
fi

CURRENT=$(sed -n 's/^config\/version="\(.*\)"$/\1/p' project.godot)
echo "DrawFight $CURRENT -> $VERSION"

# The version lives in project.godot, which is where the running game reads it from.
sed -i "s/^config\/version=\".*\"$/config\/version=\"$VERSION\"/" project.godot
# The notes also go in the game's own list of changes (Options > What's new).
if [ -n "$CHANGES" ]; then python tools/release/changelog.py "$VERSION" "$CHANGES"; fi

dotnet build DrawFight.sln -nologo -v q
rm -rf build/windows build/linux build/release
mkdir -p build/windows build/linux
# Godot does not create the export folder itself, and on an export error it can sit there
# instead of exiting - hence the timeout.
timeout 900 "$GODOT_BIN" --headless --path . --export-release "Windows Desktop" build/windows/DrawFight.exe \
	> build/export.log 2>&1 || { tail -20 build/export.log; echo "export failed - see build/export.log" >&2; git checkout project.godot assets/changelog.txt; exit 1; }
[ -f build/windows/DrawFight.pck ] || { echo "export produced no .pck - see build/export.log" >&2; git checkout project.godot assets/changelog.txt; exit 1; }
# The same game for Linux. Needs the linux_* export templates (see .ai/releasing.md).
timeout 900 "$GODOT_BIN" --headless --path . --export-release "Linux" build/linux/DrawFight.x86_64 \
	> build/export-linux.log 2>&1 || { tail -20 build/export-linux.log; echo "Linux export failed - see build/export-linux.log" >&2; git checkout project.godot assets/changelog.txt; exit 1; }
[ -f build/linux/DrawFight.pck ] || { echo "Linux export produced no .pck - see build/export-linux.log" >&2; git checkout project.godot assets/changelog.txt; exit 1; }

python tools/release/package.py "$VERSION"
# Git Bash turns an argument starting with / into a path; ISCC's /Q and /D switches must not be.
MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' "$ISCC" /Q "/DAppVersion=$VERSION" tools/release/installer.iss \
	|| { echo "installer build failed" >&2; git checkout project.godot assets/changelog.txt; exit 1; }
[ -f build/release/DrawFight-Setup.exe ] || { echo "no installer in build/release" >&2; git checkout project.godot assets/changelog.txt; exit 1; }

git diff --quiet project.godot assets/changelog.txt || git commit -q -m "Release v$VERSION" project.godot assets/changelog.txt
git tag "v$VERSION"
git push -q origin HEAD "v$VERSION"

gh release create "v$VERSION" \
	build/release/DrawFight-Setup.exe build/release/DrawFight-windows.zip \
	build/release/DrawFight-patch.zip build/release/version.json \
	build/release/DrawFight-linux.tar.gz build/release/DrawFight-linux-patch.zip \
	--title "DrawFight $VERSION" \
	--notes "${CHANGES:+$CHANGES

}$HOW_TO"

echo "Released v$VERSION - every copy updates the next time it starts."
