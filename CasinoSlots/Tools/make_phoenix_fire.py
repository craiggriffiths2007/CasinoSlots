"""Generate Phoenix Slot 2 fire cues (Python + numpy, no sample dependencies).

Defaults to the Unity Resources folder when installed under Tools. Use
--output-dir to audition elsewhere. Seeds keep builds reproducible.
"""
import argparse
from pathlib import Path
import wave
import numpy as np

RATE = 44100


def noise(rng, count, low, high):
    frequencies = np.fft.rfftfreq(count, 1 / RATE)
    spectrum = np.fft.rfft(rng.standard_normal(count))
    # Smooth spectral slopes avoid the ringing of brick-wall filtering.
    shape = (frequencies / np.maximum(frequencies, low)) ** 2
    shape /= 1 + (frequencies / high) ** 4
    shape[0] = 0
    result = np.fft.irfft(spectrum * shape, n=count)
    return result / max(np.std(result), 1e-9)


def render(symbols):
    rng = np.random.default_rng(27092026 + int(symbols))
    duration = 0.84 if symbols else 0.89
    count = round(RATE * duration)
    t = np.arange(count) / RATE
    audio = np.zeros((count, 2))
    for channel in range(2):
        air = noise(rng, count, 180, 2400)
        body = noise(rng, count, 45, 420)
        flutter = np.interp(t, np.linspace(0, duration, 38), rng.uniform(.55, 1, 38))
        if symbols:
            # A heavy damp cloth lands over the flame: short air displacement,
            # soft low impact, uneven wet slap, then a smothered steam release.
            hit = np.maximum(t - .045, 0)
            impact = (1 - np.exp(-hit / .003)) * np.exp(-hit / .060)
            pressure = np.sin(2 * np.pi * (62 * hit + 2.1 * (1 - np.exp(-hit / .025))))
            cloth = noise(rng, count, 130, 1250)
            steam = noise(rng, count, 1100, 5400)
            throw = np.exp(-((t - .035) / .022) ** 2)
            slap = impact + .28 * np.exp(-((t - .089) / .014) ** 2)
            release = np.maximum(t - .073, 0)
            hiss = (1 - np.exp(-release / .023)) * np.exp(-release / .135)
            audio[:, channel] = .09 * air * throw
            audio[:, channel] += (.40 * pressure + .30 * body) * impact
            audio[:, channel] += .23 * cloth * slap * flutter
            audio[:, channel] += .085 * steam * hiss * flutter
        else:
            # Gas catching in one breathy pressure burst: a deep whump,
            # turbulent flame front, then the quieter hiss of a burning jet.
            ignition = (1 - np.exp(-t / .008)) * np.exp(-t / .115)
            roar = (1 - np.exp(-t / .022)) * np.exp(-t / .23)
            pressure = np.sin(2 * np.pi * (68 * t + 3.2 * (1 - np.exp(-t / .045))))
            jet = noise(rng, count, 650, 4800)
            audio[:, channel] = .36 * pressure * ignition
            audio[:, channel] += (.38 * body + .20 * air) * roar * flutter
            audio[:, channel] += .055 * jet * (1 - np.exp(-t / .018)) * np.exp(-t / .34)

    # Short room reflections thicken the impact without a musical/reverb tail.
    if symbols:
        dry = audio.copy()
        for seconds, gain in ((.013, .12), (.027, .07)):
            offset = round(seconds * RATE)
            audio[offset:] += dry[:-offset, ::-1] * gain
    envelope = np.minimum(t / .012, 1)
    fade_start = .57 if symbols else .61
    fade = np.clip((t - fade_start) / (duration - fade_start), 0, 1)
    envelope *= .5 + .5 * np.cos(np.pi * fade)
    audio *= envelope[:, None]
    audio *= (.85 if symbols else .76) / np.max(np.abs(audio))
    audio[0] = audio[-1] = 0
    return audio


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/Resources/PhoenixAudio')
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    for symbols, name in ((False, 'p2-line-fire'), (True, 'p2-symbol-fire')):
        audio = render(symbols)
        assert np.isfinite(audio).all() and np.max(np.abs(audio)) < 1
        pcm = np.rint(audio * 32767).astype('<i2')
        path = args.output_dir / (name + '.wav')
        with wave.open(str(path), 'wb') as clip:
            clip.setnchannels(2)
            clip.setsampwidth(2)
            clip.setframerate(RATE)
            clip.writeframes(pcm.tobytes())
        with wave.open(str(path), 'rb') as clip:
            assert clip.getnframes() == len(audio) and clip.getnchannels() == 2
        print(f'{name}: {len(audio)/RATE:.2f}s, peak={np.max(np.abs(audio)):.2f}, RMS={np.sqrt(np.mean(audio**2)):.3f}')


if __name__ == '__main__':
    main()
