// Starlife's sound. Everything is synthesised here at start-up, so the game ships no audio files and needs no licences:
// two music loops (suite, town), the town's street ambience, a car engine, and the interface and footstep sounds.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Random = System.Random;

public sealed class SLAudio : MonoBehaviour {
    public static SLAudio I { get; private set; }
    const int Rate = 22050;

    AudioSource suite, town, street, engine, sfx;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    Task<Dictionary<string, float[]>> making;
    float wantSuite = 1, wantTown, wantStreet, wantEngine, enginePitch = 1;
    bool silent;

    public float MusicVolume { get => PlayerPrefs.GetFloat("sl.music", 0.55f); set { PlayerPrefs.SetFloat("sl.music", Mathf.Clamp01(value)); } }
    public float SoundVolume { get => PlayerPrefs.GetFloat("sl.sound", 0.8f); set { PlayerPrefs.SetFloat("sl.sound", Mathf.Clamp01(value)); } }
    public bool Ready => making == null;
    public int ClipCount => clips.Count;

    /// <summary>Makes the player. A silent one (the screenshot tours) still makes every clip, but keeps quiet.</summary>
    public static SLAudio Create(bool silent) {
        var go = new GameObject("Audio"); DontDestroyOnLoad(go); go.AddComponent<AudioListener>();
        I = go.AddComponent<SLAudio>(); I.silent = silent;
        AudioSource Src(bool loop) { var s = go.AddComponent<AudioSource>(); s.loop = loop; s.playOnAwake = false; s.spatialBlend = 0; s.volume = 0; return s; }
        I.suite = Src(true); I.town = Src(true); I.street = Src(true); I.engine = Src(true); I.sfx = Src(false);
        I.making = Task.Run(MakeAll);
        return I;
    }

    /// <summary>What is playing: "suite", "town" or "room" (indoors in town: the street is muffled and the suite's tune comes back).</summary>
    public void Scene(string mode) {
        wantSuite = mode == "suite" ? 1 : mode == "room" ? 0.6f : 0; wantTown = mode == "town" ? 0.7f : 0; wantStreet = mode == "town" ? 0.8f : mode == "room" ? 0.12f : 0;
        if (mode != "town") Engine(-1);
    }
    /// <summary>The car's engine: 0 idling to 1 flat out, or below 0 for off.</summary>
    public void Engine(float rev) { wantEngine = rev < 0 ? 0 : 0.55f + 0.3f * rev; if (rev >= 0) enginePitch = 0.8f + 1.3f * rev; }

    public void Play(string name, float volume = 1, float pitch = 1) {
        if (!clips.TryGetValue(name, out var c)) return;
        lastPlayed.TryGetValue(name, out float last); if (Time.unscaledTime - last < 0.05f) return; lastPlayed[name] = Time.unscaledTime;
        sfx.pitch = pitch; sfx.PlayOneShot(c, volume);
    }

    void Update() {
        if (making != null && making.IsCompleted) {
            if (making.IsFaulted) Debug.LogWarning("Starlife: could not make the sounds. " + making.Exception?.GetBaseException().Message);
            else foreach (var kv in making.Result) { var c = AudioClip.Create(kv.Key, kv.Value.Length, 1, Rate, false); c.SetData(kv.Value, 0); clips[kv.Key] = c; }
            making = null;
            void Start(AudioSource s, string name) { if (clips.TryGetValue(name, out var c)) { s.clip = c; s.Play(); } }
            Start(suite, "music-suite"); Start(town, "music-town"); Start(street, "street"); Start(engine, "engine");
        }
        float k = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 2.5f), music = silent ? 0 : MusicVolume, sound = silent ? 0 : SoundVolume;
        suite.volume = Mathf.Lerp(suite.volume, wantSuite * music, k); town.volume = Mathf.Lerp(town.volume, wantTown * music, k);
        street.volume = Mathf.Lerp(street.volume, wantStreet * sound, k); engine.volume = Mathf.Lerp(engine.volume, wantEngine * sound, k * 3);
        engine.pitch = Mathf.Lerp(engine.pitch, enginePitch, k * 3); sfx.volume = sound;
    }

    /// <summary>Writes a clip as a WAV, for checking it outside the game.</summary>
    public void SaveWav(string name, string file) {
        if (!clips.TryGetValue(name, out var c)) return; var d = new float[c.samples]; c.GetData(d, 0);
        using var w = new System.IO.BinaryWriter(System.IO.File.Create(file));
        void Tag(string s) => w.Write(System.Text.Encoding.ASCII.GetBytes(s));
        Tag("RIFF"); w.Write(36 + d.Length * 2); Tag("WAVEfmt "); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16); Tag("data"); w.Write(d.Length * 2);
        foreach (var v in d) w.Write((short)(Mathf.Clamp(v, -1, 1) * 32000));
    }
    public (float peak, float rms) Level(string name) {
        if (!clips.TryGetValue(name, out var c)) return (0, 0); var d = new float[c.samples]; c.GetData(d, 0); float p = 0, r = 0;
        foreach (var v in d) { p = Mathf.Max(p, Mathf.Abs(v)); r += v * v; } return (p, Mathf.Sqrt(r / Mathf.Max(1, d.Length)));
    }

    // ================================================================ the synth (runs on a worker thread: no Unity calls below)
    const float TwoPi = (float)(Math.PI * 2);
    static float Note(int midi) => 440f * (float)Math.Pow(2, (midi - 69) / 12.0);
    static float Saturate(float v) => (float)Math.Tanh(v);

    // every voice writes with its index wrapped round the buffer, so a tail runs on into the start and the loop has no seam
    static void Ep(float[] b, float at, float dur, int midi, float vol) {      // an electric piano
        float f = Note(midi); int s = (int)(at * Rate), n = (int)((dur + 0.6f) * Rate);
        for (int i = 0; i < n; i++) {
            float t = i / (float)Rate, env = Math.Min(t / 0.008f, 1) * (float)Math.Exp(-t * 2.2f) * (t > dur ? (float)Math.Exp(-(t - dur) * 9) : 1);
            float bell = (float)Math.Exp(-t * 11) * 0.28f;
            b[(s + i) % b.Length] += vol * env * ((float)Math.Sin(TwoPi * f * t) + 0.22f * (float)Math.Sin(TwoPi * 2 * f * t) + bell * (float)Math.Sin(TwoPi * 7 * f * t));
        }
    }
    static void Pluck(float[] b, float at, int midi, float vol, Random r, float decay = 0.996f) {     // a plucked string (Karplus-Strong): the highlife guitar
        int period = Math.Max(2, (int)(Rate / Note(midi))), s = (int)(at * Rate), n = Rate; var ring = new float[period];
        for (int i = 0; i < period; i++) ring[i] = (float)r.NextDouble() * 2 - 1;
        for (int i = 0; i < n; i++) {
            int a = i % period, c = (i + 1) % period; float v = ring[a]; ring[a] = decay * 0.5f * (ring[a] + ring[c]);
            b[(s + i) % b.Length] += vol * v * Math.Min(1, (n - i) / 2000f);
        }
    }
    static void Bass(float[] b, float at, float dur, int midi, float vol) {
        float f = Note(midi); int s = (int)(at * Rate), n = (int)(dur * Rate);
        for (int i = 0; i < n; i++) { float t = i / (float)Rate, env = Math.Min(t / 0.01f, 1) * Math.Min((n - i) / 600f, 1) * (float)Math.Exp(-t * 1.6f); b[(s + i) % b.Length] += vol * env * ((float)Math.Sin(TwoPi * f * t) + 0.25f * (float)Math.Sin(TwoPi * 2 * f * t)); }
    }
    static void Kick(float[] b, float at, float vol) {
        int s = (int)(at * Rate), n = (int)(0.22f * Rate); float ph = 0;
        for (int i = 0; i < n; i++) { float t = i / (float)Rate; ph += TwoPi * (48 + 90 * (float)Math.Exp(-t * 32)) / Rate; b[(s + i) % b.Length] += vol * (float)Math.Exp(-t * 15) * (float)Math.Sin(ph); }
    }
    static void Tick(float[] b, float at, float vol, float len, Random r) {     // a hat or a shaker: noise with the lows taken out
        int s = (int)(at * Rate), n = (int)(len * Rate); float last = 0;
        for (int i = 0; i < n; i++) { float x = (float)r.NextDouble() * 2 - 1, hp = x - last; last = x; float t = i / (float)n; b[(s + i) % b.Length] += vol * hp * (float)Math.Exp(-t * 5) * Math.Min(t * 12, 1); }
    }
    static float[] Finish(float[] b, float gain) { for (int i = 0; i < b.Length; i++) b[i] = Saturate(b[i] * gain) * 0.8f; return b; }

    /// <summary>A slow, warm tune for the suite: electric piano chords, a soft bass and a little melody.</summary>
    static float[] SuiteTune() {
        const float bpm = 84; float beat = 60 / bpm; int bars = 16; var b = new float[(int)(bars * 4 * beat * Rate)]; var r = new Random(7);
        int[][] chords = { new[] { 53, 57, 60, 64 }, new[] { 50, 57, 60, 65 }, new[] { 46, 53, 57, 62 }, new[] { 48, 55, 58, 64 } };      // Fmaj7, Dm7, Bbmaj7, C7
        int[] scale = { 65, 67, 69, 72, 74, 77 };   // F major pentatonic
        for (int bar = 0; bar < bars; bar++) {
            var c = chords[bar % 4]; float t = bar * 4 * beat;
            foreach (var m in c) { Ep(b, t, 1.8f * beat, m, 0.11f); Ep(b, t + 2.5f * beat, 1.2f * beat, m, 0.075f); }
            Bass(b, t, 1.8f * beat, c[0] - 12, 0.3f); Bass(b, t + 2.5f * beat, 1.2f * beat, c[0] - 12, 0.2f);
            for (int e = 0; e < 8; e++) Tick(b, t + e * beat / 2 + (e % 2 == 1 ? 0.02f : 0), e % 2 == 0 ? 0.05f : 0.03f, 0.05f, r);
            if (bar >= 4 && bar % 2 == 0) Kick(b, t, 0.28f);
            if (bar >= 4) { int i = r.Next(scale.Length); foreach (float off in new[] { 0.5f, 1.5f, 2f, 3.5f }) { if (r.NextDouble() < 0.72) Ep(b, t + off * beat, 0.5f * beat, scale[i] + 12, 0.1f); i = Math.Clamp(i + r.Next(-1, 2), 0, scale.Length - 1); } }
        }
        return Finish(b, 1.5f);
    }
    /// <summary>A bright street tune for the town, in the manner of highlife: interlocking plucked guitars over a steady bass and shaker.</summary>
    static float[] TownTune() {
        const float bpm = 108; float beat = 60 / bpm; int bars = 16; var b = new float[(int)(bars * 4 * beat * Rate)]; var r = new Random(11);
        int[][] chords = { new[] { 60, 64, 67 }, new[] { 65, 69, 72 }, new[] { 67, 71, 74 }, new[] { 65, 69, 72 } };     // C, F, G, F
        int[] lick = { 0, 2, 1, 2, 0, 1, 2, 1 };       // which chord tone each off-beat guitar note takes
        for (int bar = 0; bar < bars; bar++) {
            var c = chords[bar % 4]; float t = bar * 4 * beat;
            for (int e = 0; e < 8; e++) { float at = t + e * beat / 2; if (e != 0 && e != 3 && e != 6 || bar >= 8) Pluck(b, at, c[lick[e]] + 12, 0.16f, r); }
            foreach (float off in new[] { 1f, 2.5f, 3f }) Pluck(b, t + off * beat, c[0], 0.13f, r, 0.992f);
            Bass(b, t, 1.3f * beat, c[0] - 24, 0.34f); Bass(b, t + 1.5f * beat, 0.9f * beat, c[0] - 24, 0.26f); Bass(b, t + 3f * beat, 0.9f * beat, c[2] - 24, 0.26f);
            for (int e = 0; e < 16; e++) Tick(b, t + e * beat / 4, e % 4 == 2 ? 0.07f : 0.035f, 0.045f, r);
            Kick(b, t, 0.3f); Kick(b, t + 2.5f * beat, 0.22f);
        }
        return Finish(b, 1.3f);
    }
    /// <summary>The street: a low city hum that swells and falls, with the odd bird by day. 24 seconds, seamless.</summary>
    static float[] Street() {
        var b = new float[24 * Rate]; var r = new Random(3); float lo = 0, lo2 = 0;
        for (int i = 0; i < b.Length; i++) { float x = (float)r.NextDouble() * 2 - 1; lo += (x - lo) * 0.012f; lo2 += (x - lo2) * 0.08f; float t = i / (float)b.Length; b[i] = lo * 2.6f * (1 + 0.3f * (float)Math.Sin(TwoPi * 2 * t)) + lo2 * 0.16f * (1 + 0.5f * (float)Math.Sin(TwoPi * 5 * t)); }
        // the noise's ends do not meet: cross-fade the last second into the first
        int fade = Rate; for (int i = 0; i < fade; i++) { float k = i / (float)fade; b[i] = b[i] * k + b[b.Length - fade + i] * (1 - k); }
        Array.Resize(ref b, b.Length - fade);
        for (int c = 0; c < 9; c++) { int s = r.Next(b.Length); float f = 2600 + r.Next(1400); for (int k = 0; k < 3; k++) for (int i = 0; i < 1500; i++) { float t = i / (float)Rate, w = i / 1500f; b[(s + k * 2600 + i) % b.Length] += 0.022f * (float)Math.Sin(Math.PI * w) * (float)Math.Sin(TwoPi * (f + 500 * w) * t); } }
        return Finish(b, 1);
    }
    /// <summary>An engine note: whole cycles of a firing pulse, so it loops exactly; the game changes its pitch.</summary>
    static float[] Engine() {
        const float f = 60; int cycles = 30; var b = new float[(int)(cycles / f * Rate)]; float lo = 0;
        for (int i = 0; i < b.Length; i++) { float ph = i / (float)b.Length * cycles; float saw = 2 * (ph - (float)Math.Floor(ph)) - 1, sub = (float)Math.Sin(TwoPi * ph / 2); lo += (saw - lo) * 0.09f; b[i] = 0.5f * lo + 0.32f * sub; }
        return b;
    }
    delegate float Shape(float t);
    static float[] Make(float seconds, Shape f) { var b = new float[(int)(seconds * Rate)]; for (int i = 0; i < b.Length; i++) { b[i] = f(i / (float)Rate) * Math.Min(1, (b.Length - i) / 200f); } return b; }
    static float Tone(float t, float at, float len, float hz, float vol) { float x = t - at; return x < 0 || x > len ? 0 : vol * (float)Math.Exp(-x / len * 4) * Math.Min(x / 0.004f, 1) * ((float)Math.Sin(TwoPi * hz * x) + 0.25f * (float)Math.Sin(TwoPi * 2 * hz * x)); }
    static float[] Noise(float seconds, int seed, float cut, Func<float, float> env, float vol) {
        var r = new Random(seed); float lo = 0; var b = new float[(int)(seconds * Rate)];
        for (int i = 0; i < b.Length; i++) { lo += ((float)r.NextDouble() * 2 - 1 - lo) * cut; b[i] = vol * lo * env(i / (float)b.Length); }
        return b;
    }
    static Dictionary<string, float[]> MakeAll() => new Dictionary<string, float[]> {
        { "music-suite", SuiteTune() }, { "music-town", TownTune() }, { "street", Street() }, { "engine", Engine() },
        { "click", Make(0.05f, t => Tone(t, 0, 0.05f, 1250, 0.3f)) },
        { "coin", Make(0.34f, t => Tone(t, 0, 0.09f, 988, 0.34f) + Tone(t, 0.08f, 0.25f, 1319, 0.34f)) },
        { "buy", Make(0.28f, t => Tone(t, 0, 0.14f, 784, 0.3f) + Tone(t, 0.09f, 0.18f, 587, 0.3f)) },
        { "win", Make(0.7f, t => Tone(t, 0, 0.2f, 784, 0.3f) + Tone(t, 0.14f, 0.2f, 988, 0.3f) + Tone(t, 0.28f, 0.42f, 1175, 0.34f)) },
        { "level", Make(1.1f, t => Tone(t, 0, 0.26f, 523, 0.3f) + Tone(t, 0.13f, 0.26f, 659, 0.3f) + Tone(t, 0.26f, 0.26f, 784, 0.3f) + Tone(t, 0.39f, 0.7f, 1047, 0.36f) + Tone(t, 0.39f, 0.7f, 784, 0.18f)) },
        { "swish", Noise(0.2f, 21, 0.5f, x => (float)Math.Sin(Math.PI * x) * (float)Math.Sin(Math.PI * x), 0.5f) },
        { "shutter", Noise(0.14f, 22, 0.9f, x => x < 0.3f ? 1 - x / 0.3f : x > 0.55f ? (1 - x) / 0.45f : 0, 0.55f) },
        { "step", Noise(0.1f, 23, 0.14f, x => (float)Math.Exp(-x * 5) * Math.Min(x * 30, 1), 1.4f) },
    };
}
