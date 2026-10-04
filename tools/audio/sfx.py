# -*- coding: utf-8 -*-
"""Every DrawFight sound effect, one function per file, plus REGISTRY.

The sound of the game follows its look (see .ai/audio-direction.md): a kid's drawing come to
life, so cartoon rather than gritty. Whooshes are filtered noise, hits are a pitched thump
under a bright paper slap, and anything tonal is in A major so the stingers agree with each
other and with the music.

A function that is not in REGISTRY does not ship - build.py does not scan this module.
"""
from __future__ import division

import synth as S

SEED = 7100


# ---------------------------------------------------------------------------
# shared constructs
# ---------------------------------------------------------------------------

def swell(dur, peak_at=0.35, curve=2.0):
    """Rises to full at peak_at of the way through, then falls away. The shape of a swing."""
    n = S.n_samples(dur)
    up = max(1, int(n * peak_at))
    return S.ramp(up, 0.0, 1.0, 0.7) + S.decay(n - up, curve)


def whoosh(dur, f0, f1, q=1.3, seed=1, peak_at=0.35):
    """Air moving past something: bandpassed noise sweeping in pitch, swelling and fading."""
    v = S.osc("noise", 0, dur, seed=SEED + seed)
    v = S.bandpass(v, S.sweep(dur, f0, f1, 0.8), q)
    return S.env_mul(v, swell(dur, peak_at))


def thump(dur, f0, f1, k=10.0):
    """The body of an impact: a sine falling in pitch, gone fast."""
    n = S.n_samples(dur)
    return S.env_mul(S.osc("sine", S.sweep(dur, f0, f1, 0.4), dur), S.expdecay(n, k))


def slap(dur, cutoff=1800.0, k=28.0, seed=2):
    """The paper slap on top of an impact - bright, short, unpitched."""
    n = S.n_samples(dur)
    v = S.highpass(S.osc("noise", 0, dur, seed=SEED + seed), cutoff)
    return S.env_mul(v, S.expdecay(n, k))


def bell(freq, dur, bright=1.0):
    """A small struck bell - the voice of every countdown and menu sound."""
    return S.partials(freq, [1.0, 2.0, 3.01, 4.2], dur,
                      amps=[1.0, 0.45 * bright, 0.22 * bright, 0.12 * bright],
                      decays=[5.0, 7.0, 10.0, 14.0])


def tone(shape, freq, dur, cutoff=3000.0, attack=0.004, curve=3.0):
    v = S.lowpass(S.osc(shape, freq, dur), cutoff)
    return S.env_mul(v, S.ad(dur, attack, curve))


def metal(freq, dur, k=7.0):
    """A clank - inharmonic partials, so it rings like a girder rather than a note."""
    return S.partials(freq, [1.0, 1.71, 2.46, 3.39], dur,
                      amps=[1.0, 0.6, 0.4, 0.25], decays=[k, k * 1.3, k * 1.7, k * 2.2])


def boom(dur, bright=1.0, seed=3):
    """An explosion: a falling sub thump under a noise burst that darkens as it goes."""
    n = S.n_samples(dur)
    body = S.drive(thump(dur, 95, 32, 4.0), 2.5)
    air = S.lowpass(S.osc("noise", 0, dur, seed=SEED + seed), S.sweep(dur, 4200 * bright, 220, 0.5))
    air = S.env_mul(air, S.expdecay(n, 5.0))
    return S.mix(S.gain(body, 0.9), S.gain(air, 0.75))


A4, CS5, E5, A5, CS6, E6, A6 = 440.0, 554.37, 659.26, 880.0, 1108.73, 1318.51, 1760.0


# ---------------------------------------------------------------------------
# swings - one per attack, picked from the move's data by SfxCatalog.ForMove
# ---------------------------------------------------------------------------

def swing_light():
    return whoosh(0.13, 3200, 1600, 1.5, seed=10, peak_at=0.3)


def swing_medium():
    return S.mix(whoosh(0.19, 2400, 950, 1.3, seed=11),
                 S.gain(whoosh(0.19, 700, 400, 1.0, seed=12), 0.4))


def swing_heavy():
    body = whoosh(0.30, 1500, 380, 1.0, seed=13, peak_at=0.4)
    low = S.lowpass(whoosh(0.30, 500, 180, 0.8, seed=14, peak_at=0.4), 600)
    return S.mix(body, S.gain(low, 0.8))


def swing_blade():
    """A sword: the swing plus a bright shing as the edge goes through."""
    air = whoosh(0.22, 3400, 1500, 1.4, seed=15)
    ring = S.highpass(S.partials(2350, [1.0, 1.52, 2.71, 3.93], 0.22,
                                 amps=[1.0, 0.7, 0.4, 0.3], decays=[9, 11, 14, 18]), 1200)
    return S.layer([(air, 0.0, 1.0), (ring, 0.03, 0.32)])


def smash_charge():
    """A held smash: a buzzing hum, looped. Fighter raises its pitch as the charge builds.

    Every frequency is a whole number of cycles in the 0.5 s loop, and the loop is cut from
    the second half of a longer render so the filter has settled - otherwise the seam ticks."""
    dur = 1.0
    a = S.osc("saw", 222.0, dur)
    b = S.osc("saw", 224.0, dur, phase=0.3)
    v = S.lowpass(S.mix(a, S.gain(b, 0.8)), 1400.0, 1.2)
    v = S.tremolo(v, 14.0, 0.45)
    half = S.n_samples(0.5)
    return v[half:half * 2]


def taunt():
    """A cartoon boing."""
    dur = 0.38
    f = S.fm("sine", S.sweep(dur, 260, 540, 0.5), 15.0, S.sweep(dur, 0.25, 0.02), dur)
    return S.env_mul(S.mix(f, S.gain(S.osc("tri", S.sweep(dur, 520, 1080, 0.5), dur), 0.25)),
                     S.ad(dur, 0.004, 1.6))


# ---------------------------------------------------------------------------
# specials
# ---------------------------------------------------------------------------

def special_shot():
    """Pew."""
    dur = 0.17
    v = S.lowpass(S.osc("square", S.sweep(dur, 1500, 320, 0.6), dur), 3500)
    return S.env_mul(v, S.ad(dur, 0.002, 2.0))


def special_volley():
    """Several blades or shots at once - summoned swords, a ring of daggers: a quick flurry of
    shings climbing in pitch, so it reads as many things and not one loud one."""
    def shing(f, seed):
        air = whoosh(0.16, 3600, 1800, 1.4, seed=seed, peak_at=0.25)
        ring = S.highpass(S.partials(f, [1.0, 1.52, 2.71], 0.2, amps=[1.0, 0.6, 0.3],
                                     decays=[10, 13, 17]), 1200)
        return S.mix(S.gain(air, 0.8), S.gain(ring, 0.35))
    return S.layer([(shing(2100, 70), 0.0, 1.0), (shing(2500, 71), 0.045, 0.9),
                    (shing(2950, 72), 0.09, 0.85)])


def special_fire():
    """A thrown fireball: a gritty roar that swells."""
    dur = 0.45
    n = S.n_samples(dur)
    roar = S.bandpass(S.osc("noise", 0, dur, seed=SEED + 20), S.sweep(dur, 500, 1600, 0.6), 1.2)
    roar = S.gate(roar, 70.0, 0.75, seed=SEED + 21)
    roar = S.drive(S.env_mul(roar, swell(dur, 0.25)), 2.5)
    return S.mix(roar, S.gain(S.env_mul(S.osc("sine", 95, dur), S.expdecay(n, 5.0)), 0.5))


def special_beam():
    """A laser: a quick charging whine, then the zap."""
    whine = S.env_mul(S.osc("sine", S.sweep(0.22, 500, 1900, 1.4), 0.22), S.ramp(S.n_samples(0.22), 0.0, 1.0, 2.0))
    dur = 0.55
    n = S.n_samples(dur)
    zap = S.ringmod(S.osc("saw", S.sweep(dur, 260, 170), dur), 61.0, 0.6)
    zap = S.lowpass(zap, S.sweep(dur, 5200, 900), 1.4)
    fizz = S.env_mul(S.highpass(S.osc("noise", 0, dur, seed=SEED + 22), 5000), S.expdecay(n, 6.0))
    zap = S.env_mul(S.mix(zap, S.gain(fizz, 0.35)), S.adsr(dur, 0.003, 0.08, 0.6, 0.3))
    return S.layer([(S.gain(whine, 0.5), 0.0, 1.0), (zap, 0.2, 1.0)])


def special_dash():
    air = whoosh(0.26, 1100, 3600, 1.2, seed=23, peak_at=0.5)
    zip_ = S.env_mul(S.lowpass(S.osc("saw", S.sweep(0.26, 180, 620), 0.26), 1800), swell(0.26, 0.5))
    return S.mix(air, S.gain(zip_, 0.35))


def special_glint():
    """Blur Slash's telegraph: a ring rising through the glint and peaking on the frame he
    goes. It is exactly 22 frames long, the move's startup, and it stops dead - the cut is
    special_blink. See .ai/fighting-design.md, Telegraphs."""
    dur = 22 / 60.0
    n = S.n_samples(dur)
    ring = S.partials(S.sweep(dur, 900, 2700, 1.5), [1.0, 2.01, 3.0], dur,
                      amps=[1.0, 0.4, 0.2], decays=[0.01, 0.01, 0.01])
    shimmer = S.tremolo(ring, 24.0, 0.3)
    return S.env_mul(shimmer, S.ramp(n, 0.05, 1.0, 2.2))


def special_blink():
    """The sharp cut that ends the glint: he is already across the stage."""
    dur = 0.14
    n = S.n_samples(dur)
    cut = S.env_mul(S.highpass(S.osc("noise", 0, dur, seed=SEED + 24), 3000), S.expdecay(n, 18.0))
    edge = S.env_mul(S.osc("sine", S.sweep(dur, 3600, 1800), dur), S.expdecay(n, 14.0))
    return S.mix(cut, S.gain(edge, 0.5))


def special_rise():
    """An up-special: fwoop."""
    dur = 0.36
    air = whoosh(dur, 500, 3200, 1.1, seed=25, peak_at=0.3)
    lift = S.env_mul(S.osc("tri", S.sweep(dur, 240, 960, 0.7), dur), swell(dur, 0.3))
    return S.mix(air, S.gain(lift, 0.5))


def special_wings():
    """Three wingbeats."""
    flap = S.mix(S.lowpass(whoosh(0.1, 900, 300, 0.9, seed=26, peak_at=0.2), 1400),
                 S.gain(thump(0.1, 140, 80, 12.0), 0.5))
    return S.layer([(flap, 0.0, 1.0), (flap, 0.12, 0.85), (flap, 0.24, 0.7)])


def special_hook():
    """A grapple: thwip out, clank on."""
    dur = 0.1
    thwip = S.env_mul(S.lowpass(S.osc("saw", S.sweep(dur, 2200, 500), dur), 4000), S.ad(dur, 0.002, 2.0))
    return S.layer([(thwip, 0.0, 0.7), (metal(980, 0.3, 9.0), 0.09, 0.5)])


def special_trap():
    """Something planted in the ground: a thunk and a ring."""
    return S.layer([(thump(0.14, 170, 70, 9.0), 0.0, 1.0),
                    (S.highpass(metal(1750, 0.4, 6.0), 900), 0.0, 0.35)])


def special_drop():
    """Something falling from above: a cartoon whistle."""
    dur = 0.5
    f = S.fm("sine", S.sweep(dur, 1900, 650, 0.8), 7.0, 0.012, dur)
    return S.env_mul(f, S.adsr(dur, 0.03, 0.05, 0.8, 0.12))


def special_resize():
    """Growing or shrinking: a wobbling sweep."""
    dur = 0.42
    f = S.fm("tri", S.sweep(dur, 330, 880, 0.7), 18.0, 0.12, dur)
    return S.env_mul(S.lowpass(f, 3000), S.adsr(dur, 0.01, 0.08, 0.7, 0.15))


def special_quake():
    """A ground slam: a low rumble you feel."""
    dur = 0.75
    n = S.n_samples(dur)
    rumble = S.decimate(S.lowpass(S.osc("noise", 0, dur, seed=SEED + 27), 260), 6)
    rumble = S.env_mul(rumble, S.expdecay(n, 4.0))
    return S.mix(S.drive(thump(dur, 80, 34, 3.5), 2.0), S.gain(rumble, 0.7),
                 S.gain(slap(0.08, 1200, 30.0, seed=28), 0.4))


def special_build():
    """Lug's girder going in: clank clank clank."""
    return S.layer([(metal(560, 0.25), 0.0, 1.0), (metal(640, 0.25), 0.09, 0.85),
                    (metal(740, 0.3), 0.18, 0.95)])


def bomb_fuse():
    """A lit fuse: hiss and crackle."""
    dur = 0.65
    hiss = S.highpass(S.osc("noise", 0, dur, seed=SEED + 29), 3200)
    crackle = S.gate(S.osc("noise", 0, dur, seed=SEED + 30), 90.0, 0.3, seed=SEED + 31)
    return S.env_mul(S.mix(S.gain(hiss, 0.6), S.gain(S.highpass(crackle, 1500), 0.5)),
                     S.adsr(dur, 0.02, 0.05, 0.9, 0.08))


def explosion():
    return S.reverb(S.pad(boom(1.1), 1.4), size=0.5, damp=0.5, mix=0.18)


# ---------------------------------------------------------------------------
# hits - picked by knockback in SfxPlayer.Hit
# ---------------------------------------------------------------------------

def hit_light():
    return S.drive(S.mix(thump(0.14, 280, 130, 12.0), S.gain(slap(0.07, 2200, 30.0, seed=40), 0.55)), 2.0)


def hit_medium():
    body = S.lowpass(S.osc("noise", 0, 0.2, seed=SEED + 41), 1500)
    body = S.env_mul(body, S.expdecay(S.n_samples(0.2), 12.0))
    return S.drive(S.mix(thump(0.2, 210, 85, 9.0), S.gain(slap(0.09, 1800, 24.0, seed=42), 0.6),
                         S.gain(body, 0.45)), 2.4)


def hit_heavy():
    dur = 0.32
    crunch = S.bandpass(S.osc("noise", 0, dur, seed=SEED + 43), 900, 1.2)
    crunch = S.env_mul(crunch, S.expdecay(S.n_samples(dur), 9.0))
    return S.drive(S.mix(thump(dur, 160, 55, 6.0), S.gain(crunch, 0.7),
                         S.gain(slap(0.1, 1500, 20.0, seed=44), 0.6)), 3.0)


def hit_smash():
    """A hit at kill range: the heavy hit, plus a bright crack and a ring that hangs."""
    crack = S.gain(slap(0.05, 3500, 40.0, seed=45), 1.0)
    ring = S.partials(720, [1.0, 2.4, 3.9, 5.1], 0.7, amps=[1.0, 0.5, 0.3, 0.2], decays=[5, 7, 9, 12])
    v = S.layer([(hit_heavy(), 0.0, 1.0), (crack, 0.0, 0.8), (ring, 0.01, 0.35)])
    return S.reverb(S.pad(v, 0.8), size=0.5, damp=0.45, mix=0.15)


def hit_block():
    """A hit on a block: muffled, like hitting a cardboard shield."""
    dur = 0.16
    n = S.n_samples(dur)
    card = S.env_mul(S.lowpass(S.osc("noise", 0, dur, seed=SEED + 46), 800), S.expdecay(n, 14.0))
    knock = S.env_mul(S.osc("sine", S.sweep(dur, 360, 260), dur), S.expdecay(n, 16.0))
    return S.mix(card, S.gain(knock, 0.6))


# ---------------------------------------------------------------------------
# the match
# ---------------------------------------------------------------------------

def count_tick():
    """3, 2, 1."""
    return S.mix(bell(A5, 0.45), S.gain(tone("square", A5, 0.2, 2600), 0.25))


def count_go():
    """FIGHT! - the A major chord the ticks were waiting for, an octave up, and a rush."""
    dur = 1.0
    chord = S.mix(*[S.gain(tone("saw", f, dur, 4200, 0.004, 1.6), g)
                    for f, g in ((A5, 0.5), (CS6, 0.4), (E6, 0.4), (A6, 0.3))])
    rush = whoosh(0.5, 600, 4200, 1.0, seed=50, peak_at=0.15)
    v = S.layer([(chord, 0.0, 1.0), (bell(A6, 0.8), 0.0, 0.6), (rush, 0.0, 0.5)])
    return S.reverb(S.pad(v, 1.2), size=0.6, damp=0.4, mix=0.2)


def ko_blast():
    """A KO: the biggest sound in the game - a bright boom and a rising zing as the star goes."""
    zing = S.env_mul(S.osc("sine", S.sweep(0.6, 700, 2600, 0.6), 0.6), S.adsr(0.6, 0.01, 0.1, 0.6, 0.3))
    v = S.layer([(boom(1.2, bright=1.5, seed=51), 0.0, 1.0), (slap(0.06, 2500, 30.0, seed=52), 0.0, 0.8),
                 (zing, 0.05, 0.35)])
    return S.reverb(S.pad(v, 1.6), size=0.65, damp=0.4, mix=0.2)


def respawn():
    """Back on the stage: a soft rising arpeggio."""
    return S.layer([(bell(E5, 0.5, 0.6), 0.0, 0.7), (bell(A5, 0.5, 0.6), 0.07, 0.8),
                    (bell(CS6, 0.6, 0.6), 0.14, 0.9)])


def game_set():
    """The match is over: a short fanfare."""
    def note(f, dur):
        v = S.mix(S.osc("square", f, dur, duty=0.5), S.gain(S.osc("saw", f * 1.003, dur), 0.6))
        return S.env_mul(S.lowpass(v, 3200), S.adsr(dur, 0.006, 0.06, 0.75, min(0.12, dur * 0.4)))
    v = S.layer([(note(A4, 0.14), 0.0, 1.0), (note(CS5, 0.14), 0.15, 1.0), (note(E5, 0.14), 0.30, 1.0),
                 (note(A5, 0.9), 0.45, 1.0), (note(CS5, 0.9), 0.45, 0.5), (note(E5, 0.9), 0.45, 0.5),
                 (S.highpass(boom(0.9, 1.8, seed=53), 600), 0.45, 0.4)])
    return S.reverb(S.pad(v, 1.8), size=0.7, damp=0.4, mix=0.2)


# ---------------------------------------------------------------------------
# menus
# ---------------------------------------------------------------------------

def ui_move():
    """A pencil tick."""
    return S.mix(slap(0.035, 3500, 40.0, seed=60), S.gain(tone("sine", 1900, 0.035, 6000, 0.001, 4.0), 0.4))


def ui_select():
    dur = 0.13
    return S.mix(S.env_mul(S.osc("sine", S.sweep(dur, 520, 1150, 0.5), dur), S.ad(dur, 0.002, 2.5)),
                 S.gain(slap(0.03, 3000, 40.0, seed=61), 0.4))


def ui_back():
    dur = 0.13
    return S.mix(S.env_mul(S.osc("sine", S.sweep(dur, 950, 420, 0.5), dur), S.ad(dur, 0.002, 2.5)),
                 S.gain(slap(0.03, 3000, 40.0, seed=62), 0.4))


def ui_ready():
    """A player locked in."""
    return S.layer([(bell(E6, 0.35), 0.0, 0.8), (bell(A6, 0.45), 0.08, 1.0)])


def pause():
    return S.layer([(tone("tri", A5, 0.18, 4000, 0.003, 2.0), 0.0, 1.0),
                    (tone("tri", E5, 0.25, 4000, 0.003, 2.0), 0.09, 1.0)])


# ---------------------------------------------------------------------------
# DoomBot - electricity, rockets, a furnace, and rusty joints
# ---------------------------------------------------------------------------

def zap():
    """An electric shock: a buzzing ring-modulated saw, chopped irregularly so it crackles."""
    dur = 0.32
    n = S.n_samples(dur)
    buzz = S.ringmod(S.osc("saw", S.sweep(dur, 180, 120), dur), 97.0, 0.7)
    buzz = S.gate(S.bandpass(buzz, 2400, 0.9), 55.0, 0.7, seed=SEED + 80)
    crack = S.env_mul(S.highpass(S.osc("noise", 0, dur, seed=SEED + 81), 4000), S.expdecay(n, 9.0))
    return S.env_mul(S.mix(buzz, S.gain(crack, 0.7)), S.ad(dur, 0.002, 1.6))


def jets():
    """Rocket boots lighting: a roar of filtered noise with a low rumble, swelling then easing."""
    dur = 0.8
    roar = S.lowpass(S.osc("noise", 0, dur, seed=SEED + 82), S.sweep(dur, 900, 2400, 0.6), 0.9)
    rumble = S.lowpass(S.osc("noise", 0, dur, seed=SEED + 83), 180)
    v = S.mix(roar, S.gain(rumble, 1.4))
    return S.env_mul(v, S.adsr(dur, 0.06, 0.1, 0.7, 0.3))


def missile():
    """A small missile launching: a whoosh with a rising rocket whine."""
    dur = 0.36
    whine = S.env_mul(S.osc("tri", S.sweep(dur, 500, 1400, 0.7), dur), S.ad(dur, 0.01, 1.8))
    return S.mix(whoosh(dur, 700, 2600, 1.0, seed=84, peak_at=0.2), S.gain(whine, 0.35),
                 S.gain(slap(0.05, 2000, 30.0, seed=85), 0.5))


def steam():
    """A puff of steam: a short hiss that swells and fades."""
    dur = 0.42
    hiss = S.highpass(S.osc("noise", 0, dur, seed=SEED + 86), 2600)
    return S.env_mul(hiss, swell(dur, 0.15, 2.4))


def overheat():
    """Overheating: a long hiss of steam pouring out, sputtering, under a falling clunk."""
    dur = 1.2
    hiss = S.highpass(S.osc("noise", 0, dur, seed=SEED + 87), 2200)
    hiss = S.gate(hiss, 14.0, 0.8, seed=SEED + 88)
    hiss = S.env_mul(hiss, S.adsr(dur, 0.02, 0.1, 0.8, 0.5))
    clunk = S.layer([(metal(300, 0.4, 8.0), 0.0, 1.0), (thump(0.2, 140, 60, 9.0), 0.0, 0.8)])
    return S.layer([(clunk, 0.0, 0.7), (hiss, 0.05, 1.0)])


def furnace_blast():
    """The furnace let out at once: a whoomph of flame round a heavy thump, with a roaring tail."""
    dur = 1.0
    n = S.n_samples(dur)
    roar = S.bandpass(S.osc("noise", 0, dur, seed=SEED + 89), S.sweep(dur, 1800, 300, 0.5), 0.8)
    roar = S.drive(S.env_mul(roar, S.expdecay(n, 3.5)), 2.2)
    v = S.layer([(boom(0.9, 0.8, seed=90), 0.0, 0.9), (roar, 0.0, 0.9),
                 (whoosh(0.4, 400, 2200, 0.8, seed=91, peak_at=0.1), 0.0, 0.6)])
    return S.reverb(S.pad(v, 1.3), size=0.5, damp=0.5, mix=0.15)


def burn():
    """One tick of burning: a small crackle."""
    dur = 0.16
    crackle = S.gate(S.highpass(S.osc("noise", 0, dur, seed=SEED + 92), 1800), 120.0, 0.35, seed=SEED + 93)
    return S.env_mul(crackle, S.ad(dur, 0.002, 2.0))


def creak():
    """Rusty joints seizing after a miss: a slow, squeaky groan that bends down in pitch."""
    dur = 0.34
    squeal = S.fm("saw", S.sweep(dur, 640, 420, 0.8), 31.0, 0.05, dur)
    squeal = S.bandpass(squeal, 1300, 2.2)
    return S.env_mul(squeal, S.adsr(dur, 0.03, 0.05, 0.75, 0.12))


def clang():
    """A small hit bouncing off a hard hat: one bright ring of steel, short, with a tick on top."""
    dur = 0.5
    ring = metal(1180, dur, k=9.0)
    tick = slap(0.05, cutoff=3500.0, k=60.0, seed=61)
    return S.layer([(ring, 0.0, 1.0), (tick, 0.0, 0.5)])


def minibot_pop():
    """The MiniBot going off: a little boom, pitched up, with a clank of loose parts after it."""
    dur = 0.45
    n = S.n_samples(dur)
    air = S.lowpass(S.osc("noise", 0, dur, seed=SEED + 63), S.sweep(dur, 5200, 600, 0.5))
    pop = S.mix(S.gain(thump(dur, 220, 70, 7.0), 0.9), S.gain(S.env_mul(air, S.expdecay(n, 9.0)), 0.7))
    return S.layer([(pop, 0.0, 1.0), (metal(880, 0.2, k=14.0), 0.07, 0.35),
                    (metal(1240, 0.18, k=16.0), 0.13, 0.25)])


# ---------------------------------------------------------------------------
# REGISTRY - (name, category, peak target dBFS, builder, loops)
#
# The targets ARE the mix: the game plays every file at unity. Loudness follows how often a
# sound fires, not how important it feels - see .ai/audio-direction.md.
# ---------------------------------------------------------------------------

REGISTRY = [
    ("swing_light", "swing", -14.0, swing_light, False),
    ("swing_medium", "swing", -12.5, swing_medium, False),
    ("swing_heavy", "swing", -11.0, swing_heavy, False),
    ("swing_blade", "swing", -12.0, swing_blade, False),
    ("smash_charge", "swing", -17.0, smash_charge, True),
    ("taunt", "swing", -12.0, taunt, False),

    ("special_shot", "special", -13.0, special_shot, False),
    ("special_volley", "special", -12.0, special_volley, False),
    ("special_fire", "special", -11.0, special_fire, False),
    ("special_beam", "special", -9.0, special_beam, False),
    ("special_dash", "special", -11.0, special_dash, False),
    ("special_glint", "special", -10.0, special_glint, False),
    ("special_blink", "special", -9.0, special_blink, False),
    ("special_rise", "special", -12.0, special_rise, False),
    ("special_wings", "special", -12.0, special_wings, False),
    ("special_hook", "special", -11.0, special_hook, False),
    ("special_trap", "special", -11.0, special_trap, False),
    ("special_drop", "special", -13.0, special_drop, False),
    ("special_resize", "special", -12.0, special_resize, False),
    ("special_quake", "special", -6.0, special_quake, False),
    ("special_build", "special", -11.0, special_build, False),
    ("bomb_fuse", "special", -15.0, bomb_fuse, False),
    ("explosion", "special", -4.0, explosion, False),

    ("hit_light", "hit", -10.0, hit_light, False),
    ("hit_medium", "hit", -8.0, hit_medium, False),
    ("hit_heavy", "hit", -6.0, hit_heavy, False),
    ("hit_smash", "hit", -4.0, hit_smash, False),
    ("hit_block", "hit", -11.0, hit_block, False),

    ("count_tick", "match", -8.0, count_tick, False),
    ("count_go", "match", -5.0, count_go, False),
    ("ko_blast", "match", -3.0, ko_blast, False),
    ("respawn", "match", -13.0, respawn, False),
    ("game_set", "match", -4.0, game_set, False),

    ("ui_move", "ui", -21.0, ui_move, False),
    ("ui_select", "ui", -13.0, ui_select, False),
    ("ui_back", "ui", -14.0, ui_back, False),
    ("ui_ready", "ui", -11.0, ui_ready, False),
    ("pause", "ui", -12.0, pause, False),

    ("zap", "special", -10.0, zap, False),
    ("jets", "special", -11.0, jets, False),
    ("missile", "special", -13.0, missile, False),
    ("steam", "special", -12.0, steam, False),
    ("overheat", "special", -7.0, overheat, False),
    ("furnace_blast", "special", -4.0, furnace_blast, False),
    ("burn", "hit", -20.0, burn, False),
    ("creak", "swing", -14.0, creak, False),
    ("clang", "hit", -10.0, clang, False),
    ("minibot_pop", "special", -9.0, minibot_pop, False),
]
