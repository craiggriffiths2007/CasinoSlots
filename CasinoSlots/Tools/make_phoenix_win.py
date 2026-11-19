"""Render the Phoenix win arpeggio with a warm, plucked-string harp tone.

Run with Python and numpy. The pitches are one octave below the original
E5/G#5/B5/E6 cue; the 0.85-second cue length and ascending rhythm are retained.
"""
from pathlib import Path
import wave

import numpy as np


RATE = 44100
DURATION = 0.85
OUTPUT = Path(__file__).resolve().parents[1] / 'Assets/Resources/PhoenixAudio/p2-win.wav'


def render():
    rng = np.random.default_rng(2719)
    count = round(RATE * DURATION)
    audio = np.zeros((count, 2))
    for index, midi in enumerate((64, 68, 71, 76)):
        start = round(index * 0.085 * RATE)
        t = np.arange(count - start) / RATE
        frequency = 440 * 2 ** ((midi - 69) / 12)
        string = np.zeros_like(t)
        # A pluck away from the centre excites uneven harmonics. Upper modes
        # decay faster than the fundamental, as they do on a damped string.
        for harmonic in range(1, 15):
            amplitude = np.sin(np.pi * harmonic * 0.23) / harmonic ** 1.65
            decay = 0.48 / (1 + 0.22 * (harmonic - 1))
            mode = frequency * harmonic * np.sqrt(1 + 0.000015 * harmonic ** 2)
            string += amplitude * np.cos(2 * np.pi * mode * t) * np.exp(-t / decay)
        string *= 1 - np.exp(-t / 0.0018)
        # Quiet finger contact and soundboard modes add an acoustic attack.
        contact = rng.standard_normal(len(t))
        contact = np.convolve(contact, np.ones(9) / 9, mode='same')
        string += 0.025 * contact * np.exp(-t / 0.008) * (1 - np.exp(-t / 0.0005))
        for mode in (185, 280, 420):
            string += 0.018 * np.sin(2 * np.pi * mode * t) * np.exp(-t / 0.065)
        pan = -0.16 + index * 0.105
        audio[start:, 0] += string * np.sqrt((1 - pan) / 2)
        audio[start:, 1] += string * np.sqrt((1 + pan) / 2)

    # Subtle early room reflections, with no change to playback speed.
    dry = audio.copy()
    for delay, gain in ((0.019, 0.09), (0.031, 0.065), (0.047, 0.04)):
        offset = round(delay * RATE)
        audio[offset:] += gain * dry[:-offset, ::-1]
    tail = round(0.16 * RATE)
    audio[-tail:] *= (0.5 + 0.5 * np.cos(np.linspace(0, np.pi, tail)))[:, None]
    audio *= 0.78 / np.max(np.abs(audio))
    pcm = np.rint(audio * 32767).astype('<i2')
    assert np.isfinite(audio).all() and np.max(np.abs(audio)) < 1
    with wave.open(str(OUTPUT), 'wb') as clip:
        clip.setnchannels(2)
        clip.setsampwidth(2)
        clip.setframerate(RATE)
        clip.writeframes(pcm.tobytes())
    print(f'Wrote {OUTPUT}: {DURATION}s, stereo {RATE} Hz, E4/G#4/B4/E5')


if __name__ == '__main__':
    render()
