"""Render an original slow melodic background loop; requires numpy only."""
from pathlib import Path
import wave
import numpy as np

RATE = 44100
BPM = 68
BEAT = 60 / BPM
COUNT = round(64 * BEAT * RATE)
mix = np.zeros((COUNT, 2))
rng = np.random.default_rng(68027)


def add(sound, beat, gain, pan=0):
    indices = (round(beat * BEAT * RATE) + np.arange(len(sound))) % COUNT
    for channel, level in enumerate(np.sqrt([(1 - pan) / 2, (1 + pan) / 2])):
        np.add.at(mix[:, channel], indices, sound * gain * level)


def note(midi, seconds, pad=False):
    t = np.arange(round(seconds * RATE)) / RATE
    f = 440 * 2 ** ((midi - 69) / 12)
    if pad:
        sound = np.sin(2 * np.pi * f * t) + .18 * np.sin(2 * np.pi * f * 1.002 * t)
        envelope = np.sin(np.pi * np.minimum(t / seconds, 1)) ** 2
    else:
        sound = np.zeros_like(t)
        for h, strength in enumerate((1, .32, .16, .065, .025), 1):
            sound += strength * np.cos(2 * np.pi * f * h * t) * np.exp(-t * (.75 + .38 * h))
        envelope = (1 - np.exp(-t * 160)) * np.minimum(1, (seconds - t) / .18)
    return sound * envelope


# Em7 / Cmaj7 / Gadd9 / Dsus2: two-bar chords leave space for a singing melody.
chords = ((52, 55, 59, 62), (48, 55, 59, 64), (43, 55, 59, 62), (50, 57, 62, 64))
melodies = (
    ((0, 71, 1.5), (2, 67, 1), (3.5, 66, 1.5), (5.5, 64, 2)),
    ((0, 67, 2), (2.5, 71, 1), (4, 69, 1.5), (6, 67, 1.5)),
    ((.5, 74, 2), (3, 71, 1.5), (5, 69, 1), (6.5, 67, 1)),
    ((0, 66, 2), (2.5, 69, 1.5), (4.5, 66, 1), (6, 64, 1.5)),
)
for phrase in range(8):
    chord = chords[phrase % 4]
    at = phrase * 8
    for i, pitch in enumerate(chord):
        add(note(pitch, 10 * BEAT, True), at - 1, .045, (i - 1.5) * .3)
    for beat in range(8):
        pitch = chord[(0, 2, 1, 3, 0, 2, 3, 1)[beat]]
        add(note(pitch + 12, 2.8), at + beat + .025, .11, -.35)
    for beat in (0, 4):
        add(note(chord[0] - 12, 3.4), at + beat, .18)
    for offset, pitch, duration in melodies[phrase % 4]:
        # Small answer-phrase variation on the second pass.
        if phrase >= 4 and offset >= 6:
            pitch = 67 if phrase == 7 else pitch + 2
        add(note(pitch, (duration + .8) * BEAT), at + offset, .2, .2)
    for beat in (0, 4):
        t = np.arange(round(.25 * RATE)) / RATE
        kick = np.sin(2 * np.pi * (48 * t + .8 * (1 - np.exp(-t * 30))))
        kick *= np.exp(-t * 24) * (1 - np.exp(-t * 400))
        add(kick, at + beat, .12)
    for beat in (2, 6):
        t = np.arange(round(.15 * RATE)) / RATE
        brush = np.convolve(rng.normal(size=len(t)), np.ones(12) / 12, mode='same')
        brush *= np.exp(-t * 35) * (1 - np.exp(-t * 500))
        add(brush, at + beat, .09, -.15)

# Wrapped delays keep the ambience continuous at the loop boundary.
dry = mix.copy()
for seconds, gain in ((BEAT * .75, .16), (BEAT * 1.5, .08), (.071, .055)):
    mix += gain * np.roll(dry[:, ::-1], round(seconds * RATE), axis=0)
mix -= mix.mean(axis=0)
mix *= .72 / np.max(np.abs(mix))
mix[:128] *= np.linspace(0, 1, 128)[:, None]
mix[-128:] *= np.linspace(1, 0, 128)[:, None]
assert np.isfinite(mix).all() and np.max(np.abs(mix)) < 1
output = Path(__file__).resolve().parents[1] / 'Assets/Resources/PhoenixAudio/phoenix-relaxed.wav'
with wave.open(str(output), 'wb') as wav:
    wav.setparams((2, 2, RATE, COUNT, 'NONE', 'not compressed'))
    wav.writeframes(np.rint(mix * 32767).astype('<i2').tobytes())
print(f'{output}: {COUNT / RATE:.2f}s, {BPM} BPM, peak {np.max(np.abs(mix)):.2f}')
