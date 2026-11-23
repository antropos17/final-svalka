from pathlib import Path
import wave

import numpy as np
from scipy.signal import butter, sosfilt

RATE = 48000
ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / '.work/slot-large-win-audio-20261115/exports'
OUT.mkdir(parents=True, exist_ok=True)
RNG = np.random.default_rng(3067)


def time(seconds):
    return np.arange(round(seconds * RATE)) / RATE


def noise(samples, low, high):
    bands = butter(2, (low, high), btype='bandpass', fs=RATE, output='sos')
    return sosfilt(bands, RNG.standard_normal(samples))


def add(track, sound, seconds):
    start = round(seconds * RATE)
    size = min(len(sound), len(track) - start)
    track[start:start + size] += sound[:size]


def save(name, signal, peak, fade=.008):
    n = round(fade * RATE)
    signal[-n:] *= np.linspace(1, 0, n)
    signal *= peak / np.max(np.abs(signal))
    pcm = np.round(signal * 32767).astype('<i2')
    with wave.open(str(OUT / (name + '.wav')), 'wb') as dst:
        dst.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        dst.writeframes(pcm.tobytes())


# 16 light ratchet teeth over a periodic motor bed. The end joins the start.
t = time(.8)
roll = .025 * np.sin(2 * np.pi * 85 * t) + .012 * np.sin(2 * np.pi * 170 * t)
eu = time(.034)
for i in range(16):
    tooth = (.38 * noise(len(eu), 650, 3500) + .11 * np.sin(2 * np.pi * (720 + 45 * (i % 4)) * eu))
    tooth *= np.minimum(eu / .001, 1) * np.exp(-eu / .007)
    add(roll, tooth * (.78 + .22 * (i % 2)), .008 + .05 * i)
save('reel-roll', roll, .8, .004)

# Close, short pawl-on-wood snap, with a small higher-frequency latch detail.
t = time(.072)
seat = np.exp(-t / .009) * (.55 * noise(len(t), 700, 4900) + .32 * np.sin(2 * np.pi * 630 * t))
seat += .44 * np.sin(2 * np.pi * 225 * t) * np.exp(-t / .015)
e = np.maximum(t - .008, 0)
seat += .16 * (t >= .008) * np.sin(2 * np.pi * 1460 * e) * np.exp(-e / .009)
seat *= np.minimum(t / .0014, 1)
save('reel-seat', seat, .8)

# Single quiet brass contact. The game plays one shot per landing.
t = time(.052)
coin = .27 * noise(len(t), 2500, 9200) * np.exp(-t / .0025)
for ratio, amplitude, decay in ((1, .55, .022), (1.467, .24, .013), (2.196, .12, .011), (3.12, .05, .008)):
    coin += amplitude * np.sin(2 * np.pi * 1210 * ratio * t) * np.exp(-t / decay)
coin *= np.minimum(t / .0015, 1)
save('coin-credit', coin, .8, .012)

# A compact pitched thump with short brass overtones for the large-win flash.
t = time(.224)
phase = 2 * np.pi * (92 * t + 58 * .034 * (1 - np.exp(-t / .034)))
impact = .72 * np.sin(phase) * np.exp(-t / .047)
impact += .36 * np.sin(phase * 2) * np.exp(-t / .037)
impact += .18 * noise(len(t), 480, 3400) * np.exp(-t / .010)
for freq, amplitude, decay in ((620, .18, .049), (1048, .13, .040), (1670, .07, .026)):
    impact += amplitude * np.sin(2 * np.pi * freq * t) * np.exp(-t / decay)
impact *= np.minimum(t / .002, 1)
save('large-win-impact', impact, .82, .024)
