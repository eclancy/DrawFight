# -*- coding: utf-8 -*-
"""The music - four loops and a sting - built on synth.py.

    python tools/audio/build.py --music             # all of it (a few minutes)
    python tools/audio/music.py story victory       # only the pieces named

Writes assets/music/: menu, battle, story and doombot, each marked as a whole-file loop with a
'smpl' chunk (see synth.write_wav_loop), and victory, a one-shot with no chunk, so Godot plays
it once. Mono, 22050 Hz. Mono at half rate is about 2.6 MB a minute, which matters in a repo
with no LFS where a loop is re-rendered on every tuning pass.

Everything is in A, the key of every stinger in sfx.py (count_go is an A major chord, game_set
climbs A-C#-E-A), so the countdown lands in the key the battle music then plays in, and the
battle loop opens on that same A chord. Menu, battle, story and victory are in A major.
DoomBot's theme is in A minor - the villain's key - and opens on bare fifths, A and E with no
third, so FIGHT!'s A major chord still sits on it.

All of it is cartoon music on purpose: Eric's call on 2026-10-04 was "something more fun". The
menu is a swung ukulele tune, whistled, with a toy piano answering; the battle is a bouncy ska
romp with a handheld-console lead and a brass section answering it; the story theme is a
storybook tiptoe of pizzicato, glockenspiel and bassoon; DoomBot's is a villain's march on tuba,
growling trombones and an organ, over a factory; the victory sting is a brass fanfare. The
harmony leans on secondary dominants (C#7, F#7, B7, E7). A chord that wants to fall to the next
one is most of what makes music sound like a cartoon.

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


# --- the story band --------------------------------------------------------

def pizz(freq, dur):
    """Pizzicato strings - a bowed string plucked with a fingertip, so the ring is short and
    woody. The tiptoe of every storybook cartoon."""
    return _pluck(freq, dur, 2600.0, 0.3, int(freq * 10) + 1)


def pizzbass(freq, dur):
    """The bass, plucked the same way: a double bass, darker and longer. Its overtones carry it
    on a TV, as with every bass here."""
    return _pluck(freq, dur, 1000.0, 0.9, int(freq * 10) + 2)


def glock(freq, dur):
    """A glockenspiel: struck steel bars. A bar's overtones are not harmonic - 2.76 and 5.4
    times the note - and die long before the note does, which is the 'ting'. It carries the
    story's tune because it lives far above talking: the voice blips sit under 760 Hz, and this
    never plays below 830. Rings the same length whatever the note's value, as a bar does."""
    v = S.partials(freq, [1.0, 2.76, 5.40], 0.9, amps=[1.0, 0.25, 0.08], decays=[5.5, 12.0, 24.0])
    tick = S.bandpass(S.osc("noise", 0, 0.004, seed=244), freq * 2.0, 1.5)
    return S.mix(v, S.gain(S.env_mul(tick, S.expdecay(len(tick), 3.0)), 0.15))


def bassoon(freq, dur):
    """The comic voice of every cartoon: a double reed, nasal and a little rude. A narrow pulse
    for the buzz of the reed, pushed through a formant near 500 Hz, which is where a bassoon
    honks. Played short, it is the one instrument here that tells a joke."""
    n = S.n_samples(dur)
    f = _vibrato(_glide(freq, n, 0.97, 0.025), 5.0, 0.006, 0.25)
    ph = _phase(freq)
    # A 20% pulse averages 0.6 below zero; lift it, as the battle lead's is lifted.
    p = [v + 0.6 for v in S.osc("pulse", f, dur, duty=0.2, phase=ph)]
    v = S.mix(S.gain(p, 0.5), S.gain(S.osc("saw", f, dur, phase=(ph + 0.5) % 1.0), 0.5))
    v = S.mix(S.gain(S.lowpass(v, 1600, 0.8), 0.6), S.gain(S.bandpass(v, 480, 2.5), 1.2))
    return S.env_mul(v, S.adsr(dur, 0.015, 0.06, 0.7, min(0.05, dur * 0.3)))


def triangle(freq, dur):
    """An orchestral triangle at the top of each half: 'ting'. Thin and high, out of the way."""
    v = S.partials(freq, [1.0, 2.43, 3.87, 5.31], 1.6, amps=[0.6, 1.0, 0.7, 0.45],
                   decays=[3.0, 3.5, 4.5, 5.5])
    return S.highpass(v, 2000)


# --- the DoomBot band ------------------------------------------------------

def tuba(freq, dur):
    """The march's tuba: a fat brass bass, the filter opening on the front of every note for
    the 'bwomp'. A saw, so its overtones carry it where a TV cannot play the note itself."""
    n = S.n_samples(dur)
    f = _glide(freq, n, 0.96, 0.03)
    v = S.mix(S.gain(S.osc("saw", f, dur), 0.7), S.gain(S.osc("sine", f, dur), 0.5))
    k = min(n, S.n_samples(0.05))
    v = S.lowpass(v, S.ramp(k, 200, 900, 0.7) + S.ramp(n - k, 900, 380, 0.6), 1.1)
    return S.drive(S.env_mul(v, S.adsr(dur, 0.012, 0.1, 0.65, min(0.06, dur * 0.3))), 1.8)


def chug(freq, dur):
    """The conveyor belt: a power chord - root and fifth - on a square and two saws, driven hard,
    the filter snapping shut on every note the way a palm-muted guitar's does. A bar of eighths
    of it is the factory floor. Root and fifth only, so it fits a major chord or a minor one."""
    ph = _phase(freq)
    v = S.mix(S.gain(S.osc("square", freq, dur, phase=ph), 0.45),
              S.gain(S.osc("saw", freq * 1.004, dur, phase=(ph + 0.3) % 1.0), 0.45),
              S.gain(S.osc("saw", freq * 1.4983, dur, phase=(ph + 0.6) % 1.0), 0.35))
    v = S.lowpass(v, S.sweep(dur, 1500, 300, 0.4), 1.2)
    return S.drive(S.env_mul(v, S.ad(dur, 0.005, 1.8)), 2.6)


def bone(freq, dur):
    """The villain's trombones: two detuned saws, the filter opening as the note speaks and
    closing behind it - the 'bwah' - darker than the battle's horns, and growling: the note
    flutters at 30 Hz, as a player growls into the mouthpiece. Menace, but brass-band menace."""
    n = S.n_samples(dur)
    f = _glide(freq, n, 0.96, 0.05)
    ph = _phase(freq)
    v = S.mix(S.osc("saw", f, dur, phase=ph),
              S.gain(S.osc("saw", [x * 1.006 for x in f], dur, phase=(ph + 0.41) % 1.0), 0.85))
    k = min(n, S.n_samples(0.06))
    v = S.lowpass(v, S.ramp(k, 300, 1900, 0.7) + S.ramp(n - k, 1900, 800, 0.6), 1.3)
    v = S.tremolo(v, 30.0, 0.18)
    return S.env_mul(S.drive(v, 2.0), S.adsr(dur, 0.015, 0.12, 0.75, min(0.07, dur * 0.3)))


def organ(freq, dur):
    """The mad scientist's organ: drawbars - the note, the octave under and over it, the twelfth
    and two octaves up - in sines, with a slow tremulant. It speaks at once and stops dead,
    which is half of what makes a run of it sound like a machine."""
    ph = _phase(freq)
    v = S.mix(S.osc("sine", freq, dur, phase=ph),
              S.gain(S.osc("sine", freq * 0.5, dur, phase=ph), 0.4),
              S.gain(S.osc("sine", freq * 2.0, dur, phase=(ph + 0.25) % 1.0), 0.6),
              S.gain(S.osc("sine", freq * 3.0, dur, phase=(ph + 0.5) % 1.0), 0.35),
              S.gain(S.osc("sine", freq * 4.0, dur, phase=(ph + 0.75) % 1.0), 0.25))
    # A little overdrive, as an organ through a hot amplifier has: five sines in step add up to a
    # spike far above the note's loudness, and that spike would set the peak of the whole loop.
    v = S.drive(S.tremolo(v, 6.0, 0.15), 1.6)
    v = S.env_mul(v, S.adsr(dur, 0.006, 0.0, 1.0, min(0.03, dur * 0.25)))
    click = S.bandpass(S.osc("noise", 0, 0.006, seed=256), 2500, 1.0)
    return S.mix(v, S.gain(S.env_mul(click, S.expdecay(len(click), 3.0)), 0.15))


def bleep(freq, dur):
    """The robot thinking: a tiny square blip, a sixteenth long, running round its chord."""
    d = 0.07
    v = S.lowpass(S.osc("square", freq, d, phase=_phase(freq)), 2800, 0.8)
    return S.env_mul(v, S.ad(d, 0.002, 3.0))


def anvil(freq, dur):
    """The backbeat is a hammer on an anvil, not a snare: DoomBot was built in a factory. Struck
    steel - partials that are not harmonic - over a crack of noise and a knock of weight. Short,
    so it is a beat and not a bell."""
    d = 0.3
    n = S.n_samples(d)
    ring = S.partials(freq, [1.0, 2.71, 4.13, 5.87], d, amps=[1.0, 0.6, 0.4, 0.25],
                      decays=[11.0, 15.0, 19.0, 24.0])
    crack = S.env_mul(S.bandpass(S.osc("noise", 0, d, seed=254), 2200, 0.9), S.expdecay(n, 28.0))
    body = S.env_mul(S.osc("sine", S.sweep(d, 260, 180), d), S.expdecay(n, 18.0))
    return S.drive(S.mix(S.gain(ring, 0.55), crack, S.gain(body, 0.5)), 1.8)


def piston(freq, dur):
    """The hi-hat is a piston: a short puff of air with a tick of metal in it, steady as a
    machine. Dark for a hat, as the battle's is, because the hit sounds own the top."""
    d = 0.05
    n = S.n_samples(d)
    puff = S.env_mul(S.bandpass(S.osc("noise", 0, d, seed=255), 5500, 1.1), S.expdecay(n, 11.0))
    tick = S.env_mul(S.osc("sine", freq, d), S.expdecay(n, 45.0))
    return S.mix(puff, S.gain(tick, 0.25))


def vent(freq, dur):
    """The machine letting off steam at the end of a phrase: 'pssssh', swelling and dying."""
    n = S.n_samples(dur)
    v = S.lowpass(S.highpass(S.osc("noise", 0, dur, seed=257), 2500), 7500)
    up = int(n * 0.15)
    return S.env_mul(v, S.ramp(up, 0.0, 1.0, 0.6) + S.decay(n - up, 1.8))


# --- the victory band -------------------------------------------------------

def trumpet(freq, dur):
    """The fanfare's lead: a bright saw and pulse, the filter flaring open as the note speaks,
    a vibrato that arrives only on the held note at the end."""
    n = S.n_samples(dur)
    f = _vibrato(_glide(freq, n, 0.97, 0.02), 5.5, 0.008, 0.25)
    ph = _phase(freq)
    p = [v + 0.4 for v in S.osc("pulse", f, dur, duty=0.3, phase=ph)]
    v = S.mix(S.gain(S.osc("saw", f, dur, phase=(ph + 0.5) % 1.0), 0.7), S.gain(p, 0.4))
    k = min(n, S.n_samples(0.03))
    v = S.lowpass(v, S.ramp(k, 900, 3800, 0.7) + S.ramp(n - k, 3800, 2400, 0.6), 1.0)
    return S.env_mul(S.drive(v, 1.5), S.adsr(dur, 0.008, 0.08, 0.8, min(0.08, dur * 0.3)))


def timpani(freq, dur):
    """A kettle drum: a tuned boom that settles onto its note, with overtones near the fifth and
    the octave, which is what gives a timpani its pitch."""
    d = 1.6
    n = S.n_samples(d)
    f = S.ramp(S.n_samples(0.05), freq * 1.05, freq, 0.5)
    f += [freq] * (n - len(f))
    body = S.mix(S.osc("sine", f, d), S.gain(S.osc("sine", [x * 1.5 for x in f], d), 0.4),
                 S.gain(S.osc("sine", [x * 1.99 for x in f], d), 0.25))
    body = S.env_mul(body, S.expdecay(n, 4.0))
    strike = S.env_mul(S.lowpass(S.osc("noise", 0, d, seed=258), 900), S.expdecay(n, 40.0))
    return S.drive(S.mix(body, S.gain(strike, 0.6)), 1.4)


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


def _place(events, bpm, n):
    """events: (beat, instrument, note name or None, length in beats, velocity), laid into n
    samples. Whatever runs past the end is cut; render() makes sure nothing does."""
    beat_s = 60.0 / bpm
    buf = [0.0] * n
    for beat, instrument, name, dur_beats, vel in events:
        v = _voice(instrument, note(name) if name else 0.0, dur_beats * beat_s)
        off = S.n_samples(beat * beat_s)
        for i in xrange(min(len(v), n - off)):
            buf[off + i] += v[i] * vel
    return buf


def render(events, bpm, total_beats, tail_beats=8):
    """A loop: the overhang past the end is folded back onto the start."""
    beat_s = 60.0 / bpm
    loop_n = S.n_samples(total_beats * beat_s)
    buf = _place(events, bpm, S.n_samples((total_beats + tail_beats) * beat_s))
    out = buf[:loop_n]
    for i in xrange(loop_n, len(buf)):
        out[(i - loop_n) % loop_n] += buf[i]
    return out


def render_once(events, bpm, total_beats):
    """A one-shot: plays once from silence, so nothing is folded back. total_beats includes
    the room the last notes need to ring out."""
    return _place(events, bpm, S.n_samples(total_beats * 60.0 / bpm))


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


def swing(beat, amount=MENU_SWING):
    """Where a straight beat lands once the off-beat eighths are pushed late."""
    whole = math.floor(beat)
    return whole + amount if abs(beat - whole - 0.5) < 1e-6 else beat


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
# story: a storybook tiptoe, 100 BPM, lightly swung, sixteen bars - A, then B.
#
# It plays under talking, for as long as a scene takes, so it is built to stay out of the way:
# no drums, nothing sustained, and the tune on a glockenspiel far above the talking blips. The
# middle of the range, where the blips are, has only the violins' short plucks in it. It is
# quieter than the menu at the same peak because plucks and bells are all front and no body.
#
# The A section opens on the oldest cartoon move there is, I to the diminished chord a half
# step up (A, A#dim7, Bm7, E7): a chord that sneaks. Its second half has A7 leaning into D, and
# D turning to D minor for a wink. B gives the bassoon the jokes - creeping up D#dim7, tumbling
# down F#7 - and ends on E7, which falls straight into the A at the top.
# ---------------------------------------------------------------------------

STORY_BPM = 100.0
STORY_SWING = 0.6       # lighter than the menu's: a skip, not a shuffle

STORY_CHART = ["A", "A#dim7", "Bm7", "E7", "A", "A7", ("D", "Dm"), ("A", "E7"),
               "D", "D#dim7", "A/E", "F#7", "Bm7", "E7", ("A", "F#7"), ("Bm7", "E7")]
STORY_BEATS = 4 * len(STORY_CHART)      # 64 beats, 38.4 s

# chord: (the bass on one and three, the violins' chord on two and four) - oom-pah, plucked.
# The violins move by the smallest step they can: A to A#dim7 to A7 is one note moving.
STORY_CHORDS = {
    "A": (("A2", "E2"), ["C#4", "E4", "A4"]),
    "A#dim7": (("A#2", "C#3"), ["C#4", "E4", "G4"]),
    "Bm7": (("B2", "F#2"), ["D4", "F#4", "A4"]),
    "E7": (("E2", "G#2"), ["D4", "G#4", "B4"]),
    "A7": (("A2", "C#3"), ["C#4", "E4", "G4"]),
    "D": (("D3", "A2"), ["D4", "F#4", "A4"]),
    "Dm": (("D3", "A2"), ["D4", "F4", "A4"]),
    "A/E": (("E2", "A2"), ["C#4", "E4", "A4"]),
    "D#dim7": (("D#3", "A2"), ["C4", "F#4", "A4"]),
    "F#7": (("F#2", "C#3"), ["C#4", "E4", "A#4"]),
}

# The glock's tune, in straight beats. Its figure - a note, the half step under it, the note
# again, then down the chord - is moved onto chord after chord, and over A#dim7 it lands a half
# step higher than it did over A: the sneak is in the tune as well as the chords. Every note on
# a beat is a chord tone; the half steps are on the off-beats.
STORY_TUNE = [
    (0, "E6", .5), (.5, "D#6", .5), (1, "E6", .5), (1.5, "C#6", .5), (2, "A5", 1.0),
    (4, "E6", .5), (4.5, "D#6", .5), (5, "E6", .5), (5.5, "C#6", .5), (6, "A#5", 1.0),
    (8, "F#6", .5), (8.5, "E#6", .5), (9, "F#6", .5), (9.5, "D6", .5), (10, "B5", 1.0),
    (12, "D6", .5), (13, "B5", .5), (14, "G#5", .5), (14.5, "A5", .5), (15, "B5", .5),
    (15.5, "D6", .5),
    (16, "E6", .5), (16.5, "D#6", .5), (17, "E6", .5), (17.5, "C#6", .5), (18, "A5", 1.0),
    (20, "G6", .5), (20.5, "F#6", .5), (21, "G6", .5), (21.5, "E6", .5), (22, "C#6", 1.0),
    (24, "A6", .5), (24.5, "G#6", .5), (25, "F#6", 1.0), (26, "F6", .5), (26.5, "E6", .5),
    (27, "D6", 1.0),
    (28, "C#6", 1.0), (29, "E6", .5), (30, "D6", .5), (30.5, "C#6", .5), (31, "B5", .5),
    (31.5, "G#5", .5),
    # B: the glock calls in the odd bars and leaves the even ones to the bassoon.
    (32, "A5", .5), (32.5, "D6", .5), (33, "F#6", .5), (33.5, "G6", .5), (34, "A6", 1.5),
    (41, "C#6", .5), (41.5, "E6", .5), (42, "A6", .5), (42.5, "G#6", .5), (43, "A6", 1.0),
    (44, "A#6", 1.0),
    (48, "F#6", .5), (48.5, "E#6", .5), (49, "F#6", .5), (49.5, "D6", .5), (50, "B5", 1.0),
    (52, "D6", .5), (53, "B5", .5), (54, "G#5", .5), (54.5, "A5", .5), (55, "B5", .5),
    (55.5, "D6", .5),
    (56, "E6", .5), (56.5, "D#6", .5), (57, "E6", .5), (57.5, "C#6", .5), (58, "A#5", 1.0),
    (59, "C#6", .5), (59.5, "E6", .5),
    (60, "D6", .5), (60.5, "C#6", .5), (61, "B5", 1.0),
]

# The bassoon's answers: creeping up D#dim7 and sliding back down a half step at a time;
# tumbling down F#7, 'boop, boop, boop'; and climbing E7 into the top of the loop.
STORY_ANSWERS = [
    (37, "F#3", .5), (37.5, "A3", .5), (38, "C4", .5), (38.5, "D#4", .5), (39, "F#4", .5),
    (39.5, "F4", .5), (40, "E4", 1.0),
    (45, "E4", .5), (45.5, "C#4", .5), (46, "A#3", .5), (47, "F#3", 1.0),
    (62, "E3", .5), (62.5, "G#3", .5), (63, "B3", .5), (63.5, "D4", .5),
]

# The mix, set as the menu's was, by measuring each part alone, A-weighted, against the whole
# while it plays: the glock - the tune - within half a dB of the whole, the bassoon 2 dB under
# in the bars it has to itself, the plucked bass 9, the violins 11 and the triangle 16. The
# bass and violins are the parts in the talking's range, so they are the ones kept down: in
# 250-800 Hz the story theme carries 5 dB less than the menu does.
STORY_MIX = {"pizzbass": 0.28, "pizz": 0.18, "glock": 0.42, "bassoon": 0.3, "triangle": 0.1}


def story_events():
    ev = []
    for bar in xrange(len(STORY_CHART)):
        b = bar * 4
        if isinstance(STORY_CHART[bar], tuple):
            lows = [STORY_CHORDS[c][0][0] for c in STORY_CHART[bar]]
        else:
            lows = STORY_CHORDS[STORY_CHART[bar]][0]
        for k, low in enumerate(lows):
            ev.append((b + 2 * k, pizzbass, low, 1.5, 1.0))
        for k in (1, 3):
            # A section, not one player: the three notes land a few milliseconds apart.
            for s, t in enumerate(STORY_CHORDS[chord_at(STORY_CHART, b + k)][1]):
                ev.append((b + k + s * 0.012, pizz, t, 0.8, 0.85 if k == 1 else 0.75))
    for beat, pitch, dur in STORY_TUNE:
        # A bar rings as long as it rings: one length for every note, so each is rendered once.
        ev.append((swing(beat, STORY_SWING), glock, pitch, 1.0, 1.0))
    for beat, pitch, dur in STORY_ANSWERS:
        start = swing(beat, STORY_SWING)
        # Short - the bassoon's joke is in how little of each note it plays.
        ev.append((start, bassoon, pitch, (swing(beat + dur, STORY_SWING) - start) * 0.7, 1.0))
    ev.append((0, triangle, "A6", 1.0, 1.0))
    ev.append((32, triangle, "A6", 1.0, 0.8))
    return _mixed(ev, STORY_MIX)


# ---------------------------------------------------------------------------
# doombot: his boss theme, 140 BPM, twenty-four bars, in A minor.
#
#   march    (8)  the villain's march: a tuba on every beat, trombones growling the theme, the
#                 bass sinking a half step a bar (A, G#, G, F#) under a held organ chord, then
#                 F - Bb - E, and a stop: 'dun, dun, DUNNN', and the machine lets off steam.
#   machine  (8)  the factory floor: a chugging power chord on every eighth, accents 3-3-2, the
#                 organ running one figure down Am - G - F - E like a machine stamping parts,
#                 then again an octave up; bleeps of a robot thinking under it.
#   overheat (4)  power chords climbing a half step a bar (A, Bb, B, C) under an organ siren,
#                 a drum roll and a long hiss of steam.
#   home     (4)  the march again on F - Dm - E, and the stop, whose E7 falls into the top.
#
# Menace from the minor key, the sinking bass and the Bb (the half step above A); cartoon
# from the tuba, the trombones' growl, the stop and its 'pssssh'. The game speeds it up on his
# last stock (MusicPlayer.SetSpeed), so it is written to stay clear at 175 BPM: nothing faster
# than sixteenths, and those only on the quiet parts.
#
# FIGHT! is an A major chord and lands on the top of this loop, so the loop opens on bare
# fifths - A and E, no C - for its first two beats. The minor third first sounds as a passing
# sixteenth, after the chord has rung.
# ---------------------------------------------------------------------------

DOOM_BPM = 140.0
DOOM_CHART = (["A5", "Am/G#", "Am/G", "Am/F#", "F", "Bb", "E", "E7"]
              + ["Am", "G", "F", "E", "Am", "G", ("F", "Dm"), "E7"]
              + ["A5", "Bb5", "B5", "C5"]
              + ["F", "Dm", "E", "E7"])
DOOM_BEATS = 4 * len(DOOM_CHART)        # 96 beats, 41.1 s
DOOM_STOPS = (7, 23)

# chord: (bass root, organ and trombone voicing, the bleeps' arpeggio)
DOOM_CHORDS = {
    "A5": ("A1", ["A3", "E4", "A4"], ["A5", "E6", "A6", "E6"]),
    "Am/G#": ("G#1", ["A3", "E4", "A4"], ["A5", "E6", "A6", "E6"]),
    "Am/G": ("G1", ["A3", "E4", "A4"], ["A5", "E6", "A6", "E6"]),
    "Am/F#": ("F#1", ["A3", "E4", "A4"], ["A5", "E6", "A6", "E6"]),
    "Am": ("A1", ["A3", "C4", "E4"], ["A5", "C6", "E6", "C6"]),
    "G": ("G1", ["G3", "B3", "D4"], ["G5", "B5", "D6", "B5"]),
    "F": ("F1", ["A3", "C4", "F4"], ["F5", "A5", "C6", "A5"]),
    "Dm": ("D2", ["A3", "D4", "F4"], ["D6", "F6", "A6", "F6"]),
    "Bb": ("Bb1", ["Bb3", "D4", "F4"], ["Bb5", "D6", "F6", "D6"]),
    "E": ("E2", ["G#3", "B3", "E4"], ["E5", "G#5", "B5", "G#5"]),
    "E7": ("E2", ["G#3", "D4", "E4"], ["E5", "G#5", "B5", "D6"]),
    "Bb5": ("Bb1", ["Bb3", "F4", "Bb4"], ["Bb5", "F6", "Bb6", "F6"]),
    "B5": ("B1", ["B3", "F#4", "B4"], ["B5", "F#6", "B6", "F#6"]),
    "C5": ("C2", ["C4", "G4", "C5"], ["C6", "G6", "C7", "G6"]),
}

# The theme, on trombones: DUN, DUN, DUN, da-DUNN - three times on A as the bass sinks under
# it, then up through F and Bb and down onto E. Only A and E fall on the beats of the first
# bars; the C is always a passing sixteenth.
DOOM_THEME = [
    (0, "A3", .5), (1, "A3", .5), (2, "A3", .5), (2.75, "C4", .25), (3, "E4", 1.0),
    (4, "E4", .5), (5, "E4", .5), (6, "E4", .5), (6.75, "C4", .25), (7, "A3", 1.0),
    (8, "A3", .5), (9, "A3", .5), (10, "A3", .5), (10.75, "C4", .25), (11, "E4", .5),
    (11.5, "G4", .5),
    (12, "F#4", .75), (12.75, "E4", .25), (13, "C4", .5), (13.5, "B3", .5), (14, "A3", 1.5),
    (15.5, "C4", .5),
    (16, "A3", .5), (17, "C4", .5), (18, "F4", 1.5), (19.5, "E4", .5),
    (20, "D4", 1.0), (21, "F4", .5), (21.5, "D4", .5), (22, "Bb3", 1.5), (23.5, "A3", .5),
    (24, "G#3", 1.0), (25, "B3", .5), (26, "E4", 1.0), (27, "B3", .5), (27.5, "D4", .5),
]

# The machine: one figure - up the chord, a step down, down again - stamped onto each chord
# of A G F E in turn, then an octave up and turned round; the last bar climbs E7 a note a beat.
DOOM_MACHINE = [
    (32, "A4", .5), (32.5, "C5", .5), (33, "E5", .5), (33.5, "D5", .5), (34, "C5", .5),
    (34.5, "B4", .5), (35, "A4", 1.0),
    (36, "G4", .5), (36.5, "B4", .5), (37, "D5", .5), (37.5, "C5", .5), (38, "B4", .5),
    (38.5, "A4", .5), (39, "G4", 1.0),
    (40, "F4", .5), (40.5, "A4", .5), (41, "C5", .5), (41.5, "Bb4", .5), (42, "A4", .5),
    (42.5, "G4", .5), (43, "F4", 1.0),
    (44, "E4", .5), (44.5, "G#4", .5), (45, "B4", .5), (45.5, "A4", .5), (46, "G#4", .5),
    (46.5, "F#4", .5), (47, "E4", .5), (47.5, "G#4", .5),
    (48, "E5", .5), (48.5, "A5", .5), (49, "C6", .5), (49.5, "B5", .5), (50, "A5", .5),
    (50.5, "G#5", .5), (51, "A5", .5), (51.5, "E5", .5),
    (52, "D5", .5), (52.5, "G5", .5), (53, "B5", .5), (53.5, "A5", .5), (54, "G5", .5),
    (54.5, "F#5", .5), (55, "G5", .5), (55.5, "D5", .5),
    (56, "C5", .5), (56.5, "F5", .5), (57, "A5", .5), (57.5, "G5", .5), (58, "F5", .5),
    (58.5, "E5", .5), (59, "D5", .5), (59.5, "F5", .5),
    (60, "E5", .5), (61, "G#5", .5), (62, "B5", .5), (63, "D6", .5),
]

# The overheat's siren: root, fifth, root, fifth, on each power chord as it climbs.
DOOM_SIREN = [(64 + 4 * bar + k, (root, fifth)[k % 2], 1.0)
              for bar, (root, fifth) in enumerate([("A5", "E6"), ("Bb5", "F6"), ("B5", "F#6"),
                                                   ("C6", "G6")])
              for k in xrange(4)]

# Home: the march's second half again, on F - Dm - E.
DOOM_HOME = [
    (80, "A3", .5), (81, "C4", .5), (82, "F4", 1.5), (83.5, "E4", .5),
    (84, "D4", 1.0), (85, "F4", .5), (85.5, "D4", .5), (86, "A3", 2.0),
    (88, "G#3", 1.0), (89, "B3", .5), (90, "E4", 1.0), (91, "B3", .5), (91.5, "D4", .5),
]

# The stop: dun, dun, DUNNN on E7, in beats from the top of its bar.
DOOM_STAB = [(0, .5), (.75, .5), (1.5, 1.75)]

# Where the machine lets off steam: (beat, length in beats).
DOOM_VENTS = [(14, 2.0), (30, 1.75), (46, 2.0), (62, 2.0), (78, 2.0), (94, 1.75)]

# The mix, measured the same way: the trombones and the organ, each the lead in its half, about
# 3 dB under the whole; the anvil 9, as the battle's clap is; the tuba 10 and the chug 11 (both
# discounted by the A curve, which hears little of their weight); the bleeps 18, the kick 20 and
# the pistons 27, under the hit sounds that own the top. The kick is kept off the anvil where
# it can be, because the two together were the loop's peak, and so set how loud it could be.
DOOM_MIX = {"tuba": 0.6, "chug": 0.44, "bone": 0.3, "organ": 0.28, "bleep": 0.1, "kick": 0.5,
            "anvil": 0.36, "piston": 0.28, "vent": 0.2, "crash": 0.13, "snare": 1.2, "tom": 0.7}


def _doom_part(bar):
    if bar < 8:
        return "march"
    if bar < 16:
        return "machine"
    return "overheat" if bar < 20 else "home"


def doombot_events():
    ev = []
    for bar in xrange(len(DOOM_CHART)):
        b = bar * 4
        if bar in DOOM_STOPS:
            continue
        part = _doom_part(bar)
        root, voicing, arp = DOOM_CHORDS[chord_at(DOOM_CHART, b)]

        if part in ("march", "home"):
            # The march: a tuba and a kick on every beat, leaning on one and three - heavy feet.
            for k in xrange(4):
                ev.append((b + k, tuba, root, 0.6, 1.0 if k % 2 == 0 else 0.75))
                ev.append((b + k, kick, "A1", 0.5, 1.0 if k % 2 == 0 else 0.7))
            for e in xrange(8):
                ev.append((b + e * 0.5, piston, "A7", 0.1, 1.0 if e % 2 else 0.6))
            if part == "march":
                # A held organ chord, so the sinking bass has something to sink under.
                for t in voicing:
                    ev.append((b, organ, t, 3.9, 0.45))
        else:
            # The machine: a chug on every eighth, the accents - an octave up - falling 3-3-2.
            for e in xrange(8):
                r = DOOM_CHORDS[chord_at(DOOM_CHART, b + e * 0.5)][0]
                accent = e in (0, 3, 6)
                ev.append((b + e * 0.5, chug, _oct(r, 1) if accent else r, 0.45,
                           1.0 if accent else 0.7))
            for s in xrange(16):
                c = DOOM_CHORDS[chord_at(DOOM_CHART, b + s * 0.25)]
                ev.append((b + s * 0.25, bleep, c[2][s % 4], 0.25, 1.0 if s % 4 == 0 else 0.7))
                ev.append((b + s * 0.25, piston, "A7", 0.1, (0.7, 0.45, 1.0, 0.45)[s % 4]))
            if part == "machine":
                # Off the anvil on three, or the two together set the loop's peak.
                kicks, stabs = (0, 1.5, 2.5), (0, 2.5)
            else:
                kicks, stabs = (0, 1, 2, 3), (0, 1.5, 3)
            for k in kicks:
                ev.append((b + k, kick, "A1", 0.5, 0.7 if k in (1, 3) else 1.0))
            for k in stabs:
                for t in DOOM_CHORDS[chord_at(DOOM_CHART, b + k)][1]:
                    ev.append((b + k, bone, t, 0.5, 0.6))
        ev.append((b + 1, anvil, "A5", 0.3, 1.0))
        ev.append((b + 3, anvil, "A5", 0.3, 1.0))
        if bar in (0, 8, 16, 20):
            ev.append((b, crash, "A6", 2.5, 1.0))

    for beat, pitch, dur in DOOM_THEME + DOOM_HOME:
        ev.append((beat, bone, pitch, dur * 0.9, 1.0))
        # Doubled an octave under for weight in the march; an octave over, on the organ, at home.
        ev.append((beat, bone, _oct(pitch, -1), dur * 0.9, 0.5))
        if beat >= 80:
            ev.append((beat, organ, _oct(pitch, 1), dur * 0.9, 0.6))
    for beat, pitch, dur in DOOM_MACHINE + DOOM_SIREN:
        ev.append((beat, organ, pitch, dur * 0.9, 1.0))
    for k in xrange(16):
        # The roll into home: the overheat's last bar, building.
        ev.append((76 + 0.25 * k, snare, "A4", 0.25, 0.15 + 0.03 * k))
    for at in (bar * 4 for bar in DOOM_STOPS):
        for off, length in DOOM_STAB:
            held = length > 1
            for t in DOOM_CHORDS["E7"][1]:
                ev.append((at + off, bone, t, length, 1.0 if held else 0.85))
            for t in ("B4", "E5"):
                ev.append((at + off, organ, t, length, 0.6))
            ev.append((at + off, tuba, "E2", length, 1.0))
            ev.append((at + off, kick, "A1", 0.5, 1.0))
        ev.append((at + 1.5, crash, "A6", 2.5, 1.0))
        ev.append((at + 3.5, tom, "E3", .5, .55))
        ev.append((at + 3.75, tom, "B2", .5, .65))
    for at, length in DOOM_VENTS:
        ev.append((at, vent, "A4", length, 1.0))
    return _mixed(ev, DOOM_MIX)


# ---------------------------------------------------------------------------
# victory: the sting when a story's fight is won. A one-shot, about three seconds, 150 BPM.
#
# A trumpet runs up the A chord in a triplet - E A C# - tops out on E, and then the cartoon
# cadence: F, G, A (bVI - bVII - I), one 'bah' each, the last held with a timpani, a cymbal and
# a glockenspiel running up two octaves of A major. bVI - bVII - I is what every cartoon
# finale lands with; the C and G it borrows make the A that follows sound twice as major. The
# glock is the story theme's, the brass the battle's: the sting is where the two meet.
# ---------------------------------------------------------------------------

VICTORY_BPM = 150.0
VICTORY_BEATS = 8                       # 3.2 s, the last 1.0 s of it ringing out

VICTORY_LEAD = [(0, "E5", 1 / 3.0), (1 / 3.0, "A5", 1 / 3.0), (2 / 3.0, "C#6", 1 / 3.0),
                (1, "E6", .5), (1.5, "C#6", .5), (2, "C6", .5), (2.5, "D6", .5), (3, "E6", 2.5)]
# (beat, length, brass voicing, bass)
VICTORY_CHORDS = [(1, .5, ["A4", "C#5", "E5"], "A2"), (2, .5, ["A4", "C5", "F5"], "F2"),
                  (2.5, .5, ["B4", "D5", "G5"], "G2"), (3, 2.5, ["A4", "C#5", "E5", "A5"], "A2")]
VICTORY_RUN = ["A5", "C#6", "E6", "A6", "C#7", "E7", "A7"]

# The trumpet 2.5 dB under the whole, the horns 5, the glock 8; the timpani and the tuba are
# felt more than heard. The peak is the last chord's first instant, as a sting's should be.
VICTORY_MIX = {"trumpet": 0.42, "horn": 0.2, "tuba": 0.5, "timpani": 0.45, "snare": 1.0,
               "crash": 0.2, "glock": 0.3}


def victory_events():
    ev = []
    for beat, pitch, dur in VICTORY_LEAD:
        ev.append((beat, trumpet, pitch, dur * 0.92, 1.0))
    for beat, length, voicing, low in VICTORY_CHORDS:
        for t in voicing:
            ev.append((beat, horn, t, length * 0.92, 1.0))
        ev.append((beat, tuba, low, length * 0.92, 1.0))
    for k in xrange(16):
        # A snare roll under F and G, swelling into the last chord.
        ev.append((2 + k / 16.0, snare, "A4", 0.25, 0.15 + 0.035 * k))
    ev.append((1, timpani, "E2", 1.0, 0.6))
    ev.append((3, timpani, "A2", 1.0, 1.0))
    ev.append((3, crash, "A6", 4.0, 1.0))
    for k, pitch in enumerate(VICTORY_RUN):
        ev.append((3 + 0.25 * k, glock, pitch, 1.0, 0.7 + 0.05 * k))
    return _mixed(ev, VICTORY_MIX)


# ---------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------

TRACKS = [
    # name, events, bpm, beats, reverb mix, peak dBFS, loops. Every piece has the same peak, so
    # MusicPlayer.VolumeDb stays the one number that balances music against effects.
    ("menu", menu_events, MENU_BPM, MENU_BEATS, 0.2, -3.0, True),
    ("battle", battle_events, BATTLE_BPM, BATTLE_BEATS, 0.12, -3.0, True),
    ("story", story_events, STORY_BPM, STORY_BEATS, 0.22, -3.0, True),
    ("doombot", doombot_events, DOOM_BPM, DOOM_BEATS, 0.14, -3.0, True),
    ("victory", victory_events, VICTORY_BPM, VICTORY_BEATS, 0.2, -3.0, False),
]


def build_once(path, fn, bpm, beats, reverb_mix, target_db):
    """A one-shot: no wrapping and no loop chunk, so Godot plays it once. It starts from silence,
    so the filters can start cold; it ends faded, so its tail is not cut."""
    buf = S.reverb(render_once(fn(), bpm, beats), size=0.8, damp=0.45, mix=reverb_mix)
    buf = S.normalize(S.fade(S.dc_block(buf), 0.0004, 0.3), 10.0 ** (target_db / 20.0))
    return S.write_wav(path, S.halve_rate(buf), sr=S.SR // 2)


def build_all(out_dir, only=None):
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    for name, fn, bpm, beats, reverb_mix, target_db, loops in TRACKS:
        if only and name not in only:
            continue
        path = os.path.join(out_dir, name + ".wav")
        if not loops:
            pk, rm, secs, clipped = build_once(path, fn, bpm, beats, reverb_mix, target_db)
            print("music %-7s %5.1f s at %3.0f BPM  peak %5.1f dBFS  rms %5.1f dBFS  %4.1f MB  once%s"
                  % (name, secs, bpm, pk, rm, os.path.getsize(path) / 1048576.0,
                     "  CLIPPED x%d" % clipped if clipped else ""))
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
        pk, rm, secs, clipped = S.write_wav_loop(path, halved, sr=S.SR // 2)
        print("music %-7s %5.1f s at %3.0f BPM  peak %5.1f dBFS  rms %5.1f dBFS  %4.1f MB%s"
              % (name, secs, bpm, pk, rm, os.path.getsize(path) / 1048576.0,
                 "  CLIPPED x%d" % clipped if clipped else ""))


if __name__ == "__main__":
    build_all(os.path.join(HERE, "..", "..", "assets", "music"), sys.argv[1:])
