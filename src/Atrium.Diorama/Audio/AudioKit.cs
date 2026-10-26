using System.Runtime.InteropServices;
using Raylib_cs;
using static Atrium.Diorama.Audio.Synth;

namespace Atrium.Diorama.Audio;

/// <summary>
/// Sound cues, generative music and ambience, all synthesised at startup. Soft by design: nothing is a buzzer.
/// Disabled (silent no-ops) when there's no audio device or during scripted captures.
/// </summary>
public sealed class AudioKit : IDisposable
{
    public bool Enabled { get; }
    public float Master { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.55f;
    public float SfxVolume { get; set; } = 0.75f;
    public float AmbienceVolume { get; set; } = 0.6f;

    private readonly Dictionary<string, Sound[]> _cues = new();
    private readonly Dictionary<string, int> _next = new();
    private readonly Dictionary<string, (Sound[] Notes, float Gap)> _moodNotes = new();
    private readonly Dictionary<string, Loop> _loops = new();
    private readonly Random _rng = new(11);
    private string _mood = "hub";
    private float _noteTimer = 2f;
    private readonly Queue<(float At, Sound S, float V)> _phrase = new();
    private float _clock;

    private sealed class Loop
    {
        public Music Music;
        public IntPtr Memory;
        public float Volume, Target;
    }

    public AudioKit(bool enable)
    {
        if (!enable) return;
        Raylib.InitAudioDevice();
        if (!Raylib.IsAudioDeviceReady()) return;
        Enabled = true;
        BuildCues();
        BuildMusic();
        BuildLoops();
    }

    // ───────────────────────── cues ─────────────────────────

    private void Cue(string name, int voices, params float[][] variants)
    {
        var sounds = new List<Sound>();
        foreach (var v in variants)
        {
            var wave = Raylib.LoadWaveFromMemory(".wav", Wav(v));
            var s = Raylib.LoadSoundFromWave(wave);
            Raylib.UnloadWave(wave);
            sounds.Add(s);
            for (int i = 1; i < voices; i++) sounds.Add(Raylib.LoadSoundAlias(s));
        }
        _cues[name] = sounds.ToArray();
    }

    private void BuildCues()
    {
        float[] Knock(float f) => Normalize(Mix(0.12f, (Pluck(f, 0.12f, new[] { (1f, 1f, 0.025f), (1.5f, 0.5f, 0.015f) }), 0, 1),
            (Env(HighPass(Noise(0.02f, NoiseColor.White, (int)f), 2000), 0.0005f, 0.004f), 0, 0.25f)), 0.5f);
        Cue("tock", 3, Knock(560), Knock(610));
        Cue("paper", 3, Normalize(Env(LowPass(HighPass(Noise(0.12f, NoiseColor.White, 3), 1800), 6000), 0.004f, 0.03f), 0.35f));
        Cue("page", 2, Normalize(Env(LowPass(HighPass(Noise(0.35f, NoiseColor.Pink, 5), 700), 5000, 1500), 0.04f, 0.12f), 0.45f));
        Cue("slide", 2, Normalize(Mix(0.45f,
            (Env(LowPass(Noise(0.35f, NoiseColor.Pink, 9), 1200, 400), 0.12f, 0.08f), 0, 0.8f),
            (Pluck(95, 0.25f, new[] { (1f, 1f, 0.08f) }), 0.3f, 0.9f)), 0.6f));
        Cue("pulse", 2, Normalize(Mix(1.4f, (Marimba(Midi(60)), 0, 0.7f), (Marimba(Midi(67)), 0.07f, 0.6f), (Kalimba(Midi(72)), 0.14f, 0.35f)), 0.55f));
        Cue("hm", 2, Normalize(Mix(0.7f, (Soft(Midi(57), 0.32f, 0.05f, 0.12f), 0, 0.7f), (Soft(Midi(55), 0.36f, 0.06f, 0.2f), 0.24f, 0.6f)), 0.4f));
        Cue("chord", 1, Normalize(Mix(2.6f, (MusicBox(Midi(60)), 0, 1), (MusicBox(Midi(64)), 0.09f, 0.9f), (MusicBox(Midi(67)), 0.18f, 0.85f),
            (MusicBox(Midi(72)), 0.27f, 0.8f), (MusicBox(Midi(74)), 0.36f, 0.7f)), 0.6f));
        Cue("grind", 2, Normalize(Grains(0.6f, 14, 17), 0.5f));
        Cue("whoosh", 2, Normalize(Env(LowPass(Noise(0.6f, NoiseColor.Pink, 21), 300, 2400), 0.25f, 0.18f), 0.45f));
        Cue("bubble", 4, Normalize(Chirp(320, 900, 0.09f, 0.04f), 0.35f), Normalize(Chirp(260, 760, 0.1f, 0.05f), 0.35f), Normalize(Chirp(420, 1100, 0.07f, 0.03f), 0.3f));
        Cue("clink", 3, Normalize(Glass(2093, 0.6f), 0.4f), Normalize(Glass(2349, 0.6f), 0.4f));
        Cue("discover", 1, Normalize(Mix(2.4f, (Bell(Midi(72)), 0, 1), (Bell(Midi(74)), 0.1f, 0.9f), (Bell(Midi(76)), 0.2f, 0.85f),
            (Bell(Midi(79)), 0.3f, 0.8f), (Bell(Midi(84)), 0.42f, 0.7f), (Sparkle(1.2f, 31), 0.3f, 0.25f)), 0.6f));
        Cue("fizz", 2, Normalize(Mix(0.8f, (Chirp(500, 350, 0.08f, 0.04f), 0, 0.5f), (Chirp(440, 300, 0.08f, 0.04f), 0.15f, 0.4f),
            (Chirp(380, 260, 0.09f, 0.05f), 0.3f, 0.3f), (Env(LowPass(Noise(0.5f, NoiseColor.Pink, 4), 2000), 0.02f, 0.2f), 0, 0.15f)), 0.35f));
        Cue("splash", 2, Normalize(Mix(0.9f, (Env(LowPass(Noise(0.8f, NoiseColor.White, 41), 3500, 900), 0.005f, 0.18f), 0, 1),
            (Pluck(70, 0.4f, new[] { (1f, 1f, 0.1f) }), 0, 0.5f)), 0.5f));
        Cue("rake", 2, Normalize(Rake(0.9f), 0.45f));
        Cue("sprinkle", 2, Normalize(Sparkle(0.7f, 51), 0.3f));
        Cue("hatch", 2, Normalize(Mix(1.6f, (Chirp(300, 1000, 0.12f, 0.05f), 0, 0.6f), (Chirp(400, 1200, 0.1f, 0.05f), 0.08f, 0.5f),
            (Glass(Midi(84), 1.2f), 0.18f, 0.5f), (Glass(Midi(88), 1.2f), 0.3f, 0.4f)), 0.5f));
        Cue("bell", 1, Normalize(Bell(Midi(76), 3f), 0.5f));
        Cue("glass", 2, Normalize(Glass(Midi(93), 1.0f), 0.35f));
        Cue("creak", 1, Normalize(Mix(1.2f, (Creak(0.7f), 0, 0.8f), (Glass(Midi(86), 0.8f), 0.6f, 0.4f)), 0.45f));
        Cue("shimmer", 1, Normalize(Shimmer(2.6f), 0.55f));
        Cue("resolved", 1, Normalize(Mix(4f, (Soft(Midi(48), 3.6f, 0.6f, 1.6f), 0, 0.5f), (Soft(Midi(55), 3.4f, 0.7f, 1.6f), 0.1f, 0.4f),
            (Soft(Midi(64), 3.2f, 0.8f, 1.5f), 0.2f, 0.35f), (Soft(Midi(71), 3.0f, 0.9f, 1.4f), 0.3f, 0.25f),
            (MusicBox(Midi(72)), 0.5f, 0.6f), (MusicBox(Midi(76)), 0.75f, 0.55f), (MusicBox(Midi(79)), 1.0f, 0.5f), (MusicBox(Midi(84)), 1.3f, 0.45f)), 0.6f));
        Cue("nearmiss", 1, Normalize(Mix(1.4f, (Soft(Midi(69), 0.45f, 0.06f, 0.2f), 0, 0.7f), (Soft(Midi(65), 0.7f, 0.08f, 0.4f), 0.38f, 0.65f)), 0.4f));
        Cue("notyet", 1, Normalize(Soft(Midi(62), 0.5f, 0.05f, 0.35f), 0.25f));
        Cue("term", 1, Normalize(Mix(1.4f, (Scratch(0.35f), 0, 0.6f), (Glass(Midi(91), 1.0f), 0.3f, 0.45f)), 0.45f));
        Cue("reading", 2, Normalize(Mix(1.8f, (Bell(Midi(76), 1.6f), 0, 0.6f), (Bell(Midi(83), 1.6f), 0.12f, 0.35f)), 0.4f));
        Cue("story", 1, Normalize(Mix(4f, (Env(LowPass(HighPass(Noise(0.35f, NoiseColor.Pink, 61), 700), 5000, 1500), 0.04f, 0.12f), 0, 0.5f),
            (Soft(Midi(57), 3.4f, 0.9f, 1.8f), 0.2f, 0.35f), (Soft(Midi(64), 3.2f, 1.0f, 1.8f), 0.3f, 0.3f), (Soft(Midi(69), 3.0f, 1.1f, 1.8f), 0.4f, 0.25f)), 0.5f));
        Cue("enter", 1, Normalize(Mix(1.4f, (Pluck(140, 0.3f, new[] { (1f, 1f, 0.06f), (2.3f, 0.4f, 0.03f) }), 0, 0.5f),
            (Kalimba(Midi(72)), 0.15f, 0.5f), (Kalimba(Midi(79)), 0.28f, 0.4f)), 0.45f));
        Cue("leave", 1, Normalize(Mix(1.0f, (Kalimba(Midi(79)), 0, 0.4f), (Kalimba(Midi(72)), 0.12f, 0.4f)), 0.35f));
    }

    private float[] Grains(float seconds, int count, int seed)
    {
        var rng = new Random(seed);
        var parts = new List<(float[], float, float)>();
        for (int i = 0; i < count; i++)
            parts.Add((Env(LowPass(Noise(0.05f, NoiseColor.Brown, seed + i), 1800), 0.002f, 0.015f), (float)rng.NextDouble() * (seconds - 0.06f), 0.6f + (float)rng.NextDouble() * 0.4f));
        return Mix(seconds, parts.ToArray());
    }

    private float[] Sparkle(float seconds, int seed)
    {
        var rng = new Random(seed);
        var parts = new List<(float[], float, float)>();
        for (int i = 0; i < 12; i++)
            parts.Add((Glass(2600 + (float)rng.NextDouble() * 2400, 0.25f), (float)rng.NextDouble() * seconds * 0.8f, 0.25f + (float)rng.NextDouble() * 0.3f));
        return Mix(seconds, parts.ToArray());
    }

    private float[] Rake(float seconds)
    {
        var n = HighPass(LowPass(Noise(seconds, NoiseColor.Pink, 71), 2600), 400);
        for (int i = 0; i < n.Length; i++)
        {
            float t = i / (float)Rate;
            n[i] *= (0.6f + 0.4f * MathF.Sin(MathF.Tau * 9 * t)) * MathF.Min(1, t / 0.08f) * MathF.Min(1, (seconds - t) / 0.2f);
        }
        return n;
    }

    private float[] Creak(float seconds)
    {
        var b = Buf(seconds);
        var rng = new Random(81);
        double ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float f = 110 + 30 * MathF.Sin(MathF.Tau * 1.3f * t) + (float)rng.NextDouble() * 8;
            ph += f / Rate;
            float saw = (float)(ph % 1.0) * 2 - 1;
            b[i] = saw * 0.4f * MathF.Min(1, t / 0.05f) * MathF.Min(1, (seconds - t) / 0.2f);
        }
        return LowPass(b, 900);
    }

    private float[] Scratch(float seconds)
    {
        var n = HighPass(Noise(seconds, NoiseColor.White, 91), 3000);
        for (int i = 0; i < n.Length; i++)
        {
            float t = i / (float)Rate;
            n[i] *= (0.5f + 0.5f * MathF.Sin(MathF.Tau * 23 * t + MathF.Sin(MathF.Tau * 3 * t) * 2)) * MathF.Min(1, (seconds - t) / 0.08f);
        }
        return LowPass(n, 7000);
    }

    private float[] Shimmer(float seconds)
    {
        var parts = new List<(float[], float, float)>();
        float[] notes = { 72, 76, 79, 83, 84, 88, 91, 96 };
        for (int i = 0; i < notes.Length; i++) parts.Add((Soft(Midi(notes[i]), seconds - i * 0.18f, 0.5f, 0.8f, 0.006f), i * 0.18f, 0.25f));
        parts.Add((Env(LowPass(HighPass(Noise(seconds, NoiseColor.Pink, 3), 2000), 8000), seconds * 0.7f, 0.4f), 0, 0.2f));
        return Mix(seconds + 0.2f, parts.ToArray());
    }

    /// <summary>Plays a cue (round-robin voices, a touch of random pitch so repeats feel organic).</summary>
    public void Play(string cue, float volume = 1, float pitch = 1)
    {
        if (!Enabled || !_cues.TryGetValue(cue, out var voices)) return;
        int i = _next.TryGetValue(cue, out var n) ? n : 0;
        _next[cue] = (i + 1) % voices.Length;
        var s = voices[i];
        Raylib.SetSoundVolume(s, volume * SfxVolume * Master);
        Raylib.SetSoundPitch(s, pitch * (0.97f + (float)_rng.NextDouble() * 0.06f));
        Raylib.PlaySound(s);
    }

    // ───────────────────────── music ─────────────────────────

    private void BuildMusic()
    {
        Sound[] Notes(Func<float, float[]> voice, params int[] midi) =>
            midi.Select(m =>
            {
                var wave = Raylib.LoadWaveFromMemory(".wav", Wav(Gain(voice(Midi(m)), 0.8f)));
                var s = Raylib.LoadSoundFromWave(wave);
                Raylib.UnloadWave(wave);
                return s;
            }).ToArray();
        _moodNotes["hub"] = (Notes(f => Kalimba(f, 2.2f), 60, 62, 64, 67, 69, 72, 74, 76, 79, 81), 2.6f);
        _moodNotes["library"] = (Notes(f => MusicBox(f, 2.2f), 65, 67, 69, 72, 74, 77, 79, 81, 84), 3.0f);
        _moodNotes["apothecary"] = (Notes(f => Marimba(f, 1.4f), 62, 64, 67, 69, 72, 74, 76, 79), 2.2f);
        _moodNotes["pools"] = (Notes(f => Glass(f, 2.4f), 64, 67, 69, 71, 74, 76, 79, 81, 83), 3.2f);
        _moodNotes["sanctum"] = (Notes(f => Bell(f, 3.0f), 57, 60, 62, 64, 67, 69, 72, 74), 3.6f);
        _moodNotes["title"] = _moodNotes["hub"];
    }

    public void SetMood(string mood)
    {
        if (mood == _mood) return;
        _mood = mood;
        _noteTimer = 1.2f;
        foreach (var (name, l) in _loops) l.Target = LoopTarget(name);
    }

    private float LoopTarget(string name) => (name, _mood) switch
    {
        ("falls", "hub") or ("falls", "title") => 0.55f,
        ("falls", "pools") => 0.38f,
        ("falls", _) => 0.08f,
        ("pad-" + "warm", "hub" or "title" or "library" or "apothecary") => 0.22f,
        ("pad-" + "cool", "pools" or "sanctum") => 0.22f,
        ("room-library", "library") => 0.35f,
        ("room-apothecary", "apothecary") => 0.32f,
        ("room-pools", "pools") => 0.35f,
        ("room-sanctum", "sanctum") => 0.4f,
        _ => 0f,
    };

    private void BuildLoops()
    {
        AddLoop("falls", Seamless(LowPass(Mix(12.5f, (Noise(12.5f, NoiseColor.Pink, 101), 0, 0.9f), (LowPass(Noise(12.5f, NoiseColor.Brown, 102), 180), 0, 0.6f)), 4200), 0.5f));
        AddLoop("pad-warm", Pad(new[] { 48f, 55f, 64f, 71f }, 8));
        AddLoop("pad-cool", Pad(new[] { 45f, 52f, 59f, 64f }, 8));
        AddLoop("room-library", Seamless(Mix(10.5f, (LowPass(Noise(10.5f, NoiseColor.Brown, 111), 300), 0, 0.5f), (Scatter(10.5f, 4, s => Env(LowPass(HighPass(Noise(0.2f, NoiseColor.Pink, s), 900), 4000), 0.03f, 0.06f), 112), 0, 0.25f)), 0.5f));
        AddLoop("room-apothecary", Seamless(Mix(10.5f, (Scatter(10.5f, 26, s => Chirp(250 + s % 7 * 60, 700 + s % 5 * 90, 0.08f, 0.04f), 121), 0, 0.3f), (LowPass(Noise(10.5f, NoiseColor.Brown, 122), 250), 0, 0.3f)), 0.5f));
        AddLoop("room-pools", Seamless(Mix(10.5f, (Scatter(10.5f, 10, s => Chirp(900 + s % 4 * 150, 1600, 0.05f, 0.03f), 131), 0, 0.3f), (LowPass(Noise(10.5f, NoiseColor.Pink, 132), 900), 0, 0.25f)), 0.5f));
        AddLoop("room-sanctum", Pad(new[] { 33f, 40f, 45f }, 8, 0.6f));
    }

    private float[] Scatter(float seconds, int count, Func<int, float[]> make, int seed)
    {
        var rng = new Random(seed);
        var parts = new List<(float[], float, float)>();
        for (int i = 0; i < count; i++) parts.Add((make(seed + i), (float)rng.NextDouble() * (seconds - 0.4f), 0.4f + (float)rng.NextDouble() * 0.6f));
        return Mix(seconds, parts.ToArray());
    }

    /// <summary>A seamless chord drone: frequencies rounded to whole cycles over the loop length.</summary>
    private float[] Pad(float[] notes, float seconds, float gain = 1)
    {
        var b = Buf(seconds);
        foreach (var n in notes)
        {
            float f = MathF.Round(Midi(n) * seconds) / seconds;
            float lfo = MathF.Round(0.25f * seconds) / seconds;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] += MathF.Sin(MathF.Tau * f * t) * (0.6f + 0.4f * MathF.Sin(MathF.Tau * lfo * t + n)) * 0.25f;
            }
        }
        return Gain(Normalize(LowPass(b, 1400), 0.6f), gain);
    }

    private void AddLoop(string name, float[] samples)
    {
        var bytes = Wav(samples);
        var mem = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, mem, bytes.Length);
        Music m;
        unsafe
        {
            fixed (sbyte* ext = new sbyte[] { (sbyte)'.', (sbyte)'w', (sbyte)'a', (sbyte)'v', 0 })
                m = Raylib.LoadMusicStreamFromMemory(ext, (byte*)mem, bytes.Length);
        }
        m.Looping = true;
        Raylib.SetMusicVolume(m, 0);
        Raylib.PlayMusicStream(m);
        _loops[name] = new Loop { Music = m, Memory = mem, Volume = 0, Target = LoopTarget(name) };
    }

    public void Update(float dt)
    {
        if (!Enabled) return;
        _clock += dt;
        foreach (var l in _loops.Values)
        {
            l.Volume += (l.Target - l.Volume) * MathF.Min(1, dt * 0.8f);
            float group = l == _loops.GetValueOrDefault("pad-warm") || l == _loops.GetValueOrDefault("pad-cool") ? MusicVolume : AmbienceVolume;
            Raylib.SetMusicVolume(l.Music, l.Volume * group * Master);
            Raylib.UpdateMusicStream(l.Music);
        }

        // sparse generative melody: a note or a short phrase, then a long, comfortable pause
        while (_phrase.Count > 0 && _phrase.Peek().At <= _clock)
        {
            var (_, s, v) = _phrase.Dequeue();
            Raylib.SetSoundVolume(s, v * MusicVolume * Master);
            Raylib.PlaySound(s);
        }
        _noteTimer -= dt;
        if (_noteTimer > 0 || !_moodNotes.TryGetValue(_mood, out var mood)) return;
        var notes = mood.Notes;
        int idx = _rng.Next(notes.Length);
        int len = _rng.NextDouble() < 0.35 ? 3 : _rng.NextDouble() < 0.5 ? 2 : 1;
        for (int k = 0; k < len; k++)
        {
            _phrase.Enqueue((_clock + k * (0.32f + (float)_rng.NextDouble() * 0.18f), notes[idx], 0.18f + (float)_rng.NextDouble() * 0.08f));
            idx = Math.Clamp(idx + (_rng.Next(3) - 1) * (1 + _rng.Next(2)), 0, notes.Length - 1);
        }
        _noteTimer = mood.Gap + (float)_rng.NextDouble() * mood.Gap * 1.4f;
    }

    public void Dispose()
    {
        if (!Enabled) return;
        foreach (var l in _loops.Values) { Raylib.UnloadMusicStream(l.Music); Marshal.FreeHGlobal(l.Memory); }
        Raylib.CloseAudioDevice();
    }
}
