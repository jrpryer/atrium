namespace Atrium.Diorama.Audio;

/// <summary>Tiny offline synthesiser: every sound in this iteration is built from these at startup.</summary>
public static class Synth
{
    public const int Rate = 44100;

    public static float[] Buf(float seconds) => new float[(int)(seconds * Rate)];

    public static float Midi(float note) => 440f * MathF.Pow(2, (note - 69) / 12f);

    /// <summary>A plucked/struck voice: partials as (ratio, amplitude, decay seconds).</summary>
    public static float[] Pluck(float freq, float seconds, (float Ratio, float Amp, float Decay)[] partials, float attack = 0.004f, float detune = 0)
    {
        var b = Buf(seconds);
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float env = MathF.Min(1, t / attack);
            float v = 0;
            foreach (var (r, a, d) in partials)
            {
                float f = freq * r * (1 + detune * 0.001f);
                v += a * MathF.Sin(MathF.Tau * f * t) * MathF.Exp(-t / d);
            }
            b[i] = v * env;
        }
        return Normalize(b, 0.9f);
    }

    public static float[] Kalimba(float f, float s = 1.6f) => Pluck(f, s, new[] { (1f, 1f, 0.55f), (3.01f, 0.12f, 0.12f), (5.4f, 0.05f, 0.05f), (2f, 0.08f, 0.3f) }, 0.003f);
    public static float[] MusicBox(float f, float s = 1.8f) => Pluck(f, s, new[] { (1f, 1f, 0.7f), (2f, 0.35f, 0.35f), (3f, 0.12f, 0.22f), (4.2f, 0.1f, 0.12f) }, 0.002f);
    public static float[] Marimba(float f, float s = 1.0f) => Pluck(f, s, new[] { (1f, 1f, 0.35f), (4f, 0.25f, 0.06f), (9.2f, 0.05f, 0.03f) }, 0.003f);
    public static float[] Bell(float f, float s = 2.4f) => Pluck(f, s, new[] { (1f, 1f, 1.1f), (2.76f, 0.45f, 0.6f), (5.4f, 0.22f, 0.35f), (8.93f, 0.1f, 0.2f) }, 0.004f);
    public static float[] Glass(float f, float s = 1.6f) => Pluck(f, s, new[] { (1f, 1f, 0.8f), (2.32f, 0.4f, 0.4f), (4.25f, 0.2f, 0.25f) }, 0.002f);

    /// <summary>A soft sustained tone (flute-ish): sine plus a little second harmonic, slow attack and release.</summary>
    public static float[] Soft(float f, float seconds, float attack = 0.08f, float release = 0.3f, float vibrato = 0.004f)
    {
        var b = Buf(seconds);
        double ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float env = MathF.Min(1, t / attack) * MathF.Min(1, (seconds - t) / release);
            float ff = f * (1 + vibrato * MathF.Sin(MathF.Tau * 5.2f * t));
            ph += ff / Rate;
            b[i] = env * (MathF.Sin(MathF.Tau * (float)ph) * 0.8f + MathF.Sin(MathF.Tau * 2 * (float)ph) * 0.12f);
        }
        return b;
    }

    public enum NoiseColor { White, Pink, Brown }

    public static float[] Noise(float seconds, NoiseColor color, int seed = 1)
    {
        var rng = new Random(seed);
        var b = Buf(seconds);
        float b0 = 0, b1 = 0, b2 = 0, brown = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float w = (float)rng.NextDouble() * 2 - 1;
            switch (color)
            {
                case NoiseColor.White: b[i] = w; break;
                case NoiseColor.Pink:
                    b0 = 0.99765f * b0 + w * 0.0990460f; b1 = 0.96300f * b1 + w * 0.2965164f; b2 = 0.57000f * b2 + w * 1.0526913f;
                    b[i] = (b0 + b1 + b2 + w * 0.1848f) * 0.22f; break;
                default:
                    brown = (brown + 0.02f * w) / 1.02f; b[i] = brown * 3.5f; break;
            }
        }
        return b;
    }

    /// <summary>One-pole low-pass (cutoff in Hz), optionally swept from <paramref name="cutoff"/> to <paramref name="cutoffEnd"/>.</summary>
    public static float[] LowPass(float[] x, float cutoff, float? cutoffEnd = null)
    {
        var y = new float[x.Length];
        float prev = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float c = cutoffEnd is null ? cutoff : cutoff + (cutoffEnd.Value - cutoff) * i / x.Length;
            float a = 1 - MathF.Exp(-MathF.Tau * c / Rate);
            prev += a * (x[i] - prev);
            y[i] = prev;
        }
        return y;
    }

    public static float[] HighPass(float[] x, float cutoff)
    {
        var lp = LowPass(x, cutoff);
        var y = new float[x.Length];
        for (int i = 0; i < x.Length; i++) y[i] = x[i] - lp[i];
        return y;
    }

    public static float[] Env(float[] x, float attack, float decay, float? hold = null)
    {
        for (int i = 0; i < x.Length; i++)
        {
            float t = i / (float)Rate;
            float e = MathF.Min(1, t / MathF.Max(attack, 1e-4f));
            float h = hold ?? 0;
            if (t > attack + h) e *= MathF.Exp(-(t - attack - h) / decay);
            x[i] *= e;
        }
        return x;
    }

    /// <summary>A frequency glide (chirp) sine, for bubbles and sighs.</summary>
    public static float[] Chirp(float f0, float f1, float seconds, float decay)
    {
        var b = Buf(seconds);
        double ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float f = f0 + (f1 - f0) * MathF.Pow(t / seconds, 0.6f);
            ph += f / Rate;
            b[i] = MathF.Sin(MathF.Tau * (float)ph) * MathF.Exp(-t / decay) * MathF.Min(1, t / 0.003f);
        }
        return b;
    }

    public static float[] Mix(float seconds, params (float[] Sig, float Offset, float Gain)[] parts)
    {
        var b = Buf(seconds);
        foreach (var (sig, off, gain) in parts)
        {
            int o = (int)(off * Rate);
            for (int i = 0; i < sig.Length && o + i < b.Length; i++) if (o + i >= 0) b[o + i] += sig[i] * gain;
        }
        return b;
    }

    public static float[] Normalize(float[] b, float peak = 0.85f)
    {
        float m = 1e-6f;
        foreach (var v in b) m = MathF.Max(m, MathF.Abs(v));
        for (int i = 0; i < b.Length; i++) b[i] = b[i] / m * peak;
        return b;
    }

    public static float[] Gain(float[] b, float g)
    {
        for (int i = 0; i < b.Length; i++) b[i] *= g;
        return b;
    }

    /// <summary>Makes a buffer loop seamlessly by crossfading its tail into its head.</summary>
    public static float[] Seamless(float[] b, float fadeSeconds)
    {
        int f = (int)(fadeSeconds * Rate);
        var y = new float[b.Length - f];
        Array.Copy(b, y, y.Length);
        for (int i = 0; i < f; i++)
        {
            float t = i / (float)f;
            y[i] = b[i] * t + b[y.Length + i] * (1 - t);
        }
        return y;
    }

    /// <summary>16-bit mono PCM WAV bytes.</summary>
    public static byte[] Wav(float[] samples)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int bytes = samples.Length * 2;
        w.Write("RIFF"u8.ToArray()); w.Write(36 + bytes); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(bytes);
        foreach (var s in samples) w.Write((short)Math.Clamp(s * 32767f, -32768f, 32767f));
        return ms.ToArray();
    }
}
