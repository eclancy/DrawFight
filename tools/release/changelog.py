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
    if isinstance(notes, bytes):
        # Windows hands Python 2 its arguments in the console's code page, not UTF-8 - an
        # accented letter (Flambe's e) broke the v0.10.0 release until this fell back to it.
        try:
            notes = notes.decode('utf-8')
        except UnicodeDecodeError:
            notes = notes.decode('cp1252')
    for line in notes.splitlines():
        line = re.sub(r'^\s*[-*]\s+', '', line).replace('**', '').strip()
        if line:
            lines.append(line)
    if not lines:
        return
    with io.open(PATH, encoding='utf-8') as f:
        text = f.read()
    entry = u'== %s | %s\n%s\n\n' % (version, datetime.date.today().isoformat(), u'\n'.join(lines))
    # The first version heading at the start of a line - not the example in the comment above
    # them, which put v0.8.0's notes inside the comment.
    at = text.find(u'\n== ')
    text = text[:at + 1] + entry + text[at + 1:] if at >= 0 else text + u'\n' + entry
    with io.open(PATH, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print('changelog: added %s' % version)


if __name__ == '__main__':
    main()
