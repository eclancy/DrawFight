# -*- coding: utf-8 -*-
"""The two music loops - menus and battle - built on synth.py.

    python tools/audio/build.py --music

Writes assets/music/menu.wav and assets/music/battle.wav: mono, 22050 Hz, each marked as a
whole-file loop with a 'smpl' chunk (see synth.write_wav_loop). Mono at half rate is about
2.6 MB a minute, which matters in a repo with no LFS where a loop is re-rendered on every
tuning pass.

Both are in A major, the key of every stinger in sfx.py (count_go is an A major chord,
game_set climbs A-C#-E-A), so the countdown lands in the key the battle music then plays in,
and the battle loop opens on that same A chord.

Both are cartoon music on purpose: Eric's call on 2026-10-04 was "something more fun". The
menu is a swung ukulele tune, whistled, with a toy piano answering; the battle is a bouncy ska
romp with a handheld-console lead and a brass section answering it. The harmony leans on
secondary dominants (C#7, F#7, B7, E7). A chord that wants to fall to the next one is most of
what makes music sound like a cartoon.

Nobody on this project can hear the result, so every part is written to rules that can be
checked on paper: chord tones on the strong beats, passing notes only on the weak ones, and no
semitone rub between a tune and the chord under it.

The sequencer, the note cache and the wrapped tails come from WizardSurvivors'
tools/audio/music.py. render() runs past the end of the loop and folds the overhang back onto
the start, so a ringing note or a reverb tail crosses the loop seam instead of being cut at it.
"""
from __future__ import division

import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import synth as S

_PITCH_CLASS = {
    "C": -9, "C#": -8, "Db": -8, "D": -7, "D#": -6, "Eb": -6, "E": -5, "E#": -4,
    "F": -4, "F#": -3, "Gb": -3, "G": -2, "G#": -1, "Ab": -1,
    "A": 0, "A#": 1, "Bb": 1, "B": 2, "B#": 3,
}


def note(name):
    """Scientific pitch notation to Hz. note("A4") is 440."""
    i = len(name)
    while i > 0 and (name[i - 1].isdigit() or name[i - 1] == "-"):
        i -= 1
    return 440.0 * (2.0 ** ((_PITCH_CLASS[name[:i]] + (int(name[i:]) - 4) * 12) / 12.0))


def _oct(name, k):
    """The same note k octaves away."""
    i = len(name)
    while i > 0 and name[i - 1].isdigit():
        i -= 1
    return name[:i] + str(int(name[i:]) + k)


# ---------------------------------------------------------------------------
# instruments - each takes (frequency, seconds) and returns a buffer
# ---------------------------------------------------------------------------

def _phase(freq):
    """A start phase that differs from note to note. Chord notes struck together all starting
    at phase zero line their first edges up into one spike, which then sets the loop's peak."""
    return (freq * 0.6180339887) % 1.0


def _glide(freq, n, start, secs):
    """A pitch curve that starts at freq * start and settles on freq."""
    k = min(n, S.n_samples(secs))
    return S.ramp(k, freq * start, freq, 0.5) + [freq] * (n - k)


def _vibrato(f, rate, depth, delay):
    """Vibrato that only arrives once a note has been held - a singer's, not a siren's."""
    out = list(f)
    d0 = S.n_samples(delay)
    grow = S.SR * 0.2
    for i in xrange(d0, len(f)):
        t = i - d0
        out[i] = f[i] * (1.0 + depth * min(1.0, t / grow) * math.sin(S.TWO_PI * rate * t / S.SR))
    return out


def _pluck(freq, dur, bright, t60, seed):
    """Karplus-Strong: a burst of noise going round a loop one period long, losing its top
    end each time round. It is a real plucked string's physics, so it sounds like one."""
    n = S.n_samples(dur)
    loop = S.SR / freq - 0.5           # the two-point average below adds half a sample
    whole = int(loop)
    frac = loop - whole
    rng = random.Random(seed)
    burst = S.lowpass([rng.uniform(-1.0, 1.0) for _ in xrange(whole + 2)], bright, 0.7)
    mean = sum(burst) / len(burst)
    burst = [v - mean for v in burst]  # DC in the burst would never decay out of the loop
    g = 0.001 ** (1.0 / (t60 * freq))
    a, b, c = 0.5 * g * (1.0 - frac), 0.5 * g, 0.5 * g * frac
    y = [0.0] * n
    nb = len(burst)
    for i in xrange(n):
        v = burst[i] if i < nb else 0.0
        j = i - whole
        if j >= 2:
            v += a * y[j] + b * y[j - 1] + c * y[j - 2]
        elif j >= 0:
            v += a * y[j]
        y[i] = v
    # Left raw, the first instant of every strum - the burst - is the loudest thing in the
    # track and sets the peak the whole loop is scaled to. Saturating it and giving it a few
    # milliseconds of rise brings the ring up to meet it. The end is damped by the next strum,
    # not cut, so a chop is not a click.
    y = S.drive(S.normalize(y, 1.0), 2.0)
    ka = min(n, S.n_samples(0.003))
    k = min(n - ka, S.n_samples(0.025))
    return S.env_mul(y, S.ramp(ka, 0.0, 1.0) + [1.0] * (n - ka - k) + S.ramp(k, 1.0, 0.0))


# --- the menu band ---------------------------------------------------------

def uke(freq, dur):
    """A ukulele string. Nylon, so the burst is soft and the ring is short."""
    return _pluck(freq, dur, 2400.0, 1.0, int(freq * 10))


def upright(freq, dur):
    """The walking bass: round, with a thump at the front - a string pulled hard starts sharp.
    More overtone than fundamental, because a TV speaker cannot play 55 Hz and the ear finds
    the note from its harmonics anyway."""
    n = S.n_samples(dur)
    f = _glide(freq, n, 1.025, 0.03)
    v = S.mix(S.gain(S.osc("sine", f, dur), 0.6), S.gain(S.osc("tri", f, dur), 0.6),
              S.gain(S.osc("saw", f, dur), 0.3))
    v = S.lowpass(v, S.sweep(dur, 2200, 700, 0.4), 0.9)
    return S.env_mul(v, S.adsr(dur, 0.01, 0.18, 0.45, min(0.08, dur * 0.3)))


def whistle(freq, dur):
    """The menu tune, whistled: a near-pure tone with breath in it, scooping up into each note
    the way lips do."""
    n = S.n_samples(dur)
    f = _vibrato(_glide(freq, n, 0.955, 0.06), 5.2, 0.007, 0.18)
    tone = S.mix(S.osc("sine", f, dur), S.gain(S.osc("sine", [x * 2.0 for x in f], dur), 0.05))
    breath = S.gain(S.bandpass(S.osc("noise", 0, dur, seed=241), freq, 5.0), 1.2)
    return S.env_mul(S.mix(tone, breath), S.adsr(dur, 0.025, 0.08, 0.85, min(0.07, dur * 0.3)))


def toypiano(freq, dur):
    """A toy piano: struck metal rods, so the overtone is nowhere near harmonic and dies first.
    That plink is the whole instrument. Rings the same length whatever the note's value."""
    v = S.partials(freq, [1.0, 2.0, 6.27], 1.0, amps=[1.0, 0.1, 0.3], decays=[5.5, 8.0, 35.0])
    tick = S.highpass(S.osc("noise", 0, 0.004, seed=242), 3000)
    return S.mix(v, S.gain(S.env_mul(tick, S.expdecay(len(tick), 3.0)), 0.15))


def snap(freq, dur):
    """A finger snap - the menu's backbeat, much lighter than a snare."""
    n = S.n_samples(dur)
    return S.env_mul(S.bandpass(S.osc("noise", 0, dur, seed=212), 2800, 1.4), S.expdecay(n, 22.0))


def shaker(freq, dur):
    """A shaker. A soft front and a dark centre let it carry the swing without hissing, which
    matters in a loop that plays for minutes."""
    d = 0.08
    n = S.n_samples(d)
    k = S.n_samples(0.012)
    env = S.ramp(k, 0.0, 1.0) + S.decay(n - k, 2.5)
    return S.env_mul(S.bandpass(S.osc("noise", 0, d, seed=243), 5000, 1.2), env)


# --- the battle band -------------------------------------------------------

def _chip(freq, dur, scoop, scoop_secs):
    n = S.n_samples(dur)
    f = _vibrato(_glide(freq, n, scoop, scoop_secs), 6.0, 0.006, 0.16)
    # A 25% pulse averages half below zero; lift it, or every note starts with a thump.
    p = [v + 0.5 for v in S.osc("pulse", f, dur, duty=0.25)]
    v = S.mix(S.gain(p, 0.6), S.gain(S.osc("tri", f, dur), 0.5))
    v = S.lowpass(v, 3000, 0.8)
    return S.env_mul(v, S.adsr(dur, 0.004, 0.08, 0.6, min(0.05, dur * 0.3)))


def chip(freq, dur):
    """The battle lead: a narrow pulse like an old handheld's, the edge taken off it so it never
    gets shrill under the hit sounds, and a tiny scoop into every note."""
    return _chip(freq, dur, 0.97, 0.018)


def chipslide(freq, dur):
    """The lead's accented notes slide up a whole step - the cartoon 'bwee'."""
    return _chip(freq, dur, 0.89, 0.07)


def horn(freq, dur):
    """The answering voice: a cartoon brass section in one - two detuned saws whose filter
    opens as the note speaks, then closes, which is the 'bwah' of a horn."""
    n = S.n_samples(dur)
    f = _glide(freq, n, 0.975, 0.045)
    ph = _phase(freq)
    v = S.mix(S.osc("saw", f, dur, phase=ph),
              S.gain(S.osc("saw", [x * 1.007 for x in f], dur, phase=(ph + 0.37) % 1.0), 0.8))
    k = min(n, S.n_samples(0.04))
    v = S.lowpass(v, S.ramp(k, 500, 2600, 0.7) + S.ramp(n - k, 2600, 1100, 0.6), 1.2)
    return S.env_mul(S.drive(v, 1.6), S.adsr(dur, 0.012, 0.12, 0.7, min(0.06, dur * 0.3)))


def skank(freq, dur):
    """One note of the offbeat chop: clipped and hollow, so it is felt as rhythm, not harmony."""
    ph = _phase(freq)
    v = S.mix(S.gain(S.osc("square", freq, dur, phase=ph), 0.5),
              S.gain(S.osc("saw", freq * 2.0, dur, phase=(ph + 0.3) % 1.0), 0.2))
    v = S.highpass(S.lowpass(v, 2600, 0.9), 250)
    return S.env_mul(v, S.ad(dur, 0.002, 3.5))


def _bounce(freq, dur, fall, hold):
    """The battle bass: square and sine with a filter snap, and a pitch that sags at the tail.
    Weighted to the square's overtones rather than the sine's fundamental, for the same reason
    as the menu's bass: the fundamental is mostly headroom spent on nothing a TV can play."""
    n = S.n_samples(dur)
    k = int(n * hold)
    f = [freq] * k + S.ramp(n - k, freq, freq * fall, 1.6)
    v = S.mix(S.gain(S.osc("square", f, dur), 0.55), S.gain(S.osc("sine", f, dur), 0.5))
    v = S.lowpass(v, S.sweep(dur, 2000, 450, 0.5), 1.3)
    # A 12 ms rise is still a pluck, and it lets the kick's click land first instead of on
    # top of the bass's own front - the two together were the loudest moment of every bar.
    return S.drive(S.env_mul(v, S.ad(dur, 0.012, 1.3)), 1.8)


def bass(freq, dur):
    """Most bass notes droop a few cents as they end - just enough to sound like rubber."""
    return _bounce(freq, dur, 0.97, 0.5)


def bassdrop(freq, dur):
    """The last note of a phrase falls properly: 'bwow'."""
    return _bounce(freq, dur, 0.75, 0.3)


def bassfall(freq, dur):
    """The breakdown's first hit sinks a whole octave - the floor going out from under it."""
    return _bounce(freq, dur, 0.5, 0.05)


def clap(freq, dur):
    """Three hands not quite together, then the room - the spread is what makes it a clap."""
    parts = []
    for k, off in enumerate((0.0, 0.009, 0.018)):
        b = S.osc("noise", 0, 0.01, seed=231 + k)
        parts.append((S.env_mul(b, S.expdecay(len(b), 3.0)), off, 0.9))
    tail = S.osc("noise", 0, 0.18, seed=234)
    parts.append((S.env_mul(tail, S.expdecay(len(tail), 8.0)), 0.026, 0.8))
    return S.highpass(S.bandpass(S.layer(parts), 1250, 1.0), 400)


def block(freq, dur):
    """A woodblock: a hollow knock with only a hint of pitch, gone in a few hundredths."""
    v = S.partials(freq, [1.0, 2.57], 0.1, amps=[1.0, 0.3], decays=[9.0, 14.0])
    knock = S.bandpass(S.osc("noise", 0, 0.006, seed=235), freq * 2.0, 2.0)
    return S.mix(v, S.gain(S.env_mul(knock, S.expdecay(len(knock), 3.0)), 0.5))


def tom(freq, dur):
    """A tom for the fills, tuned to the chord it falls through."""
    d = 0.32
    n = S.n_samples(d)
    body = S.env_mul(S.osc("sine", S.ramp(n, freq * 1.6, freq, 0.3), d), S.expdecay(n, 6.5))
    stick = S.env_mul(S.bandpass(S.osc("noise", 0, d, seed=236), 1800, 1.0), S.expdecay(n, 40.0))
    return S.drive(S.mix(body, S.gain(stick, 0.5)), 1.4)


def slidewhistle(freq, dur):
    """A slide whistle rising a fifth to freq and holding it - 'fweeep', back to the top."""
    n = S.n_samples(dur)
    k = int(n * 0.85)
    f = _vibrato(S.ramp(k, freq / 1.5, freq, 0.8) + [freq] * (n - k), 7.0, 0.01, 0.0)
    v = S.mix(S.osc("sine", f, dur), S.gain(S.osc("sine", [x * 2.0 for x in f], dur), 0.08),
              S.gain(S.highpass(S.osc("noise", 0, dur, seed=237), 3000), 0.04))
    return S.env_mul(v, S.adsr(dur, 0.04, 0.05, 0.9, min(0.06, dur * 0.2)))


def kick(freq, dur):
    """Short, with a click on the front: it is heard by its knock, not its boom, so it can
    punch without setting the peak of the whole loop."""
    n = S.n_samples(dur)
    body = S.env_mul(S.osc("sine", S.sweep(dur, 160, 50, 0.3), dur), S.expdecay(n, 9.0))
    click = S.bandpass(S.osc("noise", 0, 0.005, seed=215), 3000, 1.0)
    click = S.env_mul(click, S.expdecay(len(click), 3.0))
    return S.drive(S.mix(body, S.gain(click, 0.6)), 1.5)


def snare(freq, dur):
    n = S.n_samples(dur)
    noise = S.env_mul(S.bandpass(S.osc("noise", 0, dur, seed=211), 2200, 0.8), S.expdecay(n, 9.0))
    body = S.env_mul(S.osc("tri", S.sweep(dur, 240, 170), dur), S.expdecay(n, 14.0))
    return S.mix(noise, S.gain(body, 0.6))


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


def chord_at(chart, beat):
    """The chord symbol sounding at a beat. A chart bar is one symbol, or two for half a bar each."""
    bar = chart[int(beat // 4) % len(chart)]
    if isinstance(bar, tuple):
        return bar[0] if beat % 4 < 2 else bar[1]
    return bar


# ---------------------------------------------------------------------------
# menu: a swung ukulele tune, 116 BPM, sixteen bars - A, then B.
#
# The A section's second chord is C#7, which leads somewhere it then does not go (D), and its
# fourth is D minor - the borrowed minor iv that sounds like a wink. B is a bridge that ends on
# E7, the V7 of the A the loop opens on, so the end of the loop pulls straight into its start.
# It plays for minutes on the select screens, so it stays light: nothing in it is louder than
# the tune, and there is no kick drum.
# ---------------------------------------------------------------------------

MENU_BPM = 116.0
MENU_SWING = 0.63       # where an off-beat eighth lands: 0.5 is straight, 0.667 a full shuffle

MENU_CHART = ["A", "C#7", "D", "Dm", "A", "F#7", ("Bm7", "E7"), "A",
              "D", "D", "A", "A", "B7", "B7", "E7", "E7"]
MENU_BEATS = 4 * len(MENU_CHART)     # 64 beats, 33.1 s

# A real ukulele's voicings sit inside about one octave above middle C. Each moves to the next
# by the smallest step it can, so the strum never jumps.
UKE = {
    "A": ["C#4", "E4", "A4"],
    "C#7": ["C#4", "E#4", "G#4", "B4"],
    "D": ["D4", "F#4", "A4"],
    "Dm": ["D4", "F4", "A4"],
    "F#7": ["C#4", "E4", "F#4", "A#4"],
    "Bm7": ["D4", "F#4", "A4", "B4"],
    "E7": ["D4", "E4", "G#4", "B4"],
    "B7": ["D#4", "F#4", "A4", "B4"],
}

# The island strum: down, down-up, up-down-up. Up strokes catch only the top three strings.
STRUM = [(0.0, "down", 0.9), (1.0, "down", 0.8), (1.5, "up", 0.55),
         (2.5, "up", 0.7), (3.0, "down", 0.8), (3.5, "up", 0.55)]
STRUM_SPREAD = 0.022    # beats between strings - about 11 ms, a relaxed hand

# A walking bass, a bar to a line: the root on one, chord tones on three, and the fourth beat
# stepping into the next bar's root.
MENU_WALK = [
    ["A1", "C#2", "E2", "D2"], ["C#2", "G#1", "B1", "C#2"], ["D2", "E2", "F#2", "E2"],
    ["D2", "F2", "D2", "B1"], ["A1", "C#2", "E2", "F2"], ["F#2", "E2", "C#2", "A#1"],
    ["B1", "D2", "E2", "G#1"], ["A1", "C#2", "E2", "C#2"],
    ["D2", "F#2", "A2", "F#2"], ["D2", "F#2", "A1", "B1"], ["A1", "B1", "C#2", "E2"],
    ["A2", "G#2", "E2", "C#2"], ["B1", "D#2", "F#2", "A2"], ["B1", "F#1", "A1", "D#2"],
    ["E2", "F#2", "G#2", "E2"], ["E2", "D2", "B1", "G#1"],
]

# The whistled tune, in straight beats; the swing is applied as it is played. Bars 1 and 2 are
# one figure moved onto the C#7 (E C# E A, then G# E# G# B), and bars 5 and 6 move it again
# onto the F#7: a tune that follows its chords around is the cartoon trick.
MENU_TUNE = [
    (0, "E5", 1.0), (1, "C#5", .5), (1.5, "E5", .5), (2, "A5", 2.0),
    (4, "G#5", 1.0), (5, "E#5", .5), (5.5, "G#5", .5), (6, "B5", 2.0),
    (8, "A5", 1.0), (9, "F#5", .5), (9.5, "A5", .5), (10, "D6", 2.0),
    (12, "A5", 1.5), (13.5, "F5", .5), (14, "D5", 2.0),
    (16, "E5", 1.0), (17, "C#5", .5), (17.5, "E5", .5), (18, "A5", 1.5), (19.5, "B5", .5),
    (20, "A#5", 1.0), (21, "F#5", .5), (21.5, "A#5", .5), (22, "C#6", 2.0),
    (24, "B5", 1.0), (25, "A5", .5), (25.5, "F#5", .5), (26, "G#5", 1.0), (27, "B5", .5),
    (27.5, "D6", .5),
    (28, "C#6", 2.0),
    (32, "F#5", 1.5), (33.5, "E5", .5), (34, "D5", 1.0), (35, "F#5", .5), (35.5, "A5", .5),
    (36, "A5", 1.0), (37, "B5", .5), (37.5, "A5", .5), (38, "F#5", 2.0),
    (40, "E5", 1.5), (41.5, "C#5", .5), (42, "E5", 1.0), (43, "A5", .5), (43.5, "C#6", .5),
    (44, "C#6", 1.5), (45.5, "B5", .5), (46, "A5", 2.0),
    (48, "F#5", 1.0), (49, "D#5", .5), (49.5, "F#5", .5), (50, "A5", 1.0), (51, "B5", .5),
    (51.5, "A5", .5),
    (52, "F#5", 2.0),
    (56, "E5", 1.0), (57, "G#5", .5), (57.5, "B5", .5), (58, "D6", 1.5), (59.5, "C#6", .5),
    (60, "B5", 1.5), (61.5, "G#5", 1.0),
]

# The toy piano answers in the gaps the whistler leaves, always from the chord underneath.
MENU_ANSWERS = [
    (30, "A5", .5), (30.5, "C#6", .5), (31, "E6", .5), (31.5, "F#6", .5),
    (38.5, "A5", .5), (39, "D6", .5), (39.5, "F#6", .5),
    (46.5, "E6", .5), (47, "C#6", .5), (47.5, "B5", .5),
    (54, "D#6", .5), (54.5, "B5", .5), (55, "A5", .5), (55.5, "F#5", .5),
    (62, "E6", .5), (62.5, "D6", .5), (63, "B5", .5), (63.5, "G#5", .5),
]


# The mix, as in sfx.py's REGISTRY: one level per part, and the events carry only accents. Set
# by measuring each part alone, A-weighted, against the whole while it plays: the whistle is
# within a dB of the whole - it is the tune - the toy piano 6 dB under, the uke 9, the snaps 13,
# the shaker 21 and the bass 19 (the A curve discounts bass; its overtones carry it). The uke
# is no louder because its strums are the track's peaks.
MENU_MIX = {"uke": 0.23, "upright": 0.72, "snap": 1.1, "shaker": 0.18, "whistle": 0.42,
            "toypiano": 0.3}


def _mixed(events, levels):
    return [(b, inst, p, d, v * levels[inst.__name__]) for b, inst, p, d, v in events]


def swing(beat):
    """Where a straight beat lands once the off-beat eighths are pushed late."""
    whole = math.floor(beat)
    return whole + MENU_SWING if abs(beat - whole - 0.5) < 1e-6 else beat


def menu_events():
    ev = []
    for bar in xrange(len(MENU_CHART)):
        b = bar * 4
        strum = STRUM
        if isinstance(MENU_CHART[bar], tuple):
            # The island strum has no stroke on three, so a chord that changes there would be
            # late by half a beat, ringing the old chord over the new bass. Strum the change.
            strum = sorted(STRUM + [(2.0, "down", 0.75)])
        for k, (off, stroke, vel) in enumerate(strum):
            at = swing(b + off)
            # A string rings until the hand comes back for the next stroke.
            ring = (swing(b + strum[k + 1][0]) if k + 1 < len(strum) else b + 4) - at
            tones = UKE[chord_at(MENU_CHART, b + off)]
            strings = tones if stroke == "down" else list(reversed(tones))[:3]
            for s, t in enumerate(strings):
                ev.append((at + s * STRUM_SPREAD, uke, t, ring, vel))
        for k, p in enumerate(MENU_WALK[bar]):
            ev.append((b + k, upright, p, 0.8, 1.0 if k == 0 else 0.87))
        ev.append((b + 1.0, snap, "A4", 0.3, 1.0))
        ev.append((b + 3.0, snap, "A4", 0.3, 1.0))
        for e in xrange(8):
            # The shaker leans on the late eighth - that is where the swing is felt.
            ev.append((swing(b + e * 0.5), shaker, "A6", 0.08, 1.0 if e % 2 else 0.65))
    for beat, pitch, dur in MENU_TUNE:
        start = swing(beat)
        ev.append((start, whistle, pitch, (swing(beat + dur) - start) * 0.95, 1.0))
    for beat, pitch, dur in MENU_ANSWERS:
        ev.append((swing(beat), toypiano, pitch, dur, 1.0))
    return _mixed(ev, MENU_MIX)


# ---------------------------------------------------------------------------
# battle: a bouncy ska romp, 164 BPM, twenty-eight bars.
#
#   A   (8)  the hook: the lead calls for a bar, the horns answer for a bar
#   B   (8)  the cartoon circle, A - F#7 - B7 - E7, twice, with a chase over it
#   A'  (8)  the hook again, each voice now doubling the other's half; ends on B7
#   break (2) stop-time on E: the band hits together, the claps keep going
#   lift  (2) G - D, which lands on the A at the top of the loop. bVII - IV - I is the
#             rock-and-roll way home, and a slide whistle throws it back to the start.
#
# Straight eighths (no swing), offbeat chops and an octave-bouncing bass are what make it
# bounce rather than drive. It plays under hit sounds that are louder than it, so the tune sits
# in the middle of the range and nothing in it is bright for long.
# ---------------------------------------------------------------------------

BATTLE_BPM = 164.0
BATTLE_CHART = (["A", "A", "D", "E", "A", "A", "D", "E7"]
                + ["A", "F#7", "B7", "E7"] * 2
                + ["A", "A", "D", "E", "A", "A", "B7", "B7"]
                + ["E", "E", "G", "D"])
BATTLE_BEATS = 4 * len(BATTLE_CHART)      # 112 beats, 41.0 s

# chord: (bass root, the offbeat chop's voicing). Round the circle the chop moves by half steps
# (A4 to A#4 and back, D#4 to D4 to C#4), which is where its cartoon slither comes from.
BATTLE_CHORDS = {
    "A": ("A1", ["C#4", "E4", "A4"]),
    "D": ("D2", ["D4", "F#4", "A4"]),
    "E": ("E2", ["B3", "E4", "G#4"]),
    "E7": ("E2", ["D4", "G#4", "B4"]),
    "F#7": ("F#1", ["C#4", "E4", "A#4"]),
    "B7": ("B1", ["D#4", "F#4", "A4"]),
    "G": ("G1", ["B3", "D4", "G4"]),
}

SLIDE = True

# The hook, in beats from the top of its bar. Every note of the call is a chord tone except the
# G# that bounces off the A. The answer is the same climb (1-2-3-5) on whichever chord it is
# answering, so it is heard as one idea.
CALL_A = [(0, "E5", .5), (.5, "C#5", .5), (1, "E5", .5), (1.5, "A5", 1.0, SLIDE),
          (2.5, "G#5", .5), (3, "A5", .5)]
CALL_D = [(0, "F#5", .5), (.5, "D5", .5), (1, "F#5", .5), (1.5, "A5", 1.0, SLIDE),
          (2.5, "B5", .5), (3, "A5", .5)]
ANSWER_A = [(1, "A4", .5), (1.5, "B4", .5), (2, "C#5", .5), (2.5, "E5", 1.0)]
ANSWER_E = [(1, "E4", .5), (1.5, "F#4", .5), (2, "G#4", .5), (2.5, "B4", 1.0)]

# In A' the horns play the call an octave down. A straight octave would put a G#4 against the
# chop's A4, so the horn takes a B there - a sixth under the lead instead of a rub with the chop.
CALL_A_LOW = [(0, "E4", .5), (.5, "C#4", .5), (1, "E4", .5), (1.5, "A4", 1.0),
              (2.5, "B4", .5), (3, "A4", .5)]
CALL_D_LOW = [(0, "F#4", .5), (.5, "D4", .5), (1, "F#4", .5), (1.5, "A4", 1.0),
              (2.5, "B4", .5), (3, "A4", .5)]

# The chase over the circle: each bar climbs its own chord from the third, and a passing note on
# the last eighth leads into the next chord. The second time round it climbs higher, and its
# last bar runs chromatically into the hook's first note (the chop drops out under that run).
CIRCLE = [
    (32, "C#5", .5), (32.5, "E5", .5), (33, "A5", .5), (33.5, "E5", .5), (34, "C#5", 1.0),
    (35.5, "B4", .5),
    (36, "A#4", .5), (36.5, "C#5", .5), (37, "E5", .5), (37.5, "C#5", .5), (38, "A#4", 1.0),
    (39.5, "C#5", .5),
    (40, "D#5", .5), (40.5, "F#5", .5), (41, "A5", .5), (41.5, "F#5", .5), (42, "D#5", 1.0),
    (43.5, "A5", .5),
    (44, "G#5", .5), (44.5, "B5", .5), (45, "D6", .5), (45.5, "B5", .5), (46, "G#5", 1.0),
    (47.5, "B5", .5),
    (48, "C#6", 1.5, SLIDE), (49.5, "B5", .5), (50, "A5", .5), (50.5, "E5", .5), (51, "C#5", .5),
    (51.5, "B4", .5),
    (52, "A#4", .5), (52.5, "C#5", .5), (53, "E5", .5), (53.5, "F#5", .5), (54, "A#5", 1.0, SLIDE),
    (55, "F#5", .5), (55.5, "E5", .5),
    (56, "D#5", .5), (56.5, "F#5", .5), (57, "A5", .5), (57.5, "B5", .5), (58, "D#6", 1.0, SLIDE),
    (59, "B5", .5), (59.5, "A5", .5),
    (60, "G#5", .5), (60.5, "B5", .5), (61, "D6", .5), (61.5, "B5", .5), (62, "G#5", .5),
    (62.5, "E5", .5), (63, "D5", .5), (63.5, "D#5", .5),
]

# The horns under the chase play only each chord's third and seventh. Those two notes move by
# half steps all the way round the circle, which is the sound of it.
GUIDE = {"A": ["C#4", "E4"], "F#7": ["A#3", "E4"], "B7": ["A3", "D#4"], "E7": ["G#3", "D4"]}

# The end of A': a B7 run that climbs into the breakdown and lands on its first hit.
BUILD = [
    (88, "F#5", .5), (88.5, "A5", .5), (89, "B5", .5), (89.5, "D#6", 1.0, SLIDE), (90.5, "B5", .5),
    (91, "A5", .5), (91.5, "F#5", .5),
    (92, "D#5", .5), (92.5, "F#5", .5), (93, "A5", .5), (93.5, "B5", .5), (94, "D#6", .5),
    (94.5, "B5", .5), (95, "A5", .5), (95.5, "F#5", .5),
    (96, "E5", .5),
]

# The breakdown's stop-time: BAM . . . | . ba-BAM . | BAM . . . | . ba-BAM, then a tom fill.
BREAK_HITS = [(96, 0.75), (98.5, 0.35), (99, 0.75), (100, 0.75), (101.5, 0.35), (102, 0.75)]

# The lift, G then D, rising to the D an octave above the hook's top note before the whistle.
LIFT = [
    (104, "B4", .5), (104.5, "D5", .5), (105, "G5", 1.0, SLIDE), (106, "D5", .5), (106.5, "G5", .5),
    (107, "B5", 1.0),
    (108, "A5", .5), (108.5, "F#5", .5), (109, "A5", .5), (109.5, "D6", 1.0, SLIDE),
]

# The bass walks round the circle in quarters instead of bouncing - that is what makes the B
# section a chase. Its one chromatic step (F into F#) is on a weak beat.
BATTLE_WALK = [
    ["A1", "C#2", "E2", "F2"], ["F#2", "E2", "C#2", "A#1"],
    ["B1", "D#2", "F#2", "A2"], ["E2", "D2", "B1", "G#1"],
]

FILLS = (
    [(31.5, snare, "A4", 0.25, 0.35), (31.75, snare, "A4", 0.25, 0.45)]
    + [(62 + 0.5 * k, tom, p, 0.5, 0.5) for k, p in enumerate(["E3", "B2", "G#2", "E2"])]
    + [(94 + 0.25 * k, snare, "A4", 0.25, 0.18 + 0.05 * k) for k in xrange(8)]
    + [(102.5, tom, "E3", .5, .5), (103, tom, "B2", .5, .5), (103.25, tom, "B2", .5, .45),
       (103.5, tom, "G#2", .5, .55), (103.75, tom, "E2", .5, .6)]
    + [(110.5, tom, "D3", .5, .5), (111, tom, "A2", .5, .5), (111.25, tom, "A2", .5, .45),
       (111.5, tom, "F#2", .5, .55), (111.75, tom, "D2", .5, .6)]
)

# Chops held back so a fill, or the lead's chromatic run into A', has the end of the bar to itself.
NO_CHOP = (62.5, 63.5, 110.5, 111.5)

# The mix, set the same way as the menu's: the lead about 3 dB under the whole while it plays,
# the horns 4, the bass 6, the clap and the chop - which are the bounce - 9 and 10, the crash
# 13, and the hats right down at 22, because the hit sounds on top own the high end. The kick
# reads about 18 down only because the A curve discounts its weight; it is kept light on
# purpose, since kick and bass landing together set the loop's peak, and so how loud the whole
# loop can be.
BATTLE_MIX = {"chip": 0.4, "chipslide": 0.4, "horn": 0.23, "skank": 0.23, "bass": 0.52,
              "bassdrop": 0.52, "bassfall": 0.52, "kick": 0.43, "clap": 1.0, "hat": 0.19,
              "block": 0.16, "crash": 0.13, "tom": 0.75, "snare": 1.0, "slidewhistle": 0.22}


def _part(bar):
    if bar < 8:
        return "A"
    if bar < 16:
        return "B"
    if bar < 24:
        return "A2"
    return "break" if bar < 26 else "lift"


def _phrase(ev, phrase, at, vel, octave=0, instrument=None):
    """Plays a phrase from beat `at`. Notes are held for nine-tenths of their value: a bouncy
    tune is a slightly detached one."""
    for item in phrase:
        beat, pitch, dur = item[0], item[1], item[2]
        inst = instrument or (chipslide if len(item) > 3 else chip)
        ev.append((at + beat, inst, _oct(pitch, octave), dur * 0.9, vel))


def battle_events():
    ev = []
    for bar, chord in enumerate(BATTLE_CHART):
        b = bar * 4
        part = _part(bar)
        root, chop = BATTLE_CHORDS[chord]

        if part != "break":
            for off in (0.5, 1.5, 2.5, 3.5):
                if b + off not in NO_CHOP:
                    for t in chop:
                        ev.append((b + off, skank, t, 0.35, 1.0))

        if part == "B":
            for k, p in enumerate(BATTLE_WALK[bar % 4]):
                ev.append((b + k, bass, p, 0.8, 1.0))
            for t in GUIDE[chord]:
                ev.append((b, horn, t, 0.5, 0.67))
                ev.append((b + 2.5, horn, t, 0.75, 0.67))
        elif part != "break":
            # Octaves: the root on the beat, an octave up on the off-beat - the bounce. Every
            # second bar the last one falls away.
            hi = _oct(root, 1)
            for e in xrange(8):
                drop = e == 7 and bar % 2 == 1
                ev.append((b + e * 0.5, bassdrop if drop else bass, hi if e % 2 else root, 0.45,
                           0.9 if e % 2 else 1.0))

        if part == "lift":
            kicks = (0, 1, 2, 3)          # four on the floor for the climb home
        elif part == "break":
            kicks = ()                    # the hits bring their own
        else:
            kicks = (0, 2, 2.5) if bar % 2 else (0, 2)
        for k in kicks:
            ev.append((b + k, kick, "A1", 0.5, 1.0 if k in (0, 2) else 0.7))
        ev.append((b + 1, clap, "A4", 0.3, 1.0))
        ev.append((b + 3, clap, "A4", 0.3, 1.0))
        if part != "break":
            for h in xrange(8):
                ev.append((b + h * 0.5, hat, "A6", 0.1, 0.67 if h % 2 else 1.0))
        if part == "B":
            # Tick-tock on every beat: the clock of a cartoon chase.
            for k in xrange(4):
                ev.append((b + k, block, "E6" if k % 2 == 0 else "B5", 0.1, 1.0))
        elif part != "lift":
            # Elsewhere a 3-2 clave across each pair of bars.
            for k in ((0, 1.5, 3) if bar % 2 == 0 else (1, 2)):
                ev.append((b + k, block, "E6" if k == 0 else "B5", 0.1, 1.0))
        if bar in (0, 8, 16, 24, 26):
            ev.append((b, crash, "A6", 2.5, 1.0))

    for at, phrase in ((0, CALL_A), (8, CALL_D), (16, CALL_A), (24, CALL_D),
                       (64, CALL_A), (72, CALL_D), (80, CALL_A)):
        _phrase(ev, phrase, at, 1.0)
    for at, phrase in ((4, ANSWER_A), (12, ANSWER_E), (20, ANSWER_A), (28, ANSWER_E),
                       (68, ANSWER_A), (76, ANSWER_E), (84, ANSWER_A)):
        _phrase(ev, phrase, at, 1.0, instrument=horn)
    # A' is bigger because each voice joins the other's half, not because anything gets louder.
    for at, phrase in ((64, CALL_A_LOW), (72, CALL_D_LOW), (80, CALL_A_LOW)):
        _phrase(ev, phrase, at, 0.57, instrument=horn)
    for at, phrase in ((68, ANSWER_A), (76, ANSWER_E), (84, ANSWER_A)):
        _phrase(ev, phrase, at, 0.55, octave=1)
    _phrase(ev, CIRCLE, 0, 0.9)
    _phrase(ev, BUILD, 0, 0.95)
    _phrase(ev, LIFT, 0, 1.0)

    for at, length in ((88, .5), (90.5, .75), (92, .5), (93.5, .5)):
        for t in GUIDE["B7"]:
            ev.append((at, horn, t, length, 0.67))
    for at, length in BREAK_HITS:
        full = length > 0.5
        for t in ("E4", "G#4", "B4"):
            ev.append((at, horn, t, length, 0.73 if full else 0.53))
        for t in BATTLE_CHORDS["E"][1]:
            ev.append((at, skank, t, 0.35, 1.0))
        if at == BREAK_HITS[0][0]:
            ev.append((at, bassfall, "E2", 2.0, 1.05))
        else:
            ev.append((at, bass, "E2", length * 0.9, 1.0 if full else 0.75))
        if full:
            ev.append((at, kick, "A1", 0.5, 1.0))
    for at, length, tones in ((104, .5, ("B3", "D4")), (106.5, .75, ("B3", "D4")),
                              (108, .5, ("D4", "F#4")), (109.5, .75, ("D4", "F#4"))):
        for t in tones:
            ev.append((at, horn, t, length, 0.67))
    # Lands on the A at the seam; the wrap carries its last moment onto the loop's first beat.
    ev.append((110.5, slidewhistle, "A5", 1.7, 1.0))
    ev.extend(FILLS)
    return _mixed(ev, BATTLE_MIX)


# ---------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------

TRACKS = [
    # name, events, bpm, beats, reverb mix, peak dBFS
    ("menu", menu_events, MENU_BPM, MENU_BEATS, 0.2, -3.0),
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
        # The downsampling filter is wrapped too. Started cold, it filters the first samples as
        # if silence came before them, which is a one-sample tick at the seam whenever a loop
        # ends loud. Primed with the loop's own last samples, it starts where it will be on
        # every pass after the first. (The pre-roll is even, so the decimation stays in step.)
        preroll = 64
        halved = S.halve_rate(looped[-preroll:] + looped)[preroll // 2:]
        path = os.path.join(out_dir, name + ".wav")
        pk, rm, secs, clipped = S.write_wav_loop(path, halved, sr=S.SR // 2)
        print("music %-7s %5.1f s at %3.0f BPM  peak %5.1f dBFS  rms %5.1f dBFS  %4.1f MB%s"
              % (name, secs, bpm, pk, rm, os.path.getsize(path) / 1048576.0,
                 "  CLIPPED x%d" % clipped if clipped else ""))


if __name__ == "__main__":
    build_all(os.path.join(HERE, "..", "..", "assets", "music"), sys.argv[1:])
