using System;
using UnityEngine;

namespace TempestTemple
{
    // Same notes and tempo as the web game's underwater arpeggio, rendered once to PCM.
    public sealed class TempestAudio : MonoBehaviour
    {
        AudioSource music, effects, bubbling;
        AudioClip spin, drop, pop, win, bonus, bubbleStream;
        public bool MusicOn { get; private set; }
        public bool EffectsOn { get; private set; }
        const int Rate = 22050;
        void Awake()
        {
            music = gameObject.AddComponent<AudioSource>(); effects = gameObject.AddComponent<AudioSource>();
            bubbling = gameObject.AddComponent<AudioSource>();
            bubbling.loop = true; bubbling.playOnAwake = false; bubbling.volume = 0;
            music.loop = true; music.volume = .65f; effects.volume = .7f;
            MusicOn = PlayerPrefs.GetInt("Tempest.Music", 1) != 0;
            EffectsOn = PlayerPrefs.GetInt("Tempest.Effects", 1) != 0;
            float[] song = new float[Mathf.RoundToInt(19.2f * Rate)];
            double[][] chords = { new[] {146.83,174.61,220.0}, new[] {130.81,164.81,196.0}, new[] {116.54,146.83,174.61}, new[] {130.81,164.81,196.0} };
            int[] pattern = {0,2,1,2,0,1,2,1};
            for (int beat = 0; beat < 64; beat++)
            {
                var chord = chords[beat / 16];
                if (beat % 4 == 0) AddTone(song, chord[0]/2, beat*.3, 1.3, .10);
                if (beat % 16 == 0) foreach (double f in chord) AddTone(song, f, beat*.3, 3.8, .025);
                AddTone(song, chord[pattern[beat%8]] * (beat%8 == 6 ? 4 : 2), beat*.3, .7, .045);
            }
            music.clip = Clip("Underwater arpeggio", song); music.mute = !MusicOn; music.Play();
            spin = Notes("Spin", new[] {420.0,260,140,65}, .065, .18);
            drop = Notes("Drop", new[] {260.0,330,390}, .035, .11);
            pop = BubbleSound("Bubble burst", .55, 16, 73);
            bubbleStream = BubbleSound("Rising bubbles", 2.8, 48, 127);
            bubbling.clip = bubbleStream;
            win = Notes("Win", new[] {587.0,740,880}, .1, .45);
            bonus = Notes("Bonus", new[] {587.0,740,880,1174,1480,1760}, .1, .45);
        }
        static void AddTone(float[] data, double f, double start, double duration, double volume)
        {
            int offset = (int)(start * Rate), count = (int)(duration * Rate);
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / Rate;
                double envelope = Math.Min(1, t / .015) * Math.Exp(-7 * t / duration);
                data[(offset+i) % data.Length] += (float)(Math.Sin(t*f*Math.PI*2)*envelope*volume);
            }
        }
        static AudioClip Clip(string name, float[] samples)
        { var clip = AudioClip.Create(name, samples.Length, 1, Rate, false); clip.SetData(samples, 0); return clip; }
        static AudioClip Notes(string name, double[] notes, double spacing, double duration)
        {
            var samples = new float[(int)((notes.Length*spacing+duration)*Rate)];
            for (int i = 0; i < notes.Length; i++) AddTone(samples, notes[i], i*spacing, duration, .19);
            return Clip(name, samples);
        }
        static AudioClip BubbleSound(string name, double duration, int count, int seed)
        {
            var samples = new float[(int)(duration * Rate)];
            var random = new System.Random(seed);
            for (int bubble = 0; bubble < count; bubble++)
            {
                double length = .06 + random.NextDouble() * .12;
                double start = random.NextDouble() * (duration - length);
                double frequency = 160 + random.NextDouble() * 200;
                double sweep = .3 + random.NextDouble() * .3;
                double volume = .04 + random.NextDouble() * .04;
                int offset = (int)(start * Rate), size = (int)(length * Rate);
                for (int i = 0; i < size && offset + i < samples.Length; i++)
                {
                    double t = (double)i / Rate, progress = t / length;
                    // A short, damped rising resonance makes a watery "bloop".
                    double phase = 2 * Math.PI * frequency * (t + sweep * t * t / (2 * length));
                    double envelope = Math.Min(1, t / .004) * Math.Exp(-5 * progress) * (1-progress);
                    samples[offset+i] += (float)(Math.Sin(phase) * envelope * volume);
                }
            }
            // Gentle saturation keeps overlapping bubbles inside the PCM range.
            for (int i = 0; i < samples.Length; i++) samples[i] = (float)Math.Tanh(samples[i]);
            return Clip(name, samples);
        }
        public void UpdateBubbles(int visibleCount)
        {
            float target = EffectsOn && visibleCount > 0 ? Mathf.Lerp(.12f,.38f,Mathf.Clamp01(visibleCount / 400f)) : 0;
            bubbling.volume = Mathf.MoveTowards(bubbling.volume, target, Time.unscaledDeltaTime * 1.5f);
            if (target > 0 && !bubbling.isPlaying) bubbling.Play();
            else if (target == 0 && bubbling.volume <= .001f && bubbling.isPlaying) bubbling.Stop();
        }
        public void ToggleMusic() { MusicOn = !MusicOn; music.mute = !MusicOn; PlayerPrefs.SetInt("Tempest.Music", MusicOn ? 1 : 0); }
        public void ToggleEffects()
        {
            EffectsOn = !EffectsOn;
            effects.mute = bubbling.mute = !EffectsOn;
            PlayerPrefs.SetInt("Tempest.Effects", EffectsOn ? 1 : 0);
        }
        public void Play(string cue)
        {
            if (!EffectsOn) return;
            effects.PlayOneShot(cue == "spin" ? spin : cue == "drop" ? drop : cue == "pop" ? pop : cue == "bonus" ? bonus : win);
        }
        void OnDestroy()
        { foreach (var clip in new[] { music != null ? music.clip : null, spin, drop, pop, win, bonus, bubbleStream }) if (clip != null) Destroy(clip); }
    }
}
