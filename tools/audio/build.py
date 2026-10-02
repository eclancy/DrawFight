# -*- coding: utf-8 -*-
"""Generate every sound effect into assets/sfx/, and the music into assets/music/.

Run from the repo root:

    python tools/audio/build.py                 # every sound effect (about 20 s)
    python tools/audio/build.py hit_ swing_     # only names containing these
    python tools/audio/build.py --music         # the two music loops too (a few minutes)
    python tools/audio/build.py --preview       # also write per-category audition reels

The generators are the source, the .wav files are output. Never hand-edit a file in
assets/ - edit the builder in sfx.py or music.py and re-run, exactly as with tools/art/.

Every sound is normalised to the peak target in the registry rather than to full scale.
Those targets ARE the mix: the game plays these at unity, so balance is changed by editing
one number in sfx.py and re-running, not by adding VolumeDb at call sites.

Adapted from WizardSurvivors' tools/audio/build.py.
"""
from __future__ import division

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import sfx
import synth as S

OUT = os.path.join(ROOT, "assets", "sfx")

# A one-shot longer than this is almost always an envelope mistake rather than a choice.
LONG_WARNING = 2.6
# How far under its registry target a finished file may land before it is a bug.
UNDER_TARGET_WARNING = 1.0


def db_to_linear(d):
    return 10.0 ** (d / 20.0)


def build_one(entry):
    name, category, target_db, fn, loops = entry
    buf = S.dc_block(fn())
    if not loops:
        # Fade BEFORE normalising, and keep the fade-in under half a millisecond: a fade
        # applied afterwards attenuates the very transient the target was measured against.
        # A loop is not faded at all - its ends are the seam.
        buf = S.fade(buf, 0.0004, 0.006)
    buf = S.normalize(buf, db_to_linear(target_db))
    path = os.path.join(OUT, name + ".wav")
    writer = S.write_wav_loop if loops else S.write_wav
    pk, rm, secs, clipped = writer(path, buf)
    return dict(name=name, category=category, path=path, peak=pk, rms=rm, target=target_db,
                secs=secs, clipped=clipped, loops=loops, buf=buf)


def seam_error(buf, window=64):
    """How badly a loop clicks at the wrap point, as a level difference in dB."""
    if len(buf) < window * 2:
        return 0.0
    return abs(S.db(S.rms(buf[:window])) - S.db(S.rms(buf[-window:])))


def report(results):
    print("")
    print("%-16s %-8s %8s %8s %7s  %s" % ("name", "category", "peak", "rms", "len", "notes"))
    print("-" * 72)
    warnings = 0
    for r in results:
        notes = []
        if r["clipped"]:
            notes.append("CLIPPED x%d" % r["clipped"])
        if r["secs"] > LONG_WARNING:
            notes.append("long")
        if r["peak"] < r["target"] - UNDER_TARGET_WARNING:
            notes.append("UNDER TARGET by %.1f dB" % (r["target"] - r["peak"]))
        if r["loops"]:
            e = seam_error(r["buf"])
            notes.append("loop seam %.1f dB" % e)
            if e > 6.0:
                notes.append("SEAM AUDIBLE")
        if [n for n in notes if n.isupper() or "AUDIBLE" in n]:
            warnings += 1
        print("%-16s %-8s %7.1fd %7.1fd %6.2fs  %s"
              % (r["name"], r["category"], r["peak"], r["rms"], r["secs"], ", ".join(notes)))
    total = sum([os.path.getsize(r["path"]) for r in results])
    print("-" * 72)
    print("%d files, %.1f s of audio, %.2f MB in %s"
          % (len(results), sum([r["secs"] for r in results]), total / 1048576.0,
             os.path.relpath(OUT, ROOT)))
    if warnings:
        print("%d file(s) need attention - see notes above" % warnings)
    return warnings


def previews(results):
    """One audition reel per category, into tools/audio/. Working files: nothing loads them,
    tools/audio/ carries a .gdignore, and .gitignore keeps them out of the repo."""
    cats = {}
    for r in results:
        cats.setdefault(r["category"], []).append(r)
    gap = S.silence(0.3)
    for cat, items in sorted(cats.items()):
        reel = []
        for r in items:
            reel = S.concat(reel, r["buf"], gap)
        path = os.path.join(HERE, "_preview_%s.wav" % cat)
        S.write_wav(path, reel)
        print("preview: %s (%d sounds, %.1f s)" % (os.path.relpath(path, ROOT), len(items), len(reel) / S.SR))


def main(argv):
    filters = [a for a in argv if not a.startswith("--")]

    entries = sfx.REGISTRY
    if filters:
        entries = [e for e in entries if any([f in e[0] for f in filters])]
    warnings = 0
    if entries:
        results = []
        for e in entries:
            results.append(build_one(e))
            sys.stdout.write(".")
            sys.stdout.flush()
        warnings = report(results)
        if "--preview" in argv:
            previews(results)
    elif not "--music" in argv:
        print("nothing matched %r" % filters)
        return 1

    if "--music" in argv:
        import music
        music.build_all(os.path.join(ROOT, "assets", "music"))
    return 1 if warnings else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
