#!/bin/sh
# DrawFight for Linux: puts the game in your home folder and adds it to your app menu.
#
#   ./install.sh               install, or update an installed copy
#   ./install.sh --uninstall   take it out again (your saves are kept)
#
# It installs to ~/.local/share/DrawFight, which you own, so the game can update itself there -
# the same reason the Windows installer installs per user. No sudo, nothing outside your home.
# Saves live in ~/.local/share/godot/app_userdata/DrawFight and are never touched.
set -e

APP=DrawFight
HERE=$(cd "$(dirname "$0")" && pwd)
DEST="${XDG_DATA_HOME:-$HOME/.local/share}/$APP"
MENU="${XDG_DATA_HOME:-$HOME/.local/share}/applications/drawfight.desktop"

if [ "$1" = "--uninstall" ]; then
	rm -rf "$DEST"
	rm -f "$MENU"
	echo "$APP is uninstalled. Your saves are still in ~/.local/share/godot/app_userdata/$APP."
	exit 0
fi

if [ "$HERE" != "$DEST" ]; then
	mkdir -p "$DEST"
	cp -Rf "$HERE/." "$DEST/"
fi
chmod +x "$DEST/$APP.x86_64" "$DEST/install.sh"

mkdir -p "$(dirname "$MENU")"
cat > "$MENU" <<DESKTOP
[Desktop Entry]
Type=Application
Name=$APP
Comment=A fighting game where every fighter is a drawing
Exec="$DEST/$APP.x86_64"
Path=$DEST
Icon=$DEST/icon.png
Terminal=false
Categories=Game;
DESKTOP
chmod +x "$MENU"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$(dirname "$MENU")" >/dev/null 2>&1 || true

echo "$APP is installed. Open it from your app menu, or run $DEST/$APP.x86_64"
echo "It keeps itself up to date."
