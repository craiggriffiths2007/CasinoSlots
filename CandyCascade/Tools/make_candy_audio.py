"""Create original, layered stereo game effects. No external samples are used."""
from pathlib import Path
import wave
import argparse
import numpy as np

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--only', choices=['spin', 'landing', 'cascade', 'win', 'bonus', 'click'])
args = parser.parse_args()

RATE = 44100
OUT = Path(__file__).resolve().parents[1] / 'Assets/Resources/Audio'
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(71027)

def noise(count):
    return np.convolve(rng.normal(0, 1, count), np.ones(7) / 7, mode='same')

def pluck(freq, seconds, softness=1):
    t = np.arange(int(RATE * seconds)) / RATE
    attack = 1 - np.exp(-t * 650)
    return attack * (np.sin(2*np.pi*freq*t)*np.exp(-t*8/softness)
        + .35*np.sin(2*np.pi*freq*2.006*t)*np.exp(-t*16/softness)
        + .12*np.sin(2*np.pi*freq*3.98*t)*np.exp(-t*28/softness))

def add(track, sound, at, gain=1):
    start = int(at*RATE); count = min(len(sound), len(track)-start)
    track[start:start+count] += sound[:count] * gain

def export(name, mono, peak=.68, reflections=True):
    if args.only and name != args.only:
        return
    mono[-min(1000,len(mono)):] *= np.linspace(1,0,min(1000,len(mono)))
    # Short stereo reflections add space without burying the transient.
    left, right = mono.copy(), mono.copy()
    for delay, gain in ([(0.047,.11),(.093,.06)] if reflections else []):
        n = int(delay*RATE); right[n:] += mono[:-n]*gain
        n += 313; left[n:] += mono[:-n]*gain*.7
    stereo = np.stack([left,right], axis=1)
    stereo *= peak / max(peak, np.max(np.abs(stereo)))
    assert np.isfinite(stereo).all() and np.max(np.abs(stereo)) < .95
    with wave.open(str(OUT / (name+'.wav')), 'wb') as file:
        file.setparams((2,2,RATE,len(stereo),'NONE','not compressed'))
        file.writeframes((stereo*32767).astype('<i2').tobytes())
    print(f'{name}: {len(stereo)/RATE:.2f}s stereo, peak {np.max(np.abs(stereo)):.3f}')

# A short, dry two-note touch: no filtered noise or sweeping pitch.
spin = np.zeros(int(RATE*.20))
for i,f in enumerate([659.25,783.99]):
    add(spin,pluck(f,.17,.38),i*.028,.12)
export('spin',spin,.22,reflections=False)

t = np.arange(int(RATE*.22))/RATE
landing = .6*np.sin(2*np.pi*(185*t+24*(1-np.exp(-t*23))))*np.exp(-t*29)*(1-np.exp(-t*700))
landing += noise(len(t))*.22*np.exp(-t*65)
add(landing, pluck(1046.5,.18),.018,.16)
export('landing',landing,.52)

cascade = np.zeros(int(RATE*.68))
for i,f in enumerate([783.99,987.77,1174.66,1567.98]): add(cascade,pluck(f,.42),i*.055,.30)
t = np.arange(int(RATE*.12))/RATE
pop = .4*np.sin(2*np.pi*(430*t+8*(1-np.exp(-t*45))))*np.exp(-t*45)*(1-np.exp(-t*900))
add(cascade,pop,0,.75)
export('cascade',cascade)

win = np.zeros(int(RATE*1.15))
for i,f in enumerate([523.25,659.25,783.99,1046.50]): add(win,pluck(f,.65,1.5),i*.095,.32)
export('win',win)

bonus = np.zeros(int(RATE*2.1))
for i,f in enumerate([523.25,659.25,783.99,1046.50,1318.51,1567.98]): add(bonus,pluck(f,.85,2),i*.11,.3)
for f in [523.25,659.25,783.99,1046.50]: add(bonus,pluck(f,1.2,3),.80,.20)
export('bonus',bonus,.72)

export('click',pluck(880,.12)*.35,.35)
