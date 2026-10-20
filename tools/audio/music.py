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

Most of it is cartoon music on purpose: Eric's call on 2026-10-04 was "something more fun". On
2026-10-18 the battle music went action and the menu went chill ("less like annoying whistling
music"), and every stage got its own battle loop: the Fridge Door an action-rock theme, Open
Plains a galloping adventure, City Rooftops night-time synthwave, the Big Show guitar rock (on
2026-10-20: plucked-string electric guitars through a clipping amp), the Playground the old
bouncy ska romp, the Building Site industrial, the Kitchen Counter funk with pots and pans. The menu is lo-fi: electric piano, a vibraphone tune, soft drums. The story theme is a
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


def epiano(freq, dur):
    """The menu's electric piano: a sine for the tine's note, a little of its octave and twelfth,
    and a bell partial far up that dies in a moment - the soft 'tink' on the front of a Rhodes -
    with a slow tremolo. Warm, rounded, nothing whistling. Rings out over a fixed time, so a
    held chord and a short one decay alike, and lets go when the hand comes up."""
    ring = 2.4
    v = S.partials(freq, [1.0, 2.0, 3.0, 14.0], ring, amps=[1.0, 0.22, 0.05, 0.04],
                   decays=[2.6, 5.0, 8.0, 60.0])
    v = S.lowpass(S.tremolo(v, 4.2, 0.16), 2800, 0.7)
    n = len(v)
    k = min(n, S.n_samples(dur))
    r = min(n - k, S.n_samples(0.18))
    env = S.ramp(min(k, S.n_samples(0.004)), 0.0, 1.0)
    env += [1.0] * (k - len(env)) + S.ramp(r, 1.0, 0.0) + [0.0] * (n - k - r)
    return S.env_mul(v, env)


def vibe(freq, dur):
    """The menu tune, on a vibraphone: struck aluminium bars - the note, and the bar's overtone two
    octaves up, which fades first - with the motor's slow wobble and a soft yarn mallet. Mellow
    where the old whistle was piercing."""
    ring = 2.0
    v = S.partials(freq, [1.0, 4.0, 10.0], ring, amps=[1.0, 0.18, 0.03], decays=[3.0, 9.0, 30.0])
    v = S.tremolo(v, 5.0, 0.28)
    mallet = S.lowpass(S.osc("noise", 0, 0.006, seed=245), 1800, 0.8)
    v = S.mix(v, S.gain(S.env_mul(mallet, S.expdecay(len(mallet), 3.0)), 0.12))
    n = len(v)
    k = min(n, S.n_samples(dur + 0.25))
    r = min(n - k, S.n_samples(0.3))
    return S.env_mul(v, [1.0] * k + S.ramp(r, 1.0, 0.0) + [0.0] * (n - k - r))


def softkick(freq, dur):
    """A soft, round kick - a felt beater on a small drum. Felt more than heard, so the menu can
    sit on a beat for minutes without it ever thumping."""
    d = 0.32
    n = S.n_samples(d)
    body = S.env_mul(S.osc("sine", S.sweep(d, 120, 48, 0.35), d), S.expdecay(n, 9.0))
    return S.lowpass(body, 500, 0.7)


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


def lead(freq, dur):
    """The battle lead: two saws a hair apart and a square an octave under them, driven - the
    synth that plays the hero's theme - scooping into each note and singing with a late
    vibrato on the long ones. The filter keeps it under the hit sounds, which own the top end."""
    n = S.n_samples(dur)
    f = _vibrato(_glide(freq, n, 0.985, 0.03), 5.5, 0.008, 0.25)
    ph = _phase(freq)
    v = S.mix(S.osc("saw", f, dur, phase=ph),
              S.gain(S.osc("saw", [x * 1.006 for x in f], dur, phase=(ph + 0.5) % 1.0), 0.9),
              S.gain(S.osc("square", [x * 0.5 for x in f], dur, phase=ph), 0.3))
    v = S.lowpass(v, 2600, 1.0)
    return S.env_mul(S.drive(v, 1.8), S.adsr(dur, 0.008, 0.1, 0.8, min(0.06, dur * 0.3)))


def drivebass(freq, dur):
    """The battle bass: saw, square and sine, driven, the filter closing on every note - eighths
    of it under the guitar are the engine of the whole track. Its overtones carry it on a TV."""
    ph = _phase(freq)
    v = S.mix(S.gain(S.osc("saw", freq, dur, phase=ph), 0.6),
              S.gain(S.osc("square", freq, dur, phase=ph), 0.35),
              S.gain(S.osc("sine", freq, dur, phase=ph), 0.5))
    v = S.lowpass(v, S.sweep(dur, 1500, 450, 0.5), 1.1)
    return S.drive(S.env_mul(v, S.adsr(dur, 0.006, 0.08, 0.7, min(0.03, dur * 0.3))), 2.0)


# --- the stage bands -------------------------------------------------------
# Every stage has its own battle music (Eric, 2026-10-18), so a few more instruments: the
# electric guitars of the Big Show, the steel strings of Open Plains, the synth arpeggio of City
# Rooftops at night and the pots and pans of the Kitchen Counter.

# The Big Show's guitars (Eric, 2026-10-20: "more rock focused ... something with guitar").
# An electric guitar is three things, and each is built the way the real one works: a plucked
# string (Karplus-Strong, as in _pluck, but with a delay that can change length, so a note can
# bend and sing), an amp turned up until it clips, and a speaker cabinet that cannot play much
# above 5 kHz. The cabinet is what turns the clipping from fizz into a guitar.

def _string(freqs, n, bright, t60, seed, pick=0.13):
    """One plucked string, n samples long. freqs is the pitch, per sample, so a note can bend.
    pick is where along the string it is picked: near the bridge, which hollows out a few
    harmonics the way a real pick does."""
    f0 = freqs[0]
    whole = int(S.SR / f0)
    rng = random.Random(seed)
    burst = [rng.uniform(-1.0, 1.0) for _ in xrange(whole + 2)]
    d = max(1, int(whole * pick))
    burst = S.lowpass([burst[i] - (burst[i - d] if i >= d else 0.0) for i in xrange(len(burst))],
                      bright, 0.7)
    mean = sum(burst) / len(burst)
    burst = [v - mean for v in burst]
    nb = len(burst)
    g = 0.5 * 0.001 ** (1.0 / (t60 * f0))
    sr = float(S.SR)
    y = [0.0] * n
    for i in xrange(n):
        v = burst[i] if i < nb else 0.0
        # Read the string one period back, between samples; the two-point average is the
        # string losing its top end each time round, and adds the half sample taken off here.
        p = i - (sr / freqs[i] - 0.5)
        j = int(p)
        if j >= 1:
            fr = p - j
            a = y[j] + fr * (y[j + 1] - y[j])
            b = y[j - 1] + fr * (y[j] - y[j - 1])
            v += g * (a + b)
        y[i] = v
    return y


def _amp(v, gain, cab, low=90.0):
    """The amp and its cabinet. The pickup rolls the top off before the clipping, so it clips
    on the notes rather than on hiss; a bias makes it clip harder one way than the other, the
    way a valve does, which adds the even harmonics that make distortion sound warm; then two
    filters for the speaker cone, and a high-pass that takes the bias's thump back out."""
    v = S.normalize(S.lowpass(S.highpass(v, 110.0), 3400.0, 0.8), 1.0)
    bias = 0.3
    t0 = math.tanh(bias)
    v = [math.tanh(gain * x + bias) - t0 for x in v]
    v = S.lowpass(S.lowpass(v, cab, 1.1), cab * 1.4, 0.7)
    return S.highpass(v, low)


def _cents(c):
    return 2.0 ** (c / 1200.0)


def _powerchord(freq, dur, mute, take):
    """A power chord - root, fifth and octave, no third - on three strings struck down-stroke,
    low string first, then put through the amp. Open, it rings for as long as it is held; palm
    muted, the heel of the hand damps it to a short dark chug. take picks the strings slightly
    differently, so two takes laid together sound like two guitars, not one twice as loud."""
    hold = dur + 0.03
    n = S.n_samples(hold)
    seed = int(freq * 7) + 3 * take + (50 if mute else 0)
    detune = ((0.0, 3.0, -2.0), (2.5, -3.5, 4.0))[take]
    spread = (0.006, 0.0085)[take]
    v = [0.0] * n
    for k, (ratio, level) in enumerate(((1.0, 1.0), (1.4983, 0.85), (2.0, 0.6))):
        off = S.n_samples(k * spread)
        f = freq * ratio * _cents(detune[k])
        s = _string([f] * (n - off), n - off, 2400.0 if mute else 5200.0,
                    0.16 if mute else 3.0, seed + k)
        for i in xrange(n - off):
            v[off + i] += s[i] * level
    if mute:
        v = _amp(v, 7.0, 1700.0)
        env = S.ramp(S.n_samples(0.002), 0.0, 1.0)
        return S.env_mul(v, env + S.decay(n - len(env), 1.6))
    v = _amp(v, 16.0, 2700.0)
    ka = S.n_samples(0.002)
    kr = min(n - ka, S.n_samples(0.03))
    # The end is a hand coming down on the strings, not a fade: quick, but not a cut.
    return S.env_mul(v, S.ramp(ka, 0.0, 1.0) + [1.0] * (n - ka - kr) + S.ramp(kr, 1.0, 0.0))


def _doubled(freq, dur, mute):
    """Rhythm guitar is played twice and the takes laid together - the oldest trick in rock. The
    second is a few milliseconds behind the first and a few cents away, so they thicken each
    other instead of adding up to the same guitar louder."""
    a = _powerchord(freq, dur, mute, 0)
    b = _powerchord(freq, dur, mute, 1)
    lag = S.n_samples(0.007)
    out = a + [0.0] * lag
    for i in xrange(len(b)):
        out[lag + i] += b[i] * 0.8
    return out


def gtropen(freq, dur):
    """The Big Show's rhythm guitars, open: a power chord struck and left to ring."""
    return _doubled(freq, dur, False)


def gtrmute(freq, dur):
    """The Big Show's rhythm guitars, palm-muted: the chug the riff is built on."""
    return _doubled(freq, dur, True)


def _leadgtr(freq, dur, bend):
    """The lead guitar: one string, the amp turned further up so a note sustains, a pick scraping
    into each note, and a wide, slow vibrato once a note is held - a guitarist's, from the
    wrist. bend is how many semitones the note is pushed up into from below."""
    hold = dur + 0.04
    n = S.n_samples(hold)
    start = freq * 2.0 ** (-bend / 12.0) if bend else freq * 0.99
    k = min(n, S.n_samples(0.11 if bend else 0.015))
    f = S.ramp(k, start, freq, 0.6) + [freq] * (n - k)
    f = _vibrato(f, 5.2, 0.011, 0.18 if not bend else 0.22)
    v = _string(f, n, 6000.0, 4.0, int(freq * 3) + 17, pick=0.2)
    v = _amp(v, 22.0, 3200.0, low=180.0)
    ka = S.n_samples(0.003)
    kr = min(n - ka, S.n_samples(0.04))
    return S.env_mul(v, S.ramp(ka, 0.0, 1.0) + [1.0] * (n - ka - kr) + S.ramp(kr, 1.0, 0.0))


def gtrlead(freq, dur):
    return _leadgtr(freq, dur, 0)


def gtrbend(freq, dur):
    """A note bent up a whole step into pitch - the sound of a guitar solo more than any other."""
    return _leadgtr(freq, dur, 2)


def bassgtr(freq, dur):
    """The bass guitar, played with a pick: a plucked string, its overtones carrying it on a TV
    speaker as every bass here does, a little drive, and the hand damping it on time so the
    eighths stay locked to the kick instead of blurring into a drone."""
    n = S.n_samples(dur + 0.02)
    v = _string([freq] * n, n, 2600.0, 1.6, int(freq * 5) + 29, pick=0.2)
    v = S.drive(S.normalize(S.lowpass(v, S.sweep(dur + 0.02, 2200, 900, 0.5), 0.9), 1.0), 1.8)
    ka = S.n_samples(0.002)
    kr = min(n - ka, S.n_samples(0.025))
    return S.env_mul(v, S.ramp(ka, 0.0, 1.0) + [1.0] * (n - ka - kr) + S.ramp(kr, 1.0, 0.0))


def rocksnare(freq, dur):
    """The Big Show's snare: the kit's snare, hit harder, through the drive. A bare snare is
    one tall spike of noise, and in a track this dense that spike set the peak the whole loop is
    scaled to; driven, the backbeat is fat instead of spiky, and the rest can come up."""
    return S.drive(S.normalize(snare(freq, dur), 1.0), 3.0)


def ride(freq, dur):
    """A ride cymbal for the choruses: a bell 'ping' of metal partials over a long, soft wash."""
    d = 0.7
    n = S.n_samples(d)
    ping = S.partials(3150.0, [1.0, 1.43, 1.97, 2.61], d, amps=[0.6, 0.5, 0.35, 0.25],
                      decays=[8.0, 10.0, 12.0, 14.0])
    wash = S.env_mul(S.highpass(S.osc("noise", 0, d, seed=271), 6500), S.expdecay(n, 5.0))
    return S.mix(S.gain(ping, 0.3), S.gain(wash, 0.6))


def steel(freq, dur):
    """Open Plains' guitar: a steel string, brighter and longer than the old ukulele's nylon."""
    return _pluck(freq, dur, 4200.0, 1.4, int(freq * 10) + 7)


def arp(freq, dur):
    """City Rooftops at night: a synth pluck, a saw and a square, the filter snapping shut at
    once - sixteenths of it running round the chord are the city's lights going by."""
    d = 0.16
    ph = _phase(freq)
    v = S.mix(S.osc("saw", freq, d, phase=ph), S.gain(S.osc("square", freq * 1.003, d, phase=ph), 0.5))
    v = S.lowpass(v, S.sweep(d, 3400, 500, 0.35), 1.6)
    return S.env_mul(v, S.ad(d, 0.002, 2.6))


def pan(freq, dur):
    """A saucepan struck with a wooden spoon: a clang of metal partials that are nowhere near
    harmonic, over the knock of the spoon. The Kitchen Counter's cowbell."""
    d = 0.35
    n = S.n_samples(d)
    ring = S.partials(freq, [1.0, 1.59, 2.14, 2.92], d, amps=[1.0, 0.7, 0.5, 0.3],
                      decays=[9.0, 12.0, 16.0, 22.0])
    knock = S.env_mul(S.bandpass(S.osc("noise", 0, d, seed=261), 900, 1.2), S.expdecay(n, 40.0))
    return S.drive(S.mix(S.gain(ring, 0.6), S.gain(knock, 0.6)), 1.4)


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
# menu: chill, 88 BPM, lightly swung, sixteen bars - A, then B (Eric, 2026-10-18: "the menus
# can still be chill, but less like annoying whistling music").
#
# Lo-fi: an electric piano laying down seventh chords, a vibraphone tune with room around it, an
# upright bass, and a soft kick, a snap and a shaker. A goes Amaj7 - F#m7 - Bm7 - E9 twice (the
# second time through Dmaj7); B is the bridge, Dmaj7 - C#m7 - Bm7 - E9, whose E9 pulls into the
# Amaj7 at the top. It plays for minutes on the menus, so nothing in it is bright or busy.
# ---------------------------------------------------------------------------

MENU_BPM = 88.0
MENU_SWING = 0.6        # where an off-beat eighth lands: 0.5 is straight, 0.667 a full shuffle

MENU_CHART = ["Amaj7", "F#m7", "Bm7", "E9", "Amaj7", "F#m7", "Dmaj7", "E9",
              "Dmaj7", "C#m7", "Bm7", "E9", "Dmaj7", "C#m7", "Bm7", "E9"]
MENU_BEATS = 4 * len(MENU_CHART)     # 64 beats, 43.6 s

# Rootless voicings round middle C, the way a pianist plays them over a bass: each moves to the
# next by the smallest step it can.
KEYS = {
    "Amaj7": ["C#4", "E4", "G#4", "B4"],
    "F#m7": ["C#4", "E4", "F#4", "A4"],
    "Bm7": ["D4", "F#4", "A4", "B4"],
    "E9": ["D4", "F#4", "G#4", "B4"],
    "Dmaj7": ["C#4", "E4", "F#4", "A4"],
    "C#m7": ["B3", "C#4", "E4", "G#4"],
}

# The bass: the root on one, the fifth on the and of two - laid back, not walking.
MENU_BASS = {
    "Amaj7": ("A1", "E2"), "F#m7": ("F#1", "C#2"), "Bm7": ("B1", "F#2"), "E9": ("E2", "B1"),
    "Dmaj7": ("D2", "A1"), "C#m7": ("C#2", "G#1"),
}

# The vibraphone tune, in straight beats; the swing is applied as it is played. Chord tones on
# the strong beats, and it leaves space: a phrase, then a breath.
MENU_TUNE = [
    (0.5, "E5", .5), (1, "C#5", 1), (2, "E5", .5), (2.5, "G#5", 1.5),
    (4, "A5", 1.5), (5.5, "F#5", .5), (6, "E5", 2),
    (8.5, "D5", .5), (9, "F#5", 1), (10, "A5", 1.5), (11.5, "B5", .5),
    (12, "G#5", 2), (14, "D5", 1), (15, "B4", 1),
    (16.5, "E5", .5), (17, "C#5", 1), (18, "E5", .5), (18.5, "G#5", 1.5),
    (20, "C#6", 1.5), (21.5, "A5", .5), (22, "F#5", 2),
    (24, "F#5", 1), (25, "A5", 1), (26, "C#6", 2),
    (28, "B5", 1.5), (29.5, "G#5", .5), (30, "E5", 2),
    (32, "A5", 3), (35, "F#5", .5), (35.5, "A5", .5),
    (36, "G#5", 2), (38, "E5", 1), (39, "C#5", 1),
    (40, "D5", 1), (41, "F#5", 1), (42, "A5", 1.5), (43.5, "B5", .5),
    (44, "G#5", 3),
    (48.5, "F#5", .5), (49, "A5", .5), (49.5, "C#6", 1.5), (51, "A5", 1),
    (52, "B5", 1), (53, "G#5", 1), (54, "E5", 2),
    (56, "F#5", 1.5), (57.5, "D5", .5), (58, "B4", 2),
    (60, "D5", 1), (61, "E5", 1), (62, "G#5", 1), (63, "B5", 1),
]


# The mix, as in sfx.py's REGISTRY: one level per part, and the events carry only accents. Set by
# measuring each part alone, above 300 Hz, against the whole while it plays: the vibraphone - the tune
# - within 2 dB of the whole, the piano 4.5 under, the rest 15 and more. The
# vibraphone is the tune, so it sits on top; the piano under it; the drums felt, not heard.
MENU_MIX = {"epiano": 0.3, "vibe": 0.5, "upright": 0.72, "softkick": 0.55, "snap": 0.7,
            "shaker": 0.14}


def _mixed(events, levels):
    return [(b, inst, p, d, v * levels[inst.__name__]) for b, inst, p, d, v in events]


def swing(beat, amount=MENU_SWING):
    """Where a straight beat lands once the off-beat eighths are pushed late."""
    whole = math.floor(beat)
    return whole + amount if abs(beat - whole - 0.5) < 1e-6 else beat


def menu_events():
    ev = []
    for bar, chord in enumerate(MENU_CHART):
        b = bar * 4
        # The piano: the chord on one, held, and again softly on the and of two.
        for at, hold, vel in ((b, 2.3, 0.9), (swing(b + 2.5), 1.3, 0.6)):
            for s, t in enumerate(KEYS[chord]):
                ev.append((at + s * 0.012, epiano, t, hold, vel))
        root, fifth = MENU_BASS[chord]
        ev.append((b, upright, root, 1.4, 1.0))
        ev.append((swing(b + 2.5), upright, fifth, 1.0, 0.85))
        ev.append((b, softkick, None, 0.3, 1.0))
        ev.append((swing(b + 2.5), softkick, None, 0.3, 0.7))
        ev.append((b + 1.0, snap, None, 0.3, 1.0))
        ev.append((b + 3.0, snap, None, 0.3, 1.0))
        for e in xrange(8):
            ev.append((swing(b + e * 0.5), shaker, None, 0.08, 1.0 if e % 2 else 0.6))
    for beat, pitch, dur in MENU_TUNE:
        start = swing(beat)
        ev.append((start, vibe, pitch, swing(beat + dur) - start, 1.0))
    return _mixed(ev, MENU_MIX)


# ---------------------------------------------------------------------------
# battle: action, 152 BPM, twenty-four bars, in A (Eric, 2026-10-18: "more like actiony battle").
#
#   intro (4)  the riff: power chords chugging eighths over a rock beat, A - A - F - G
#   A     (8)  the hero's theme on the lead, A - F - G - A twice, the second time home by E
#   B     (8)  the theme soars, D - F - G - A, the drums drive four on the floor and the brass
#              stabs a rhythm under it; ends on E
#   build (4)  half time on E, then F and G climbing under a snare roll, back into the A
#
# The guitar plays power chords - root and fifth, no third - so FIGHT!'s A major chord lands on
# it cleanly, and the tune can lean minor (C, F, G - the action-movie sound) without a clash.
# The hit sounds are louder than all of it and own the top end, so the lead's edge is filtered.
# ---------------------------------------------------------------------------

BATTLE_BPM = 152.0
BATTLE_CHART = (["A5", "A5", "F5", "G5"]
                + ["A5", "F5", "G5", "A5", "A5", "F5", "G5", "E5"]
                + ["D5", "F5", "G5", "A5", "D5", "F5", "G5", "E5"]
                + ["E5", "E5", "F5", "G5"])
BATTLE_BEATS = 4 * len(BATTLE_CHART)      # 96 beats, 37.9 s

# chord: (the guitar's root, the bass's root, the brass stab's voicing)
BATTLE_CHORDS = {
    "A5": ("A2", "A1", ["A4", "C5", "E5"]),
    "D5": ("D3", "D2", ["D4", "F4", "A4"]),
    "E5": ("E2", "E1", ["E4", "G#4", "B4"]),
    "F5": ("F2", "F1", ["F4", "A4", "C5"]),
    "G5": ("G2", "G1", ["G4", "B4", "D5"]),
}

# The hero's theme (section A), from the section's first beat.
THEME = [
    (0, "A4", .5), (.5, "C5", .5), (1, "E5", 1), (2, "A5", 1.5), (3.5, "G5", .5),
    (4, "F5", 1.5), (5.5, "E5", .5), (6, "C5", 1), (7, "A4", 1),
    (8, "G5", 1), (9, "D5", .5), (9.5, "E5", .5), (10, "G5", 1), (11, "B4", 1),
    (12, "A4", 3), (15, "E5", .5), (15.5, "G5", .5),
    (16, "A4", .5), (16.5, "C5", .5), (17, "E5", 1), (18, "A5", 1), (19, "C6", 1),
    (20, "A5", 1), (21, "F5", 1), (22, "C5", .5), (22.5, "F5", .5), (23, "A5", 1),
    (24, "B5", 1), (25, "G5", 1), (26, "D5", .5), (26.5, "G5", .5), (27, "B5", 1),
    (28, "B5", 2), (30, "E5", 2),
]

# The theme soaring (section B): long notes, each landing on the chord's top.
SOAR = [
    (0, "A5", 2), (2, "F5", 1), (3, "D5", 1),
    (4, "C6", 2), (6, "A5", 1), (7, "F5", 1),
    (8, "D6", 2), (10, "B5", 1), (11, "G5", 1),
    (12, "C6", 1.5), (13.5, "B5", .5), (14, "A5", 2),
    (16, "A5", 1), (17, "D6", 1), (18, "C6", 1), (19, "A5", 1),
    (20, "C6", 2), (22, "F6", 1), (23, "C6", 1),
    (24, "D6", 1.5), (25.5, "C6", .5), (26, "B5", 1), (27, "G5", 1),
    (28, "B5", 2), (30, "E5", 2),
]

# The brass stabs in B: a short chord on one, the and of two and the and of four.
STABS = [0.0, 1.5, 3.5]

BATTLE_MIX = {"lead": 0.3, "horn": 0.14, "chug": 0.48, "drivebass": 0.42, "kick": 0.62,
              "snare": 1.0, "hat": 0.2, "crash": 0.13, "tom": 0.7}


def _battle_part(bar):
    if bar < 4:
        return "intro"
    if bar < 12:
        return "A"
    if bar < 20:
        return "B"
    return "build"


def battle_events():
    ev = []
    for bar, chord in enumerate(BATTLE_CHART):
        b = bar * 4
        part = _battle_part(bar)
        guitar, root, stab = BATTLE_CHORDS[chord]
        half_time = part == "build" and bar < 22

        # The guitar: eighths, the accents falling 3-3-2 (one, the and of two, four), which is
        # what makes a chug drive. The first two bars of the intro ring a chord before they start.
        for e in xrange(8):
            at = b + e * 0.5
            if bar < 2 and e < 4:
                if e == 0:
                    ev.append((at, chug, guitar, 1.9, 1.0))
                continue
            accent = e in (0, 3, 6)
            ev.append((at, chug, guitar, 0.45, 1.0 if accent else 0.62))

        # The bass doubles the guitar's eighths an octave down, up an octave on the last one.
        for e in xrange(8):
            pitch = _oct(root, 1) if e == 7 else root
            ev.append((b + e * 0.5, drivebass, pitch, 0.45, 1.0 if e % 2 == 0 else 0.8))

        # The drums.
        if half_time:
            ev.append((b, kick, None, 0.3, 1.0))
            ev.append((b + 2, snare, None, 0.3, 1.0))
            for q in xrange(4):
                ev.append((b + q, hat, None, 0.1, 0.8))
        elif part == "B":
            for q in xrange(4):
                ev.append((b + q, kick, None, 0.3, 1.0))
            ev.append((b + 1, snare, None, 0.3, 1.0))
            ev.append((b + 3, snare, None, 0.3, 1.0))
            for e in xrange(8):
                ev.append((b + e * 0.5, hat, None, 0.1, 1.0 if e % 2 else 0.75))
        else:
            for at in (0.0, 2.0, 2.5):
                ev.append((b + at, kick, None, 0.3, 1.0 if at != 2.5 else 0.8))
            ev.append((b + 1, snare, None, 0.3, 1.0))
            ev.append((b + 3, snare, None, 0.3, 1.0))
            for e in xrange(8):
                ev.append((b + e * 0.5, hat, None, 0.1, 0.9 if e % 2 else 0.6))

        # A crash at the top of every section, and of the B section's second half.
        if bar in (0, 4, 12, 16, 20):
            ev.append((b, crash, None, 1.5, 1.0))

        # The brass stabs under the soaring theme.
        if part == "B":
            for at in STABS:
                for t in stab:
                    ev.append((b + at, horn, t, 0.35, 1.0))

    # The tunes.
    for beat, pitch, dur in THEME:
        ev.append((16 + beat, lead, pitch, dur * 0.92, 1.0))
    for beat, pitch, dur in SOAR:
        ev.append((48 + beat, lead, pitch, dur * 0.92, 1.0))

    # The build: the brass swell up on F and G, a snare roll getting thicker, toms falling into
    # the top of the loop.
    for t in BATTLE_CHORDS["F5"][2]:
        ev.append((88, horn, t, 3.8, 0.8))
    for t in BATTLE_CHORDS["G5"][2]:
        ev.append((92, horn, t, 3.4, 1.0))
    for k in xrange(8):
        ev.append((88 + k * 0.5, snare, None, 0.2, 0.45 + 0.04 * k))
    for k in xrange(12):
        ev.append((92 + k * 0.25, snare, None, 0.15, 0.6 + 0.03 * k))
    for k, (at, pitch) in enumerate(((95.0, "G2"), (95.25, "E2"), (95.5, "D2"), (95.75, "A1"))):
        ev.append((at, tom, pitch, 0.3, 0.9))
    for k in xrange(4):
        ev.append((84 + 3 + k * 0.25, tom, ("B2", "A2", "G2", "E2")[k], 0.3, 0.8))
    return _mixed(ev, BATTLE_MIX)


# ---------------------------------------------------------------------------
# The Playground: a bouncy ska romp, 164 BPM, twenty-eight bars. It was the battle music for
# every stage until 2026-10-18; it suits the playground, where the fighting is a game.
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

SKA_BPM = 164.0
SKA_CHART = (["A", "A", "D", "E", "A", "A", "D", "E7"]
                + ["A", "F#7", "B7", "E7"] * 2
                + ["A", "A", "D", "E", "A", "A", "B7", "B7"]
                + ["E", "E", "G", "D"])
SKA_BEATS = 4 * len(SKA_CHART)      # 112 beats, 41.0 s

# chord: (bass root, the offbeat chop's voicing). Round the circle the chop moves by half steps
# (A4 to A#4 and back, D#4 to D4 to C#4), which is where its cartoon slither comes from.
SKA_CHORDS = {
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
SKA_WALK = [
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
SKA_MIX = {"chip": 0.4, "chipslide": 0.4, "horn": 0.23, "skank": 0.23, "bass": 0.52,
              "bassdrop": 0.52, "bassfall": 0.52, "kick": 0.43, "clap": 1.0, "hat": 0.19,
              "block": 0.16, "crash": 0.13, "tom": 0.75, "snare": 1.0, "slidewhistle": 0.22}


def _ska_part(bar):
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


def playground_events():
    ev = []
    for bar, chord in enumerate(SKA_CHART):
        b = bar * 4
        part = _ska_part(bar)
        root, chop = SKA_CHORDS[chord]

        if part != "break":
            for off in (0.5, 1.5, 2.5, 3.5):
                if b + off not in NO_CHOP:
                    for t in chop:
                        ev.append((b + off, skank, t, 0.35, 1.0))

        if part == "B":
            for k, p in enumerate(SKA_WALK[bar % 4]):
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
        for t in SKA_CHORDS["E"][1]:
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
    return _mixed(ev, SKA_MIX)


# ---------------------------------------------------------------------------
# The other stages' battle music (Eric, 2026-10-18: "the battle theme should be different for
# every stage, use a tune that is fitting for each stage"). The Fridge Door plays the battle
# above; the Playground plays the bouncy ska romp below it; each of the rest has its own.
# ---------------------------------------------------------------------------

_NAMES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]


def _shift(name, semis):
    """A note moved by semitones, spelt with sharps."""
    i = len(name)
    while i > 0 and name[i - 1].isdigit():
        i -= 1
    k = _NAMES.index(name[:i]) if name[:i] in _NAMES else (_PITCH_CLASS[name[:i]] + 9) % 12
    total = int(name[i:]) * 12 + k + semis
    return _NAMES[total % 12] + str(total // 12)


def _tune(ev, notes, at, instrument, vel=1.0, held=0.92):
    for beat, pitch, dur in notes:
        ev.append((at + beat, instrument, pitch, dur * held, vel))


def _rock_drums(ev, b, kicks=(0.0, 2.0, 2.5), snares=(1.0, 3.0), hats=8, hat_vel=(0.6, 0.9)):
    for k in kicks:
        ev.append((b + k, kick, None, 0.3, 1.0 if k == int(k) else 0.8))
    for s in snares:
        ev.append((b + s, snare, None, 0.3, 1.0))
    step = 4.0 / hats
    for h in xrange(hats):
        ev.append((b + h * step, hat, None, 0.08, hat_vel[1] if h % 2 else hat_vel[0]))


# ---------------------------------------------------------------------------
# Open Plains: an adventure, 138 BPM, A major. A galloping steel-string strum, a march on the
# drums, and the tune on brass - a hero riding out across the grass.
# ---------------------------------------------------------------------------

PLAINS_BPM = 138.0
PLAINS_CHART = (["A", "A", "D", "E"]
                + ["A", "D", "A", "E", "F#m", "D", "E", "A"]
                + ["D", "E", "C#m", "F#m", "D", "Bm", "E", "E"])
PLAINS_BEATS = 4 * len(PLAINS_CHART)     # 80 beats, 34.8 s

# chord: (bass root, the guitar's strum)
PLAINS_CHORDS = {
    "A": ("A1", ["A3", "C#4", "E4"]), "D": ("D2", ["A3", "D4", "F#4"]),
    "E": ("E2", ["B3", "E4", "G#4"]), "F#m": ("F#1", ["A3", "C#4", "F#4"]),
    "C#m": ("C#2", ["G#3", "C#4", "E4"]), "Bm": ("B1", ["B3", "D4", "F#4"]),
}

PLAINS_TUNE = [
    (0, "E4", 1), (1, "A4", 1), (2, "C#5", 1.5), (3.5, "B4", .5),
    (4, "A4", 1.5), (5.5, "F#4", .5), (6, "D5", 2),
    (8, "C#5", 1), (9, "E5", 1), (10, "A5", 2),
    (12, "E5", 2), (14, "B4", 1), (15, "G#4", 1),
    (16, "A4", 1), (17, "C#5", 1), (18, "F#5", 2),
    (20, "F#5", 1.5), (21.5, "E5", .5), (22, "D5", 2),
    (24, "E5", 1), (25, "G#5", 1), (26, "B5", 2),
    (28, "A5", 3), (31, "E5", 1),
    (32, "F#5", 2), (34, "A5", 2),
    (36, "G#5", 2), (38, "B5", 2),
    (40, "C#6", 2), (42, "G#5", 2),
    (44, "A5", 3), (47, "C#6", 1),
    (48, "D6", 2), (50, "A5", 1), (51, "F#5", 1),
    (52, "F#5", 2), (54, "D5", 1), (55, "B4", 1),
    (56, "B4", 1), (57, "E5", 1), (58, "G#5", 1), (59, "B5", 1),
    (60, "B5", 2), (62, "G#5", 1), (63, "E5", 1),
]

PLAINS_MIX = {"steel": 0.2, "horn": 0.3, "drivebass": 0.34, "kick": 0.5, "snare": 0.8,
              "hat": 0.12, "crash": 0.12, "tom": 0.6}


def plains_events():
    ev = []
    for bar, chord in enumerate(PLAINS_CHART):
        b = bar * 4
        root, strum = PLAINS_CHORDS[chord]
        # The gallop: an eighth and two sixteenths on every beat - da, da-da.
        for q in xrange(4):
            for off, vel in ((0.0, 1.0), (0.5, 0.6), (0.75, 0.7)):
                for s, t in enumerate(strum):
                    ev.append((b + q + off + s * 0.01, steel, t, 0.24, vel))
        for q in xrange(4):
            pitch = root if q % 2 == 0 else _shift(root, 7)
            ev.append((b + q, drivebass, pitch, 0.8, 1.0 if q % 2 == 0 else 0.8))
        # A march: kick on one and three, snare on two and four, ghost notes riding the gallop.
        _rock_drums(ev, b, kicks=(0.0, 2.0), hats=4)
        for g in (1.75, 3.5, 3.75):
            ev.append((b + g, snare, None, 0.2, 0.3))
        if bar in (0, 4, 12):
            ev.append((b, crash, None, 1.5, 1.0))
    _tune(ev, PLAINS_TUNE, 16, horn)
    # Toms falling home at the end of the loop.
    for k, pitch in enumerate(("E3", "C#3", "B2", "A2")):
        ev.append((79 + k * 0.25, tom, pitch, 0.3, 0.9))
    return _mixed(ev, PLAINS_MIX)


# ---------------------------------------------------------------------------
# City Rooftops: night, 120 BPM, A minor. Synthwave - a synth arpeggio running sixteenths round
# the chord like lights going by, a pulsing octave bass, a big backbeat, and a long singing lead.
# ---------------------------------------------------------------------------

ROOF_BPM = 120.0
ROOF_CHART = ["Am", "F", "C", "G", "Am", "F", "C", "E",
              "F", "G", "Am", "Am", "F", "G", "E", "E"]
ROOF_BEATS = 4 * len(ROOF_CHART)        # 64 beats, 32.0 s

ROOF_CHORDS = {
    "Am": ("A1", ["A3", "C4", "E4", "A4"]), "F": ("F1", ["F3", "A3", "C4", "F4"]),
    "C": ("C2", ["G3", "C4", "E4", "G4"]), "G": ("G1", ["G3", "B3", "D4", "G4"]),
    "E": ("E2", ["G#3", "B3", "E4", "G#4"]),
}

ROOF_TUNE = [
    (0, "E5", 2), (2, "C5", 1), (3, "A4", 1),
    (4, "A4", 2), (6, "C5", 1), (7, "F5", 1),
    (8, "E5", 2), (10, "G5", 1), (11, "E5", 1),
    (12, "D5", 3), (15, "B4", 1),
    (16, "C5", 1), (17, "E5", 1), (18, "A5", 2),
    (20, "A5", 1), (21, "G5", .5), (21.5, "F5", .5), (22, "C5", 2),
    (24, "E5", 1), (25, "G5", 1), (26, "C6", 2),
    (28, "B5", 2), (30, "G#5", 2),
    (32, "C6", 2), (34, "A5", 2),
    (36, "D6", 2), (38, "B5", 2),
    (40, "C6", 1), (41, "B5", 1), (42, "A5", 2),
    (44, "E5", 4),
    (48, "F5", 2), (50, "A5", 2),
    (52, "G5", 2), (54, "B5", 2),
    (56, "G#5", 2), (58, "B5", 2),
    (60, "E5", 4),
]

ROOF_MIX = {"arp": 0.4, "lead": 0.28, "drivebass": 0.4, "kick": 0.62, "snare": 1.1, "hat": 0.14,
            "crash": 0.12}


def rooftops_events():
    ev = []
    order = [0, 1, 2, 3, 2, 1, 2, 3]
    for bar, chord in enumerate(ROOF_CHART):
        b = bar * 4
        root, tones = ROOF_CHORDS[chord]
        for s in xrange(16):
            t = tones[order[s % 8]]
            ev.append((b + s * 0.25, arp, _oct(t, 1) if s >= 8 and bar % 2 else t, 0.2,
                       1.0 if s % 4 == 0 else 0.7))
        for e in xrange(8):
            ev.append((b + e * 0.5, drivebass, _oct(root, 1) if e % 2 else root, 0.4,
                       1.0 if e % 2 == 0 else 0.75))
        _rock_drums(ev, b, kicks=(0.0, 1.5, 2.0), hats=8, hat_vel=(0.5, 0.9))
        if bar in (0, 8):
            ev.append((b, crash, None, 2.0, 1.0))
    _tune(ev, ROOF_TUNE, 0, lead, held=0.97)
    return _mixed(ev, ROOF_MIX)


# ---------------------------------------------------------------------------
# The Big Show: guitar rock, 180 BPM, in A (Eric, 2026-10-20: "big show should be more rock
# focused for its music. can you find something with guitar?"). Two rhythm guitars on power
# chords, a lead guitar, a bass guitar and a rock kit, twenty-four bars:
#
#   the riff (8 bars)    palm-muted A chugging under stabs of C, D and G - the riff alone, then
#                        again with the lead guitar's hook over it
#   the chorus (8)       open chords left to ring, D - A - E - F# and D - A - E - E, struck 3-3-2,
#                        the lead singing the tune on top and the ride cymbal going
#   the solo (4)         the chug galloping on A, the lead running and bending over it
#   the turnaround (4)   F - G - A hit and held twice, stop-time - the cartoon cadence the
#                        victory sting lands with - then a bar and a half of muted E swelling
#                        under a snare roll, and an E rung out into the riff at the top
#
# 180 BPM is exactly twenty frames a beat, which is what the stage's speakers thump on
# (SpeakerBlast.BeatFrames), so the cones keep time with it for the whole match. Power chords
# have no third, so FIGHT!'s A major chord lands on the riff, and the riff can borrow C and G
# from A minor the way rock does while the chorus sings in A major.
# ---------------------------------------------------------------------------

BIG_BPM = 180.0
BIG_BEATS = 96                          # 24 bars, 32.0 s

# A power chord's name: (the guitars' lowest string, the bass's note an octave under it).
BIG_ROOTS = {"A": ("A2", "A1"), "C": ("C3", "C2"), "D": ("D3", "D2"), "E": ("E2", "E1"),
             "F": ("F2", "F1"), "F#": ("F#2", "F#1"), "G": ("G2", "G1")}

MUTE, OPEN = True, False

# The riff, a bar at a time: (beat in the bar, chord, length in beats, palm-muted?). Each bar
# chugs on A and breaks out of it on the off-beat; the fourth bar climbs to E, which pulls
# back to A.
RIFF_1 = [(0, "A", .5, MUTE), (.5, "A", .5, MUTE), (1, "A", .5, MUTE), (1.5, "C", .5, OPEN),
          (2, "A", .5, MUTE), (2.5, "A", .5, MUTE), (3, "D", 1, OPEN)]
RIFF_2 = [(0, "A", .5, MUTE), (.5, "A", .5, MUTE), (1, "A", .5, MUTE), (1.5, "C", .5, OPEN),
          (2, "D", .5, OPEN), (2.5, "C", .5, OPEN), (3, "G", 1, OPEN)]
RIFF_4 = [(0, "A", .5, MUTE), (.5, "A", .5, MUTE), (1, "A", .5, MUTE), (1.5, "C", .5, OPEN),
          (2, "D", .5, OPEN), (2.5, "E", 1.5, OPEN)]


def _chorus_bar(chord):
    """Struck 3-3-2 and left to ring, with a muted push into the next bar."""
    return [(0, chord, 1.5, OPEN), (1.5, chord, 1.5, OPEN), (3, chord, .5, OPEN),
            (3.5, chord, .5, MUTE)]


def _gallop(chord, upto=4.0):
    return [(e * 0.5, chord, .5, MUTE) for e in xrange(int(upto * 2))]


BIG_GUITAR = (
    [RIFF_1, RIFF_2, RIFF_1, RIFF_4] * 2
    + [_chorus_bar(c) for c in ("D", "A", "E", "F#", "D", "A", "E", "E")]
    + [_gallop("A"), _gallop("A", 3) + [(3, "G", 1, OPEN)],
       _gallop("A"), _gallop("A", 2) + [(2, "C", .5, OPEN), (2.5, "D", 1.5, OPEN)]]
    + [[(0, "F", 1.5, OPEN), (1.5, "G", 1.5, OPEN), (3, "A", 1, OPEN)],
       [(0, "F", 1.5, OPEN), (1.5, "G", 1.5, OPEN), (3, "C", .5, OPEN), (3.5, "D", .5, OPEN)],
       _gallop("E"), _gallop("E", 2) + [(2, "E", 2, OPEN)]]
)

BEND = True

# The lead guitar: (beat, note, length[, bent up into]). Chord tones on the strong beats, and no
# note a semitone off the chord under it. The riff's hook and the solo are A minor pentatonic -
# A C D E G - which sits on every chord the riff uses; the chorus tune is in A major.
BIG_HOOK = [
    (16, "A4", .5), (16.5, "C5", .5), (17, "D5", 1, BEND), (18, "E5", .5), (18.5, "D5", .5),
    (19, "C5", .5), (19.5, "A4", .5),
    (20, "E5", .5), (20.5, "G5", .5), (21, "A5", 1, BEND), (22, "G5", .5), (22.5, "E5", .5),
    (23, "D5", 1),
    (24, "A4", .5), (24.5, "C5", .5), (25, "D5", 1, BEND), (26, "E5", .5), (26.5, "D5", .5),
    (27, "C5", .5), (27.5, "A4", .5),
    (28, "E5", .5), (28.5, "G5", .5), (29, "A5", .5), (29.5, "C6", .5), (30, "D6", .5),
    (30.5, "E6", 1.5, BEND),
]
BIG_TUNE = [
    (32, "A5", 1.5), (33.5, "F#5", .5), (34, "A5", 1), (35, "B5", 1),
    (36, "C#6", 2, BEND), (38, "B5", 1), (39, "A5", 1),
    (40, "B5", 1.5), (41.5, "G#5", .5), (42, "E5", 2),
    (44, "F#5", 1), (45, "A5", 1), (46, "C#6", 2),
    (48, "D6", 1.5), (49.5, "B5", .5), (50, "A5", 2),
    (52, "C#6", 1.5), (53.5, "B5", .5), (54, "A5", 2),
    (56, "B5", 1), (57, "G#5", 1), (58, "E5", 1), (59, "F#5", 1),
    (60, "G#5", 1), (61, "B5", 1), (62, "E6", 2, BEND),
]
BIG_SOLO = [
    (64, "A5", .5, BEND), (64.5, "G5", .25), (64.75, "E5", .25), (65, "G5", .5), (65.5, "A5", .5),
    (66, "C6", 1, BEND), (67, "A5", .5), (67.5, "G5", .5),
    (68, "E5", .25), (68.25, "G5", .25), (68.5, "A5", .25), (68.75, "C6", .25), (69, "D6", 1, BEND),
    (70, "C6", .5), (70.5, "A5", .5), (71, "G5", 1),
    (72, "E6", .5), (72.5, "D6", .25), (72.75, "C6", .25), (73, "D6", .25), (73.25, "C6", .25),
    (73.5, "A5", .5), (74, "C6", .25), (74.25, "A5", .25), (74.5, "G5", .25), (74.75, "E5", .25),
    (75, "G5", .5), (75.5, "A5", .5),
    (76, "A5", 1.5, BEND), (77.5, "G5", .5), (78, "G5", .5), (78.5, "A5", 1.5),
    # Over the turnaround it doubles the hits, rising to the major third - A, B, C# over F, G, A.
    (80, "A5", 1.5), (81.5, "B5", 1.5), (83, "C#6", 1, BEND),
    (84, "A5", 1.5), (85.5, "B5", 1.5), (87, "C6", .5), (87.5, "D6", .5),
]

# The mix, one level per part. Set by measuring each part, in quarter-second windows, against
# the whole while it plays: the lead 4 dB under the whole, the rhythm guitars 5, the bass 6.5,
# the kick 8, the snare 12 (a quarter second is mostly the gap after a hit, so a drum reads low
# here), the cymbals 20, because the hit sounds own the top end. A-weighted, the loop comes out
# as loud as Open Plains and City Rooftops.
BIG_MIX = {"gtrmute": 0.5, "gtropen": 0.42, "gtrlead": 0.5, "gtrbend": 0.5, "bassgtr": 0.8,
           "kick": 1.8, "rocksnare": 1.0, "hat": 0.25, "ride": 0.4, "crash": 0.3, "tom": 0.9}


def _big_kit(ev, b, kicks, cymbal, cymbal_vel):
    """A bar of rock beat: the kick where it is told, the snare on two and four, and a cymbal -
    the hats, or the ride in the chorus - on every eighth."""
    for k in kicks:
        ev.append((b + k, kick, None, 0.3, 1.0 if k == int(k) else 0.8))
    for s in (1.0, 3.0):
        ev.append((b + s, rocksnare, None, 0.3, 1.0))
    for e in xrange(8):
        ev.append((b + e * 0.5, cymbal, None, 0.08, cymbal_vel[e % 2]))


def bigshow_events():
    ev = []
    for bar, hits in enumerate(BIG_GUITAR):
        b = bar * 4
        part = ("riff" if bar < 8 else "chorus" if bar < 16 else "solo" if bar < 20
                else "stops" if bar < 22 else "build")
        for at, chord, length, muted in hits:
            if part == "build":
                # The swell: each chug harder than the last, into the E rung out at the top.
                vel = 1.0 if not muted else 0.55 + 0.04 * (at * 2 + (bar - 22) * 8)
            elif part == "solo" and muted:
                vel = 1.0 if int(at * 2) % 8 in (0, 3, 6) else 0.72
            else:
                vel = 0.85 if muted else 1.0
            guitar, low = BIG_ROOTS[chord]
            ev.append((b + at, gtrmute if muted else gtropen, guitar, length, vel))
            if part == "stops":
                ev.append((b + at, bassgtr, low, length * 0.95, 1.0))
        if part != "stops":
            # The bass picks every eighth, on whatever chord the guitars last struck, locked
            # to them and to the kick.
            for e in xrange(8):
                chord = [h for h in hits if h[0] <= e * 0.5][-1][1]
                ev.append((b + e * 0.5, bassgtr, BIG_ROOTS[chord][1], 0.48,
                           1.0 if e % 2 == 0 else 0.8))

        if part == "riff":
            _big_kit(ev, b, (0.0, 1.5, 2.0), hat, (0.9, 0.6))
            if bar % 4 == 0:
                ev.append((b, crash, None, 2.0, 1.0))
        elif part == "chorus":
            # The kick goes with the guitars on one and the and of two, but not on four, where
            # it, the snare and a fresh strum together were the loudest moment in the loop. The
            # ride takes over from the hats.
            _big_kit(ev, b, (0.0, 1.5, 2.0), ride, (1.0, 0.6))
            if bar % 4 == 0:
                ev.append((b, crash, None, 2.0, 1.0))
        elif part == "solo":
            _big_kit(ev, b, (0.0, 0.5, 2.0, 2.5), hat, (1.0, 0.7))
            if bar == 16:
                ev.append((b, crash, None, 2.0, 1.0))
        elif part == "stops":
            # Stop-time: the whole band on each hit and nothing between - the snare taking the
            # kick's place on the last one.
            for at, chord, length, muted in hits:
                ev.append((b + at, rocksnare if at == 3 else kick, None, 0.3, 1.0))
                ev.append((b + at, crash, None, length, 0.8 if at else 1.0))
        else:
            for k in xrange(4):
                ev.append((b + k, kick, None, 0.3, 1.0))
            if bar == 22:
                for e in xrange(8):
                    ev.append((b + e * 0.5, rocksnare, None, 0.25, 0.35 + 0.05 * e))
            else:
                for s in xrange(8):
                    ev.append((b + s * 0.25, rocksnare, None, 0.25, 0.6 + 0.04 * s))
                for k, pitch in enumerate(("E3", "B2", "A2", "E2")):
                    ev.append((b + 2 + k * 0.5, tom, pitch, 0.5, 0.9))

    for item in BIG_HOOK + BIG_TUNE + BIG_SOLO:
        beat, pitch, length = item[:3]
        bent = len(item) > 3 and item[3]
        ev.append((beat, gtrbend if bent else gtrlead, pitch, length * 0.95, 1.0))
    return _mixed(ev, BIG_MIX)


# ---------------------------------------------------------------------------
# The Building Site: industrial, 126 BPM, A minor. The site's machines are the band: a kick on
# every beat, a hammer on an anvil for the backbeat, pistons ticking sixteenths, a muted power
# chord grinding, a robot's bleeps, steam venting at the end of every phrase, and a low riff
# going round like a conveyor belt.
# ---------------------------------------------------------------------------

SITE_BPM = 126.0
SITE_CHART = (["A5", "A5", "A5", "A5"]
              + ["C5", "C5", "G5", "G5"]
              + ["A5", "A5", "F5", "E5"]
              + ["F5", "G5", "A5", "A5"])
SITE_BEATS = 4 * len(SITE_CHART)        # 64 beats, 30.5 s

SITE_ROOTS = {"A5": ("A2", "A1"), "C5": ("C3", "C2"), "G5": ("G2", "G1"), "F5": ("F2", "F1"),
              "E5": ("E2", "E1")}

# The conveyor riff, two bars long, on A; moved onto each chord's root.
SITE_RIFF = [(0, "A3", .5), (.75, "A3", .25), (1, "C4", .5), (1.5, "A3", .5), (2, "G3", .5),
             (2.5, "A3", 1.5), (4, "A3", .5), (4.75, "A3", .25), (5, "E4", .5), (5.5, "D4", .5),
             (6, "C4", 1), (7, "G3", 1)]

SITE_MIX = {"chug": 0.34, "drivebass": 0.4, "kick": 0.7, "anvil": 0.34, "piston": 0.3,
            "bleep": 0.12, "vent": 0.18, "lead": 0.2, "crash": 0.12}


def site_events():
    ev = []
    for bar, chord in enumerate(SITE_CHART):
        b = bar * 4
        guitar, root = SITE_ROOTS[chord]
        for e in xrange(16):
            # The grind: sixteenths, every fourth one hit hard.
            if e % 2 == 0 or e in (3, 11):
                ev.append((b + e * 0.25, chug, guitar, 0.22, 1.0 if e % 4 == 0 else 0.55))
        for e in xrange(8):
            ev.append((b + e * 0.5, drivebass, root, 0.4, 1.0 if e % 2 == 0 else 0.7))
        for q in xrange(4):
            ev.append((b + q, kick, None, 0.3, 1.0))
        ev.append((b + 1, anvil, "E4", 0.3, 1.0))
        ev.append((b + 3, anvil, "E4", 0.3, 1.0))
        for s in xrange(16):
            ev.append((b + s * 0.25, piston, "A6", 0.05, 1.0 if s % 4 == 2 else 0.55))
        if bar >= 8:
            for s, t in enumerate((root, _shift(root, 12), _shift(root, 19), _shift(root, 24))):
                ev.append((b + 2 + s * 0.25, bleep, _oct(t, 2), 0.1, 1.0))
        if bar % 4 == 3:
            ev.append((b + 3, vent, None, 1.0, 1.0))
        if bar in (0, 8):
            ev.append((b, crash, None, 1.5, 1.0))
    # The riff goes round from the second phrase on, moved onto each pair of bars' chord.
    for pair in xrange(2, 8):
        root = SITE_ROOTS[SITE_CHART[pair * 2]][0]
        semis = (_NAMES.index(root[:-1]) - _NAMES.index("A")) % 12
        if semis > 6:
            semis -= 12
        riff = [(beat, _shift(p, semis), d) for beat, p, d in SITE_RIFF]
        _tune(ev, riff, pair * 8, lead, held=0.85)
    return _mixed(ev, SITE_MIX)


# ---------------------------------------------------------------------------
# The Kitchen Counter: funk, 108 BPM, A7 and D7. A choppy clav-like chop on the sixteenths, a
# popping bass, a horn section with the tune, and the drums partly pots and pans - a saucepan
# for the cowbell, a woodblock spoon for the clave - as if the cooks were the band.
# ---------------------------------------------------------------------------

KITCHEN_BPM = 108.0
KITCHEN_CHART = ["A7", "A7", "D7", "D7", "A7", "A7", "E7", "D7"] * 2
KITCHEN_BEATS = 4 * len(KITCHEN_CHART)  # 64 beats, 35.6 s

# chord: (semitones from A for the bass riff, the chop's voicing)
KITCHEN_CHORDS = {"A7": (0, ["G3", "C#4", "E4"]), "D7": (5, ["F#3", "C4", "D4"]),
                  "E7": (7, ["G#3", "D4", "E4"])}

# The popping bass, one bar, on A: the root, an octave pop, the seventh and the fifth.
KITCHEN_BASS = [(0, "A1", .4), (.75, "A1", .2), (1.5, "A2", .3), (2, "E2", .4), (2.75, "G2", .2),
                (3, "A2", .3), (3.5, "G2", .3)]
KITCHEN_CHOP = [0.5, 0.75, 1.5, 2.5, 3.25, 3.5]

KITCHEN_TUNE = [
    (0, "E5", .5), (.5, "G5", .5), (1, "A5", .5), (1.5, "G5", .5), (2, "E5", 1),
    (6.5, "E5", .5), (7, "G5", 1),
    (8, "F#5", .5), (8.5, "A5", .5), (9, "C6", .5), (9.5, "A5", .5), (10, "F#5", 1),
    (14.5, "A5", .5), (15, "C6", 1),
    (16, "E5", .5), (16.5, "G5", .5), (17, "A5", .5), (17.5, "G5", .5), (18, "E5", 1),
    (22, "E5", .5), (22.5, "G5", .5), (23, "A5", 1),
    (24, "G#5", .5), (24.5, "B5", .5), (25, "D6", .5), (25.5, "B5", .5), (26, "G#5", 1),
    (28, "A5", .5), (28.5, "F#5", .5), (29, "D5", 1), (30, "C5", 1),
]

KITCHEN_MIX = {"skank": 0.22, "horn": 0.28, "lead": 0.22, "drivebass": 0.45, "kick": 0.55,
               "snare": 0.9, "hat": 0.12, "pan": 0.16, "block": 0.14, "crash": 0.1}


def kitchen_events():
    ev = []
    for bar, chord in enumerate(KITCHEN_CHART):
        b = bar * 4
        semis, chop = KITCHEN_CHORDS[chord]
        for off in KITCHEN_CHOP:
            for t in chop:
                ev.append((b + off, skank, t, 0.2, 1.0 if off in (0.5, 2.5) else 0.7))
        for beat, p, d in KITCHEN_BASS:
            ev.append((b + beat, drivebass, _shift(p, semis), d, 1.0 if beat in (0, 2) else 0.8))
        _rock_drums(ev, b, kicks=(0.0, 1.75, 2.5), hats=16, hat_vel=(0.5, 0.8))
        # The saucepan on the off-beats, the spoon on a son clave across the pair of bars.
        for off in (0.5, 1.5, 2.5, 3.5):
            ev.append((b + off, pan, "E5", 0.3, 1.0))
        for k in ((0, 1.5, 3) if bar % 2 == 0 else (1, 2)):
            ev.append((b + k, block, "E6", 0.1, 1.0))
        if bar in (0, 8):
            ev.append((b, crash, None, 1.5, 1.0))
    _tune(ev, KITCHEN_TUNE, 0, horn, held=0.9)
    # Second time round the lead joins the horns an octave down.
    _tune(ev, [(beat, _oct(p, -1), d) for beat, p, d in KITCHEN_TUNE], 32, lead, vel=0.8, held=0.9)
    _tune(ev, KITCHEN_TUNE, 32, horn, held=0.9)
    return _mixed(ev, KITCHEN_MIX)


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
    # Each stage's own battle music (Stage data names it); the Fridge Door plays "battle".
    ("battle_plains", plains_events, PLAINS_BPM, PLAINS_BEATS, 0.15, -3.0, True),
    ("battle_rooftops", rooftops_events, ROOF_BPM, ROOF_BEATS, 0.2, -3.0, True),
    ("battle_bigshow", bigshow_events, BIG_BPM, BIG_BEATS, 0.1, -3.0, True),
    ("battle_playground", playground_events, SKA_BPM, SKA_BEATS, 0.12, -3.0, True),
    ("battle_site", site_events, SITE_BPM, SITE_BEATS, 0.1, -3.0, True),
    ("battle_kitchen", kitchen_events, KITCHEN_BPM, KITCHEN_BEATS, 0.1, -3.0, True),
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
