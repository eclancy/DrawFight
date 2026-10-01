#!/usr/bin/env bash
# Copies new fighter submissions from the ecec.dev/draw form down to fighters/incoming/.
#
#     bash tools/intake/pull_submissions.sh
#
# Each submission arrives as a folder: photo-1.jpg, photo-2.png ... (the drawing, the moves
# page, anything else they drew), answers.json, and sheet.md (the character sheet, readable).
# Folders already pulled are skipped, so it is safe to run any time. Nothing is deleted from
# the server.
#
# fighters/incoming/ is gitignored: a submission is someone's drawing, and it only enters the
# repo (and so becomes public) when it is deliberately made into a fighter.
#
# Needs the ecec.dev deploy key at ~/.ssh/ecec_hub_deploy. The form and the server side live in
# the EricClancyEngineeringAndCrafts repo (public/draw, intake/, docs/DEPLOYMENT.md).
set -euo pipefail

HOST="root@ecec.dev"
KEY="$HOME/.ssh/ecec_hub_deploy"
REMOTE="/var/lib/drawfight-submissions"
HERE="$(cd "$(dirname "$0")/../.." && pwd)"
LOCAL="$HERE/fighters/incoming"
SSH_OPTS=(-i "$KEY" -o BatchMode=yes -o ConnectTimeout=15)

mkdir -p "$LOCAL"
new=0
for id in $(ssh "${SSH_OPTS[@]}" "$HOST" "ls -1 $REMOTE 2>/dev/null"); do
	if [ -d "$LOCAL/$id" ]; then continue; fi
	scp -q -r "${SSH_OPTS[@]}" "$HOST:$REMOTE/$id" "$LOCAL/"
	echo "new: fighters/incoming/$id"
	new=$((new + 1))
done
echo "$new new submission(s)."
