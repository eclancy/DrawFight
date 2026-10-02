# -*- coding: utf-8 -*-
"""The two music loops - menus and battle - built on synth.py.

    python tools/audio/build.py --music

Writes assets/music/menu.wav and assets/music/battle.wav: mono, 22050 Hz, each marked as a
whole-file loop with a 'smpl' chunk (see synth.write_wav_loop). Mono at half rate is about
2.6 MB a minute, which matters in a repo with no LFS where a loop is re-rendered on every
tuning pass.

Both are in A major, the key of every stinger in sfx.py (count_go is an A major chord,
game_set climbs A-C#-E-A), so the countdown lands in the key the battle music then plays in.

The sequencer, the note cache and the wrapped tails come from WizardSurvivors'
tools/audio/music.py. render() runs past the end of the loop and folds the overhang back onto
the start, so a ringing note or a reverb tail crosses the loop seam instead of being cut at it.
"""
from __future__ import division

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import synth as S

_PITCH_CLASS = {
    "C": -9, "C#": -8, "Db": -8, "D": -7, "D#": -6, "Eb": -6, "E": -5,
    "F": -4, "F#": -3, "Gb": -3, "G": -2, "G#": -1, "Ab": -1,
    "A": 0, "A#": 1, "Bb": 1, "B": 2,
}


def note(name):
    """Scientific pitch notation to Hz. note("A4") is 440."""
    i = len(name)
    while i > 0 and (name[i - 1].isdigit() or name[i - 1] == "-"):
        i -= 1
    return 440.0 * (2.0 ** ((_PITCH_CLASS[name[:i]] + (int(name[i:]) - 4) * 12) / 12.0))


# ---------------------------------------------------------------------------
# instruments - each takes (frequency, seconds) and returns a buffer
# ---------------------------------------------------------------------------

def marimba(freq, dur):
    """Wooden and round - the menu's voice. The partial ratios are a real marimba bar's."""
    d = max(dur, 0.9)
    v = S.partials(freq, [1.0, 3.93, 9.24], d, amps=[1.0, 0.3, 0.08], decays=[5.5, 13.0, 24.0])
    return S.env_mul(v, S.ad(d, 0.002, 0.5))


def glock(freq, dur):
    """A bright little bell for the menu tune."""
    d = max(dur, 0.8)
    return S.partials(freq, [1.0, 2.76, 5.4], d, amps=[1.0, 0.25, 0.1], decays=[4.0, 9.0, 16.0])


def softbass(freq, dur):
    v = S.mix(S.osc("sine", freq, dur), S.gain(S.osc("tri", freq, dur), 0.35))
    return S.env_mul(S.lowpass(v, 700), S.adsr(dur, 0.01, 0.1, 0.7, min(0.15, dur * 0.4)))


def warmpad(freq, dur):
    a = S.osc("saw", freq, dur)
    b = S.osc("saw", freq * 1.005, dur, phase=0.4)
    v = S.lowpass(S.mix(a, S.gain(b, 0.8)), 900, 0.8)
    return S.env_mul(v, S.adsr(dur, 0.4, 0.3, 0.8, 0.8))


def bass(freq, dur):
    """The battle bass: square and sine, with a filter snap so it drives on a TV speaker."""
    v = S.mix(S.gain(S.osc("square", freq, dur), 0.55), S.gain(S.osc("sine", freq, dur), 0.7))
    v = S.lowpass(v, S.sweep(dur, 1800, 350, 2.0), 1.5)
    return S.drive(S.env_mul(v, S.ad(dur, 0.004, 1.6)), 2.2)


def power(freq, dur):
    """A power chord - root, fifth, octave - in driven saws. The closest a synth gets to a guitar."""
    v = S.mix(S.osc("saw", freq, dur), S.gain(S.osc("saw", freq * 1.4983, dur, phase=0.2), 0.8),
              S.gain(S.osc("saw", freq * 2.0, dur, phase=0.5), 0.5))
    v = S.drive(S.lowpass(v, 2200, 0.9), 3.0)
    return S.env_mul(v, S.adsr(dur, 0.004, 0.06, 0.7, min(0.08, dur * 0.3)))


def pluck(freq, dur):
    v = S.lowpass(S.osc("saw", freq, dur), S.sweep(dur, 3200, 700, 2.4), 2.0)
    return S.env_mul(v, S.ad(dur, 0.003, 3.0))


def lead(freq, dur):
    """The battle tune: a square with a little vibrato that arrives late, like a singer's."""
    vib = S.ramp(S.n_samples(dur), 0.0, 0.007, 2.0)
    f = S.fm("square", freq, 5.5, vib, dur)
    v = S.mix(S.gain(f, 0.7), S.gain(S.osc("saw", freq * 1.003, dur), 0.3))
    v = S.lowpass(v, 2600, 1.1)
    return S.env_mul(v, S.adsr(dur, 0.01, 0.12, 0.75, min(0.12, dur * 0.3)))


def kick(freq, dur):
    n = S.n_samples(dur)
    return S.drive(S.env_mul(S.osc("sine", S.sweep(dur, 140, 45, 0.3), dur), S.expdecay(n, 7.0)), 2.0)


def snare(freq, dur):
    n = S.n_samples(dur)
    noise = S.env_mul(S.bandpass(S.osc("noise", 0, dur, seed=211), 2200, 0.8), S.expdecay(n, 9.0))
    body = S.env_mul(S.osc("tri", S.sweep(dur, 240, 170), dur), S.expdecay(n, 14.0))
    return S.mix(noise, S.gain(body, 0.6))


def snap(freq, dur):
    """A finger snap - the menu's backbeat, much lighter than a snare."""
    n = S.n_samples(dur)
    return S.env_mul(S.bandpass(S.osc("noise", 0, dur, seed=212), 2800, 1.4), S.expdecay(n, 22.0))


def hat(freq, dur):
    n = S.n_samples(dur)
    return S.env_mul(S.highpass(S.osc("noise", 0, dur, seed=213), 7000), S.expdecay(n, 14.0))


def crash(freq, dur):
    n = S.n_samples(dur)
    return S.env_mul(S.highpass(S.osc("noise", 0, dur, seed=214), 4500), S.expdecay(n, 4.5))


# ---------------------------------------------------------------------------
# sequencer
# ---------------------------------------------------------------------------

_CACHE = {}


def _voice(instrument, freq, dur):
    """One note, rendered once. Velocity is applied by the caller so it never splits the key."""
    key = (instrument.__name__, round(freq, 3), round(dur, 4))
    if key not in _CACHE:
        _CACHE[key] = instrument(freq, dur)
    return _CACHE[key]


def render(events, bpm, total_beats, tail_beats=8):
    """events: (beat, instrument, note name or None, length in beats, velocity)."""
    beat_s = 60.0 / bpm
    loop_n = S.n_samples(total_beats * beat_s)
    full_n = S.n_samples((total_beats + tail_beats) * beat_s)
    buf = [0.0] * full_n
    for beat, instrument, name, dur_beats, vel in events:
        v = _voice(instrument, note(name) if name else 0.0, dur_beats * beat_s)
        off = S.n_samples(beat * beat_s)
        for i in xrange(min(len(v), full_n - off)):
            buf[off + i] += v[i] * vel
    out = buf[:loop_n]
    for i in xrange(loop_n, full_n):
        out[(i - loop_n) % loop_n] += buf[i]
    return out


def chord_slots(slots, slot_beats):
    """(start beat, root, chord tones) for each slot in order."""
    return [(k * slot_beats, root, tones) for k, (root, tones) in enumerate(slots)]


# ---------------------------------------------------------------------------
# menu: relaxed, 100 BPM, marimba and bells. I - vi - ii - V, twice.
# ---------------------------------------------------------------------------

MENU_BPM = 100.0
MENU_SLOTS = [
    ("A2", ["A3", "C#4", "E4"]), ("F#2", ["F#3", "A3", "C#4"]),
    ("B2", ["B3", "D4", "F#4"]), ("E2", ["E3", "G#3", "B3"]),
] * 2
MENU_BEATS = 8 * len(MENU_SLOTS)          # 64 beats, 38.4 s

MENU_TUNE = [
    (0, "E5", 1.0), (1, "C#5", 0.5), (1.5, "E5", 0.5), (2, "A5", 1.5), (3.5, "G#5", 0.5),
    (4, "F#5", 1.0), (5, "E5", 2.0),
    (8, "C#5", 1.0), (9, "A4", 0.5), (9.5, "C#5", 0.5), (10, "F#5", 1.5), (11.5, "E5", 0.5),
    (12, "C#5", 3.0),
    (16, "D5", 1.0), (17, "B4", 0.5), (17.5, "D5", 0.5), (18, "F#5", 1.5), (19.5, "E5", 0.5),
    (20, "D5", 1.0), (21, "B4", 2.0),
    (24, "E5", 1.0), (25, "G#5", 1.0), (26, "B5", 1.5), (27.5, "A5", 0.5), (28, "G#5", 1.0),
    (29, "E5", 2.5),
    (32, "E5", 1.0), (33, "C#5", 0.5), (33.5, "E5", 0.5), (34, "A5", 1.5), (35.5, "B5", 0.5),
    (36, "C#6", 1.0), (37, "A5", 2.0),
    (40, "C#5", 1.0), (41, "A4", 0.5), (41.5, "C#5", 0.5), (42, "F#5", 1.5), (43.5, "G#5", 0.5),
    (44, "A5", 3.0),
    (48, "D5", 1.0), (49, "F#5", 0.5), (49.5, "A5", 0.5), (50, "B5", 1.5), (51.5, "A5", 0.5),
    (52, "F#5", 1.0), (53, "D5", 2.0),
    (56, "E5", 1.0), (57, "G#5", 1.0), (58, "B5", 1.0), (59, "D6", 1.0), (60, "C#6", 1.5),
    (61.5, "B5", 0.5), (62, "G#5", 1.5),
]


def menu_events():
    ev = []
    for start, root, tones in chord_slots(MENU_SLOTS, 8):
        for v, t in enumerate(tones):
            ev.append((start, warmpad, t, 9.0, 0.16 - 0.02 * v))
        # Bass on one and three, walking up to the next chord on the last beat of the slot.
        for b in (0, 2, 4, 6):
            ev.append((start + b, softbass, root, 1.6, 0.8 if b % 4 == 0 else 0.6))
        # Marimba eighths up and down the chord, an octave up.
        ladder = [t[:-1] + str(int(t[-1]) + 1) for t in tones]
        ladder = ladder + [ladder[1]]
        for e in xrange(16):
            ev.append((start + e * 0.5, marimba, ladder[e % len(ladder)], 0.5, 0.32 if e % 2 else 0.42))
    for bar in xrange(MENU_BEATS // 4):
        b = bar * 4
        ev.append((b + 0.0, kick, "A1", 0.5, 0.55))
        ev.append((b + 2.0, kick, "A1", 0.5, 0.45))
        ev.append((b + 1.0, snap, "A4", 0.3, 0.45))
        ev.append((b + 3.0, snap, "A4", 0.3, 0.45))
        for h in xrange(8):
            ev.append((b + h * 0.5, hat, "A6", 0.12, 0.10 if h % 2 else 0.16))
    for beat, pitch, dur in MENU_TUNE:
        ev.append((beat, glock, pitch, dur, 0.55))
        ev.append((beat, marimba, pitch, dur, 0.25))
    return ev


# ---------------------------------------------------------------------------
# battle: driving, 152 BPM. I - vi - IV - V, three times; the tune enters the second time.
# ---------------------------------------------------------------------------

BATTLE_BPM = 152.0
BATTLE_SLOTS = [
    ("A1", "A2", ["A3", "C#4", "E4"]), ("F#1", "F#2", ["F#3", "A3", "C#4"]),
    ("D2", "D2", ["D3", "F#3", "A3"]), ("E2", "E2", ["E3", "G#3", "B3"]),
] * 3
BATTLE_BEATS = 8 * len(BATTLE_SLOTS)      # 96 beats, 37.9 s

_TUNE = [
    (0, "A4", 1.0), (1, "C#5", 1.0), (2, "E5", 1.5), (3.5, "A5", 0.5), (4, "G#5", 1.0),
    (5, "E5", 1.0), (6, "F#5", 2.0),
    (8, "F#5", 1.0), (9, "E5", 0.5), (9.5, "C#5", 1.5), (11, "A4", 1.0), (12, "C#5", 1.0),
    (13, "E5", 1.0), (14, "F#5", 2.0),
    (16, "D5", 1.0), (17, "F#5", 1.0), (18, "A5", 1.5), (19.5, "B5", 0.5), (20, "A5", 1.0),
    (21, "F#5", 1.0), (22, "D5", 2.0),
]
BATTLE_TUNE = (
    [(32 + b, p, d) for b, p, d in _TUNE]
    + [(56, "E5", 1.5), (57.5, "F#5", 0.5), (58, "G#5", 1.5), (59.5, "A5", 0.5), (60, "B5", 2.0),
       (62, "G#5", 1.0), (63, "E5", 1.0)]
    + [(64 + b, p, d) for b, p, d in _TUNE]
    + [(88, "E6", 2.0), (90, "D6", 1.0), (91, "C#6", 1.0), (92, "B5", 2.0), (94, "G#5", 1.0),
       (95, "E5", 1.0)]
)

# Where the power chords hit in each bar - pushed off the beat, so the bar drives forward.
STABS = [(0.0, 0.6), (0.75, 0.35), (1.5, 0.6), (2.5, 0.35), (3.0, 0.4), (3.5, 0.4)]


def battle_events():
    ev = []
    for start, low, chord_root, tones in [(s, r[0], r[1], r[2])
                                           for s, r in zip([k * 8 for k in xrange(len(BATTLE_SLOTS))],
                                                           BATTLE_SLOTS)]:
        octave = low[:-1] + str(int(low[-1]) + 1)
        for e in xrange(16):
            # Driving eighths, jumping the octave on the last two of every bar.
            pitch = octave if e % 8 >= 6 else low
            ev.append((start + e * 0.5, bass, pitch, 0.45, 0.85 if e % 2 == 0 else 0.65))
        for bar in (0, 4):
            for off, length in STABS:
                ev.append((start + bar + off, power, chord_root, length, 0.30))
        ladder = [t[:-1] + str(int(t[-1]) + 1) for t in tones]
        ladder = ladder + [ladder[1]]
        for e in xrange(16):
            ev.append((start + e * 0.5, pluck, ladder[e % len(ladder)], 0.45, 0.22))
    for bar in xrange(BATTLE_BEATS // 4):
        b = bar * 4
        for k in xrange(4):
            ev.append((b + k, kick, "A1", 0.5, 0.9 if k % 2 == 0 else 0.75))
        if bar % 2 == 1:
            ev.append((b + 3.5, kick, "A1", 0.5, 0.6))
        ev.append((b + 1.0, snare, "A4", 0.4, 0.7))
        ev.append((b + 3.0, snare, "A4", 0.4, 0.7))
        if bar % 8 == 7:
            ev.append((b + 3.5, snare, "A4", 0.25, 0.45))
            ev.append((b + 3.75, snare, "A4", 0.25, 0.55))
        for h in xrange(8):
            ev.append((b + h * 0.5, hat, "A6", 0.1, 0.16 if h % 2 else 0.24))
        if bar % 8 == 0:
            ev.append((b, crash, "A6", 2.5, 0.35))
    for beat, pitch, dur in BATTLE_TUNE:
        ev.append((beat, lead, pitch, dur, 0.5))
    return ev


# ---------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------

TRACKS = [
    # name, events, bpm, beats, reverb mix, peak dBFS
    ("menu", menu_events, MENU_BPM, MENU_BEATS, 0.22, -3.0),
    ("battle", battle_events, BATTLE_BPM, BATTLE_BEATS, 0.12, -3.0),
]


def build_all(out_dir, only=None):
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    for name, fn, bpm, beats, reverb_mix, target_db in TRACKS:
        if only and name not in only:
            continue
        buf = render(fn(), bpm, beats)
        # The reverb is wrapped the same way the notes are: a second copy of the loop's start
        # is appended, reverbed with it, and its tail folded back - so the loop point does not
        # cut the room off.
        n = len(buf)
        wet = S.reverb(buf + buf[:S.n_samples(1.5)], size=0.8, damp=0.45, mix=reverb_mix)
        looped = wet[:n]
        for i in xrange(n, len(wet)):
            looped[i - n] = wet[i]
        looped = S.normalize(S.dc_block(looped), 10.0 ** (target_db / 20.0))
        path = os.path.join(out_dir, name + ".wav")
        pk, rm, secs, clipped = S.write_wav_loop(path, S.halve_rate(looped), sr=S.SR // 2)
        print("music %-7s %5.1f s at %3.0f BPM  peak %5.1f dBFS  rms %5.1f dBFS  %4.1f MB%s"
              % (name, secs, bpm, pk, rm, os.path.getsize(path) / 1048576.0,
                 "  CLIPPED x%d" % clipped if clipped else ""))


if __name__ == "__main__":
    build_all(os.path.join(HERE, "..", "..", "assets", "music"), sys.argv[1:])
