"""Generate an original instrumental grunge-metal loop using numpy synthesis."""
from pathlib import Path
import wave
import numpy as np

RATE = 44100
BPM = 96
BEAT = 60 / BPM
LENGTH = round(32 * BEAT * RATE)
rng = np.random.default_rng(92726)
mix = np.zeros((LENGTH, 2))


def filter_band(signal, low, high):
    frequencies = np.fft.rfftfreq(len(signal), 1 / RATE)
    response = (1 - np.exp(-(frequencies / low) ** 4)) / (1 + (frequencies / high) ** 6)
    return np.fft.irfft(np.fft.rfft(signal) * response, len(signal))


def add(signal, beat, gain, pan=0):
    start = round(beat * BEAT * RATE)
    indices = (start + np.arange(len(signal))) % LENGTH
    gains = np.sqrt([(1 - pan) / 2, (1 + pan) / 2])
    for channel in range(2):
        np.add.at(mix[:, channel], indices, signal * gain * gains[channel])


def guitar(midi, duration, muted, detune):
    t = np.arange(round(duration * RATE)) / RATE
    chord = np.zeros_like(t)
    for semitones, strength in ((0, 1), (7, .72), (12, .45)):
        frequency = 440 * 2 ** ((midi + semitones - 69 + detune) / 12)
        string = np.zeros_like(t)
        # Plucked-string modes with a pick-position notch and damped overtones.
        for h in range(1, 27):
            amplitude = np.sin(h * np.pi * .19) / h ** 1.25
            phase = 2 * np.pi * frequency * h * t + rng.uniform(-.15, .15)
            string += amplitude * np.cos(phase) * np.exp(-t * (2 + h * .23))
        chord += strength * string
    attack = 1 - np.exp(-t * 1200)
    envelope = attack * np.exp(-t * (16 if muted else 1.8))
    chord += .025 * rng.normal(size=len(t)) * np.exp(-t * 95)
    distorted = np.tanh(7 * chord * envelope)
    distorted *= np.minimum(1, (duration - t) / .025)
    # Cabinet roll-off removes the harsh digital high end.
    return filter_band(distorted, 75, 3300)


def drum(kind):
    seconds = {'kick': .32, 'snare': .26, 'hat': .085, 'open': .32, 'crash': 1.7}[kind]
    t = np.arange(round(seconds * RATE)) / RATE
    noise = rng.normal(size=len(t))
    if kind == 'kick':
        signal = np.sin(2 * np.pi * (48 * t + 5 * (1 - np.exp(-t * 30)))) * np.exp(-t * 15)
        signal += .12 * noise * np.exp(-t * 220)
    elif kind == 'snare':
        signal = .65 * filter_band(noise, 700, 9500) * np.exp(-t * 20)
        signal += .45 * (np.sin(2 * np.pi * 182 * t) + .3 * np.sin(2 * np.pi * 327 * t)) * np.exp(-t * 28)
    else:
        signal = filter_band(noise, 4500, 14500)
        for f in (4211, 5873, 7139, 9311):
            signal += .06 * np.sin(2 * np.pi * f * t)
        signal *= np.exp(-t * (48 if kind == 'hat' else 13 if kind == 'open' else 3.2))
    signal *= np.minimum(1, t / .001) * np.minimum(1, (seconds - t) / .012)
    return signal


# Drop-D power chords: heavy syncopated chugs, with a darker descending turnaround.
roots = (38, 38, 41, 36, 38, 43, 41, 36)
for bar, root in enumerate(roots):
    riff = ((0, .42, True), (.5, .22, True), (.875, .2, True),
            (1.5, .45, False), (2.25, .23, True), (2.75, .22, True), (3.25, .7, False))
    for beat, length, muted in riff:
        pitch = root if beat < 3.25 or bar % 2 == 0 else root - 2
        duration = length * BEAT
        for pan, detune in ((-.85, -.045), (.85, .04)):
            add(guitar(pitch, duration, muted, detune), bar * 4 + beat + rng.uniform(0, .008), .22, pan)
        t = np.arange(round(duration * RATE)) / RATE
        f = 440 * 2 ** ((pitch - 12 - 69) / 12)
        bass = np.tanh(1.6 * (np.sin(2 * np.pi * f * t) + .2 * np.sin(4 * np.pi * f * t)))
        bass *= (1 - np.exp(-t * 650)) * np.exp(-t * 3) * np.minimum(1, (duration - t) / .02)
        add(bass, bar * 4 + beat, .32)
    for beat in (0, 1.5, 2, 2.75):
        add(drum('kick'), bar * 4 + beat, .72)
    for beat in (1, 3):
        add(drum('snare'), bar * 4 + beat + .014, .55)
    for eighth in range(8):
        add(drum('open' if eighth == 7 else 'hat'), bar * 4 + eighth * .5,
            .12 if eighth % 2 == 0 else .085, .28)
    if bar in (0, 4):
        add(drum('crash'), bar * 4, .14, -.3)
    if bar == 7:
        for beat in (3.5, 3.75):
            add(drum('snare'), bar * 4 + beat, .3)

# Circular ambience retains cymbal tails across the loop boundary.
dry = mix.copy()
mix += .065 * np.roll(dry[:, ::-1], round(.043 * RATE), axis=0)
mix += .035 * np.roll(dry, round(.079 * RATE), axis=0)
mix -= mix.mean(axis=0)
mix = np.tanh(mix * 1.15)
mix *= .88 / np.max(np.abs(mix))
# Keep the wrap free of clicks without an audible gap.
edge = 128
mix[:edge] *= np.linspace(0, 1, edge)[:, None]
mix[-edge:] *= np.linspace(1, 0, edge)[:, None]
assert np.isfinite(mix).all() and np.max(np.abs(mix)) < 1
output = Path(__file__).resolve().parents[1] / 'Assets/Resources/PhoenixAudio/phoenix-metal.wav'
with wave.open(str(output), 'wb') as wav:
    wav.setparams((2, 2, RATE, LENGTH, 'NONE', 'not compressed'))
    wav.writeframes(np.rint(mix * 32767).astype('<i2').tobytes())
print(f'{output}: {LENGTH / RATE:.2f}s, {BPM} BPM, peak {np.max(np.abs(mix)):.2f}, RMS {np.sqrt(np.mean(mix ** 2)):.3f}')
