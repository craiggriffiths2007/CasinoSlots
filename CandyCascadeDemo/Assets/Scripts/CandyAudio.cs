using UnityEngine;

namespace CandyCascade
{
    public sealed class CandyAudio : MonoBehaviour
    {
        AudioClip spin, landing, cascade, bonus, win, click;
        readonly AudioSource[] voices = new AudioSource[12];
        int nextVoice;
        bool muted;
        public bool Muted
        {
            get => muted;
            set { muted = value; foreach (var voice in voices) if (voice) voice.mute = value; }
        }
        void Awake()
        {
            spin = Load("spin"); landing = Load("landing"); cascade = Load("cascade");
            bonus = Load("bonus"); win = Load("win"); click = Load("click");
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false; voices[i].spatialBlend = 0;
            }
        }
        static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            if (!clip) Debug.LogError("Missing candy audio: " + name);
            return clip;
        }
        void Play(AudioClip clip, float gain, float pitch = 1, float pan = 0)
        {
            if (muted || !clip) return;
            var source = voices[nextVoice++ % voices.Length];
            source.Stop(); source.clip = clip; source.volume = gain;
            source.pitch = pitch; source.panStereo = pan; source.Play();
        }
        public void Spin() => Play(spin, .22f);
        public void Land(int column) => Play(landing, .22f, .92f + column * .035f, (column - 3) * .12f);
        public void Cascade(int chain) => Play(cascade, .6f, Mathf.Pow(2, Mathf.Min(chain - 1, 7) / 12f));
        public void Bonus() => Play(bonus, .7f);
        public void Win(decimal multiple) => Play(win, multiple >= 10 ? .7f : .42f, multiple >= 10 ? 1.12f : 1);
        public void Click() => Play(click, .30f);
    }
}
