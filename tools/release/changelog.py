# -*- coding: utf-8 -*-
"""Adds a version to assets/changelog.txt, the changes shown in the game (Options > What's new).

    python tools/release/changelog.py 0.7.3 "What changed, for players"

Called by release.sh with its notes. Each line of the notes is one change; a leading "- " and
any **bold** markers are dropped, since the game shows plain lines.
"""
from __future__ import print_function

import datetime
import io
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PATH = os.path.join(HERE, '..', '..', 'assets', 'changelog.txt')


def main():
    version, notes = sys.argv[1], sys.argv[2]
    lines = []
    for line in notes.decode('utf-8').splitlines() if isinstance(notes, bytes) else notes.splitlines():
        line = re.sub(r'^\s*[-*]\s+', '', line).replace('**', '').strip()
        if line:
            lines.append(line)
    if not lines:
        return
    with io.open(PATH, encoding='utf-8') as f:
        text = f.read()
    entry = u'== %s | %s\n%s\n\n' % (version, datetime.date.today().isoformat(), u'\n'.join(lines))
    at = text.find(u'== ')
    text = text[:at] + entry + text[at:] if at >= 0 else text + u'\n' + entry
    with io.open(PATH, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print('changelog: added %s' % version)


if __name__ == '__main__':
    main()
