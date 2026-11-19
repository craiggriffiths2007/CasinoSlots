using System.Collections;
using UnityEngine;

public class PhoenixAudio : MonoBehaviour
{
    private const string MuteKey = "PhoenixSlot2.AudioMuted";
    private AudioSource music;
    private readonly AudioSource[] effects = new AudioSource[24];
    private readonly System.Collections.Generic.Dictionary<AudioSource, Coroutine> effectFades = new();
    private AudioSource fire;
    private AudioClip[] drops;
    private AudioClip[] landings;
    private AudioClip win;
    private AudioClip payout;
    private AudioClip lineFire;
    private AudioClip symbolFire;
    private Coroutine fireFade;
    private readonly System.Random variation = new System.Random();
    private int nextVoice;
    private bool started;
    private bool focused = true;
    public bool Muted { get; private set; }

    private void Awake()
    {
        music = CreateSource(0.16f, true);
        for (int i = 0; i < effects.Length; i++) effects[i] = CreateSource(0.65f, false);
        fire = CreateSource(0f, false);
        music.clip = Load("phoenix-relaxed");
        drops = new[] { Load("p2-drop-0"), Load("p2-drop-1"), Load("p2-drop-2") };
        landings = new[] { Load("p2-land-0"), Load("p2-land-1"), Load("p2-land-2") };
        win = Load("p2-win");
        payout = Load("p2-payout");
        lineFire = Load("p2-line-fire");
        symbolFire = Load("p2-symbol-fire");
        Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
        ApplyMute();
    }

    private AudioSource CreateSource(float volume, bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.loop = loop;
        return source;
    }

    private static AudioClip Load(string name)
    {
        var clip = Resources.Load<AudioClip>("PhoenixAudio/" + name);
        if (clip == null) Debug.LogWarning("Missing Phoenix audio: " + name);
        return clip;
    }

    public void Begin()
    {
        if (started || !focused) return;
        started = true;
        if (music.clip != null) music.Play();
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        PlayerPrefs.SetInt(MuteKey, Muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMute();
        Begin();
    }

    private void ApplyMute()
    {
        music.mute = fire.mute = Muted;
        foreach (var voice in effects) voice.mute = Muted;
    }

    private void Play(AudioClip clip, float volume, bool varyPitch = false, float maxDuration = 0f, float pitchMultiplier = 1f)
    {
        if (!focused || Muted || clip == null) return;
        // Independent voices avoid retuning or cutting off another overlapping effect.
        AudioSource source = effects[nextVoice];
        for (int i = 0; i < effects.Length; i++)
        {
            int index = (nextVoice + i) % effects.Length;
            if (effects[index].isPlaying) continue;
            source = effects[index];
            nextVoice = index;
            break;
        }
        nextVoice = (nextVoice + 1) % effects.Length;
        if (effectFades.TryGetValue(source, out Coroutine fade))
        {
            StopCoroutine(fade);
            effectFades.Remove(source);
        }
        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitchMultiplier * (varyPitch ? 0.90f + (float)variation.NextDouble() * 0.20f : 1f);
        source.Play();
        if (maxDuration > 0f) effectFades[source] = StartCoroutine(FadeEffect(source, maxDuration));
    }

    private IEnumerator FadeEffect(AudioSource source, float duration)
    {
        float volume = source.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = volume * (1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        source.Stop();
        effectFades.Remove(source);
    }

    public void Drop() => Play(drops[variation.Next(drops.Length)], 0.34f, true, 0.18f);
    public void Land() => Play(landings[variation.Next(landings.Length)], 0.18f + (float)variation.NextDouble() * 0.12f, true, pitchMultiplier: 1.18f);
    public void Win() => Play(win, 0.31f);
    public void Payout() => Play(payout, 0.325f);
    public void Phoenix() => Play(win, 0.31f, pitchMultiplier: 0.9f);

    public void StartFire(bool symbols)
    {
        if (fireFade != null) StopCoroutine(fireFade);
        fire.Stop();
        fire.volume = 0f;
        fire.clip = symbols ? symbolFire : lineFire;
        fire.loop = false;
        fire.pitch = 0.98f + (float)variation.NextDouble() * 0.04f;
        if (!focused || fire.clip == null) return;
        fire.Play();
        fireFade = StartCoroutine(FadeFire(symbols ? 0.62f : 0.40f, 0.012f, false));
    }

    public void StopFire()
    {
        if (fireFade != null) StopCoroutine(fireFade);
        fireFade = StartCoroutine(FadeFire(0f, 0.06f, true));
    }

    private IEnumerator FadeFire(float target, float duration, bool stop)
    {
        float start = fire.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fire.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        fire.volume = target;
        if (stop) fire.Stop();
        fireFade = null;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        if (music == null) return;
        if (!hasFocus)
        {
            music.Pause();
            foreach (var fade in effectFades.Values) StopCoroutine(fade);
            effectFades.Clear();
            foreach (var voice in effects) voice.Stop();
            if (fireFade != null) StopCoroutine(fireFade);
            fireFade = null;
            fire.Stop();
        }
        else if (started) music.UnPause();
    }
}
