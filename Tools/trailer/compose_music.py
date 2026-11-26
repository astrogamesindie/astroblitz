# Astro Blitz trailer - original music, synthesised here (no third-party audio, no licence to clear).
#
# 128 BPM, A minor, exactly 64 beats = 30 s, laid out on the trailer's edit:
#   b0-4   intro (menu)             pad + filtered arp
#   b4-8   build (space jump)       riser, snare roll, kick speeding up
#   b8-32  DROP 1 (gameplay)        kick, clap, hats, pumping bass, supersaws, arp, lead from b16
#   b32-40 boss                     siren, dark drone, half-time drums, then a build
#   b40-52 DROP 2 (boss down)       everything + lead
#   b52-64 end card                 big hit, last chords, final hit at b60 and the tail
# Plus an "edit" stem: whooshes on the cuts, sub booms and reverse swells into the drops.
#
# Run: python Tools/trailer/compose_music.py  ->  Trailer/build/music.wav (48 kHz stereo)

import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, fftconvolve, sosfilt

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, 'Trailer', 'build', 'music.wav')

SR = 48000
BPM = 128.0
B = 60.0 / BPM
BEATS = 64
LENGTH = BEATS * B
N = int(round(LENGTH * SR))
rng = np.random.default_rng(7)

# Cuts of the edit (beats) and their kind, shared with compose_trailer.py.
CUTS = {4: 'zoom', 8: 'drop', 15: 'glitch', 22: 'whip', 28: 'stripe', 32: 'boss', 40: 'drop', 43: 'zoom',
        48: 'whip', 50: 'whip', 52: 'drop'}


def midi(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


def at(beat):
    return int(round(beat * B * SR))


def lp(x, hz, order=2):
    return sosfilt(butter(order, min(hz, SR * 0.45), 'low', fs=SR, output='sos'), x)


def hp(x, hz, order=2):
    return sosfilt(butter(order, hz, 'high', fs=SR, output='sos'), x)


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, min(hi, SR * 0.45)], 'band', fs=SR, output='sos'), x)


def sweep(x, start_hz, end_hz, kind='low', block=512, curve=2.0):
    """Time-varying filter (blockwise), for risers and filter opens."""
    out = np.zeros_like(x)
    n = len(x)
    zi = None
    for i in range(0, n, block):
        k = (i / max(1, n - 1)) ** curve
        hz = start_hz * (end_hz / start_hz) ** k
        if kind == 'band':
            sos = butter(2, [hz * 0.7, min(hz * 1.4, SR * 0.45)], 'band', fs=SR, output='sos')
        else:
            sos = butter(2, min(hz, SR * 0.45), kind, fs=SR, output='sos')
        if zi is None or zi.shape[0] != sos.shape[0]:
            zi = np.zeros((sos.shape[0], 2))
        out[i:i + block], zi = sosfilt(sos, x[i:i + block], zi=zi)
    return out


def env(n, attack=0.005, decay=0.1, sustain=0.0, release=0.05, hold=None):
    """ADSR over n samples (hold = samples before release)."""
    t = np.arange(n) / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    d = sustain + (1 - sustain) * np.exp(-np.maximum(0, t - attack) / max(decay, 1e-4))
    e = np.where(t < attack, a, d)
    if hold is not None and hold < n:
        r = np.exp(-np.arange(n - hold) / SR / max(release, 1e-4))
        e[hold:] = e[hold - 1] * r
    return e


def saw(f, n, phase=None):
    t = np.arange(n) / SR
    p = rng.random() if phase is None else phase
    return 2.0 * ((f * t + p) % 1.0) - 1.0


def add(buf, start, sig):
    if start >= len(buf):
        return
    end = min(len(buf), start + len(sig))
    if start < 0:
        sig = sig[-start:]
        start = 0
        end = min(len(buf), len(sig))
    buf[start:end] += sig[:end - start]


def stereo(n):
    return np.zeros((n, 2))


def pan(sig, p):
    """p in [-1, 1]."""
    l = np.cos((p + 1) * np.pi / 4)
    r = np.sin((p + 1) * np.pi / 4)
    return np.stack([sig * l, sig * r], axis=1)


# ----------------------------------------------------------------------
# Instruments
# ----------------------------------------------------------------------

def kick(length=0.45, punch=1.0):
    n = int(length * SR)
    t = np.arange(n) / SR
    f = 52 + 125 * np.exp(-t / 0.03)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.2)
    click = hp(rng.standard_normal(n), 2500) * np.exp(-t / 0.004) * 0.35
    return np.tanh((body + click) * 1.6 * punch) * 0.9


def clap():
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    e = np.zeros(n)
    for off in (0.0, 0.011, 0.022):
        e += np.exp(-np.maximum(0, t - off) / 0.007) * (t >= off)
    e += np.exp(-np.maximum(0, t - 0.03) / 0.13) * (t >= 0.03) * 0.6
    return bp(noise, 900, 5200) * e * 0.55


def snare(level=1.0):
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    tone = np.sin(2 * np.pi * 190 * t) * np.exp(-t / 0.05)
    noise = bp(rng.standard_normal(n), 1200, 9000) * np.exp(-t / 0.09)
    return (tone * 0.5 + noise * 0.7) * level


def hat(open_=False, level=1.0):
    n = int((0.25 if open_ else 0.06) * SR)
    t = np.arange(n) / SR
    noise = hp(rng.standard_normal(n), 7500, 4)
    return noise * np.exp(-t / (0.12 if open_ else 0.018)) * 0.32 * level


def crash(length=2.2, level=1.0):
    n = int(length * SR)
    t = np.arange(n) / SR
    noise = hp(rng.standard_normal(n), 4200, 2)
    metal = sum(np.sign(np.sin(2 * np.pi * f * t)) for f in (523.0, 787.0, 1093.0, 1471.0)) * 0.05
    return (noise * 0.55 + hp(metal, 3000)) * np.exp(-t / 0.75) * level * 0.6


def supersaw(notes, n, cutoff=6500, voices=7, detune=0.16, level=0.12):
    out = stereo(n)
    for m in notes:
        for v in range(voices):
            d = (v - (voices - 1) / 2) / ((voices - 1) / 2) * detune
            s = saw(midi(m + d), n)
            out += pan(s, (v / (voices - 1)) * 1.6 - 0.8)
    out[:, 0] = lp(out[:, 0], cutoff)
    out[:, 1] = lp(out[:, 1], cutoff)
    return out * level / (len(notes) * voices) ** 0.5


def pluck(m, length, cutoff=5200, level=0.25):
    n = int(length * SR)
    t = np.arange(n) / SR
    s = saw(midi(m), n) * 0.7 + saw(midi(m + 12.04), n) * 0.3
    # Filter envelope: bright attack, closing fast.
    blocks = [s[i:i + 256] for i in range(0, n, 256)]
    out = np.zeros(n)
    zi = np.zeros((1, 2))
    for k, blk in enumerate(blocks):
        tt = k * 256 / SR
        hz = 350 + cutoff * np.exp(-tt / 0.07)
        sos = butter(2, min(hz, SR * 0.45), 'low', fs=SR, output='sos')
        y, zi = sosfilt(sos, blk, zi=zi)
        out[k * 256:k * 256 + len(blk)] = y
    return out * np.exp(-t / 0.16) * level


def lead_note(m, length, level=0.2):
    n = int(length * SR)
    t = np.arange(n) / SR
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.5 * t) * np.clip((t - 0.12) / 0.2, 0, 1)
    f = midi(m) * vib
    ph = np.cumsum(f) / SR
    s = (2 * ((ph + 0.1) % 1) - 1) * 0.5 + (2 * ((ph * 1.005 + 0.4) % 1) - 1) * 0.5
    s += np.sign(np.sin(2 * np.pi * ph * 0.5)) * 0.15
    s = lp(s, 4200)
    e = env(n, 0.01, 0.4, 0.75, 0.08, hold=max(1, n - int(0.08 * SR)))
    return s * e * level


def bass_note(m, length, level=0.5):
    n = int(length * SR)
    t = np.arange(n) / SR
    s = saw(midi(m), n, 0.0) * 0.75 + np.sin(2 * np.pi * midi(m - 12) * t) * 0.55
    s = lp(s, 1100, 2)
    return np.tanh(s * 1.4) * env(n, 0.004, 0.25, 0.6, 0.03, hold=max(1, n - int(0.03 * SR))) * level


def pad(notes, n, cutoff=1800, level=0.1):
    out = stereo(n)
    t = np.arange(n) / SR
    for m in notes:
        for d, p in ((-0.1, -0.6), (0.1, 0.6), (0.0, 0.0)):
            out += pan(saw(midi(m + d), n), p)
    out[:, 0] = lp(out[:, 0], cutoff)
    out[:, 1] = lp(out[:, 1], cutoff)
    fade = np.clip(t / 0.4, 0, 1) * np.clip((n / SR - t) / 0.3, 0, 1)
    return out * fade[:, None] * level / len(notes) ** 0.5


def riser(beats, top=6000.0, level=0.35):
    n = int(beats * B * SR)
    t = np.arange(n) / SR
    noise = sweep(rng.standard_normal(n), 300, top, 'band', curve=1.6)
    f = 180 * (2400 / 180) ** ((t / t[-1]) ** 1.8)
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.25
    e = (t / t[-1]) ** 2.2
    return (noise * 0.8 + tone) * e * level


def reverse_swell(beats=1.0, level=0.5):
    n = int(beats * B * SR)
    return crash(beats * B + 0.01, 1.0)[:n][::-1] * level


def boom(level=1.0):
    n = int(2.6 * SR)
    t = np.arange(n) / SR
    f = 30 + 70 * np.exp(-t / 0.18)
    sub = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.7) * 0.8
    hit = lp(rng.standard_normal(n), 1800) * np.exp(-t / 0.12) * 0.7
    return np.tanh((sub * 1.2 + hit) * 1.3) * level


def whoosh(length=0.42, up=True, level=0.45):
    n = int(length * SR)
    t = np.arange(n) / SR
    x = sweep(rng.standard_normal(n), 400 if up else 5000, 5000 if up else 400, 'band', curve=1.0)
    bell = np.sin(np.pi * np.clip(t / length, 0, 1)) ** 1.5
    return x * bell * level


def siren(beats=2.0, level=0.18):
    n = int(beats * B * SR)
    t = np.arange(n) / SR
    f = np.where((t / (B / 2)).astype(int) % 2 == 0, midi(81), midi(75))
    s = np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR))
    return lp(s, 2600) * level * np.clip((n / SR - t) / 0.2, 0, 1)


def zap(level=0.4):
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    f = 2200 * np.exp(-t / 0.08) + 90
    s = saw(1, n) * 0
    s = np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)) * np.exp(-t / 0.18)
    return lp(s, 6000) * level


# ----------------------------------------------------------------------
# Arrangement
# ----------------------------------------------------------------------

CHORDS = {
    'Am': ([57, 60, 64, 69], 45), 'F': ([53, 57, 60, 65], 41), 'C': ([55, 60, 64, 67], 48),
    'G': ([55, 59, 62, 67], 43), 'E': ([56, 59, 64, 68], 40), 'Dk': ([57, 60, 63, 66], 45),
}
BARS = ['Am', 'F', 'Am', 'F', 'C', 'G', 'Am', 'F', 'Dk', 'E', 'Am', 'F', 'G', 'Am', 'F', 'Am']
DROPS = [(8, 32), (40, 52), (52, 60)]
LEAD = {
    'Am': [(0, 81, 0.5), (0.75, 84, 0.5), (1.5, 88, 0.75), (2.5, 86, 0.5), (3, 84, 0.5), (3.5, 83, 0.5)],
    'F': [(0, 81, 0.5), (0.75, 84, 0.5), (1.5, 89, 0.75), (2.5, 88, 0.5), (3, 84, 0.5), (3.5, 81, 0.5)],
    'C': [(0, 79, 0.5), (0.75, 84, 0.5), (1.5, 88, 0.75), (2.5, 86, 0.5), (3, 84, 0.5), (3.5, 79, 0.5)],
    'G': [(0, 83, 0.5), (0.75, 86, 0.5), (1.5, 91, 0.75), (2.5, 88, 0.5), (3, 86, 0.5), (3.5, 83, 0.5)],
}


def in_drop(b):
    return any(s <= b < e for s, e in DROPS)


def build():
    drums, bass, chords, arp, lead, fx, padbus = (stereo(N) for _ in range(7))
    kicks = []

    def k_at(beat, punch=1.0, level=1.0):
        add(drums, at(beat), pan(kick(punch=punch) * level, 0))
        kicks.append(beat)

    # --- intro / build ---
    for i in range(16):
        add(drums, at(i * 0.25), pan(hat(level=0.35 + 0.02 * i), 0.3))
    for i in range(16, 32):
        add(drums, at(i * 0.25), pan(hat(level=0.5), 0.3 if i % 2 else -0.3))
    for b in (4, 5, 6):
        k_at(b, 0.9, 0.8)
    for b in (6.5, 7, 7.25, 7.5):
        k_at(b, 0.8, 0.7)
    roll = [4 + i * 0.5 for i in range(4)] + [6 + i * 0.25 for i in range(4)] + [7 + i * 0.125 for i in range(6)]
    for i, b in enumerate(roll):
        add(drums, at(b), pan(snare(0.25 + 0.75 * i / len(roll)), 0))
    add(fx, at(4), pan(riser(3.75, 7000, 0.4), 0))
    add(fx, at(7), pan(reverse_swell(1.0, 0.6), 0))

    # --- drop drums ---
    for b in range(8, 60):
        if b in range(32, 40):
            continue
        if not in_drop(b):
            continue
        k_at(b, 1.0, 1.0)
        if b % 2 == 1:
            add(drums, at(b), pan(clap(), 0))
        add(drums, at(b + 0.5), pan(hat(True, 0.8), 0.2))
        for s in (0.25, 0.75):
            add(drums, at(b + s), pan(hat(False, 0.55), -0.25))
    for b in (8, 24, 40, 52):
        add(drums, at(b), pan(crash(2.4, 1.0), 0.15))
    # fill into the boss
    for i, b in enumerate([31 + i * 0.125 for i in range(8)]):
        add(drums, at(b), pan(snare(0.4 + 0.08 * i), 0))

    # --- boss: half time, siren, build ---
    k_at(32, 1.3, 1.2)
    add(fx, at(32), pan(boom(0.8), 0))
    add(fx, at(32), pan(siren(2.0), 0.2))
    for b in (34, 35.5, 36, 37.5):
        k_at(b, 1.0, 0.9)
        add(drums, at(b + 1), pan(clap() * 0.9, 0))
    for b in [36 + i * 0.5 for i in range(4)] + [38 + i * 0.25 for i in range(4)]:
        add(drums, at(b), pan(snare(0.55), 0))
    add(fx, at(36), pan(riser(3.0, 8000, 0.45), 0))
    add(fx, at(39), pan(reverse_swell(1.0, 0.7), 0))
    drone = stereo(at(8))
    t = np.arange(at(8)) / SR
    dsig = lp(saw(midi(33), at(8)) + saw(midi(33.08), at(8)) + 0.5 * saw(midi(39), at(8)), 500) * 0.22
    dsig *= np.clip(t / 0.3, 0, 1) * (0.8 + 0.2 * np.sin(2 * np.pi * t / (B * 2)))
    add(padbus, at(32), pan(dsig, 0))

    # --- final hit ---
    k_at(60, 1.3, 1.2)
    add(drums, at(60), pan(crash(4.0, 1.2), 0))
    add(fx, at(60), pan(boom(0.9), 0))

    # --- harmony ---
    for bar, name in enumerate(BARS):
        notes, root = CHORDS[name]
        b0 = bar * 4
        if bar < 2:
            add(padbus, at(b0), pad(notes, at(4) + int(0.3 * SR), 1200 + 1800 * bar, 0.16))
        elif bar in (8,):
            continue
        elif bar == 9:
            add(padbus, at(b0), pad(notes, at(4), 2400, 0.14))
            for i in range(8):
                add(bass, at(b0 + i * 0.5), pan(bass_note(root + 12 * (i % 2), B * 0.45, 0.45), 0))
        elif bar == 15:
            add(chords, at(b0), supersaw(notes + [notes[0] + 12], at(4), 7500, level=0.32) *
                np.exp(-np.arange(at(4)) / SR / 1.2)[:, None])
            add(bass, at(b0), pan(bass_note(root, B * 3.5, 0.55), 0))
        else:
            dur = at(4)
            add(chords, at(b0), supersaw(notes, dur, 5600 if bar < 10 else 7500, level=0.3))
            for i in range(8):
                add(bass, at(b0 + i * 0.5), pan(bass_note(root + 12 * (i % 2), B * 0.45), 0))
        # arpeggio
        if bar in (8, 15):
            continue
        order = [0, 1, 2, 3, 2, 1, 3, 2]
        arp_notes = [n + 12 for n in notes]
        for i in range(16):
            m = arp_notes[order[i % 8]] + (12 if i % 8 >= 6 else 0)
            cutoff = 900 + 300 * i if bar == 0 else (2500 + 600 * i if bar == 1 else 5200)
            add(arp, at(b0 + i * 0.25), pan(pluck(m, B * 0.6, cutoff, 0.2 if bar < 2 else 0.17), 0.35 if i % 2 else -0.35))
        # lead
        if name in LEAD and (bar >= 10 or 4 <= bar <= 7) and bar not in (13, 14, 15):
            level = 0.26 if bar >= 10 else 0.17
            for off, m, d in LEAD[name]:
                add(lead, at(b0 + off), pan(lead_note(m, d * B, level), 0.05))
        if bar in (13, 14):
            for off, m, d in [(0, 88, 1.0), (1, 84, 1.0), (2, 81, 2.0)]:
                add(lead, at(b0 + off), pan(lead_note(m if bar == 13 else m - 3, d * B, 0.24), 0.05))

    # --- edit stem: cuts, drops, special ---
    for cut, kind in CUTS.items():
        if kind in ('zoom', 'whip', 'glitch', 'stripe'):
            add(fx, at(cut) - int(0.24 * SR), pan(whoosh(0.42, True, 0.4), 0.4 if kind == 'whip' else 0))
        if kind == 'drop':
            add(fx, at(cut), pan(boom(1.0), 0))
    add(fx, at(29), pan(zap(0.35), 0))

    # --- sidechain pump from the kicks ---
    duck = np.ones(N)
    tt = np.arange(int(0.3 * SR)) / SR
    shape = 1 - 0.75 * np.exp(-tt / 0.07)
    for b in kicks:
        s = at(b)
        e = min(N, s + len(shape))
        duck[s:e] = np.minimum(duck[s:e], shape[:e - s])
    for bus in (bass, chords, arp, padbus):
        bus *= duck[:, None]
    lead *= (0.55 + 0.45 * duck)[:, None]

    # --- space: reverb and a dotted-eighth delay on the arp ---
    delay = int(0.75 * B * SR)
    echo = np.zeros_like(arp)
    echo[delay:] += arp[:-delay] * 0.35
    echo[2 * delay:] += arp[:-2 * delay] * 0.15
    arp += echo[:, ::-1]

    ir_n = int(2.2 * SR)
    ti = np.arange(ir_n) / SR
    ir = np.stack([hp(rng.standard_normal(ir_n), 300) * np.exp(-ti / 0.5) for _ in range(2)], axis=1)
    ir /= np.sqrt((ir ** 2).sum(axis=0))
    send = drums * 0.06 + lead * 0.35 + arp * 0.25 + fx * 0.25 + chords * 0.15
    wet = np.stack([fftconvolve(send[:, c], ir[:, c])[:N] for c in range(2)], axis=1)

    mix = drums * 0.8 + bass * 0.5 + chords * 1.0 + arp * 0.8 + lead * 1.0 + padbus * 1.0 + fx * 0.85 + wet * 0.6
    for c in range(2):
        mix[:, c] = hp(mix[:, c], 30)
        # presence: phone speakers live here
        mix[:, c] += bp(mix[:, c], 1800, 6500) * 0.9 + bp(mix[:, c], 350, 1600) * 0.35
    # gentle glue + soft clip, then fade the very end
    mix = np.tanh(mix * 1.15) / np.tanh(1.15)
    t = np.arange(N) / SR
    mix *= np.clip((LENGTH - t) / 0.6, 0, 1)[:, None]
    peak = np.abs(mix).max()
    return mix / peak * 0.89


def main():
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    mix = build()
    wavfile.write(OUT, SR, (mix * 32767).astype(np.int16))
    print('wrote', OUT, '%.2f s' % (len(mix) / SR))


if __name__ == '__main__':
    main()
