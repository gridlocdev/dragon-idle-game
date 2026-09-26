using Raylib_cs;

namespace DragonIdle;

/// <summary>Procedurally synthesized sound effects (no asset files needed).</summary>
public class Sfx
{
    public Sound[] Hit = null!, Crit = null!, Coin = null!, Kill = null!, BossKill = null!, LevelUp = null!,
        Milestone = null!, Deny = null!, Ability = null!, Roar = null!, Boom = null!;
    readonly Dictionary<Sound[], int> next = new();
    readonly Dictionary<Sound[], double> lastPlay = new();
    readonly Dictionary<Sound[], float> baseVol = new();
    readonly Random rng = new();
    public bool Muted;
    bool ok;

    const int Rate = 44100;

    public void Init()
    {
        Raylib.InitAudioDevice();
        ok = Raylib.IsAudioDeviceReady();
        if (!ok) return;
        var r = new Random(1);
        float Noise() => (float)(r.NextDouble() * 2 - 1);

        Hit = Make(0.08f, (t, k) => (MathF.Sin(t * 2 * MathF.PI * (220 - 120 * k)) * 0.6f + Noise() * 0.5f) * Env(k, 0.005f), 4, 0.35f);
        Crit = Make(0.18f, (t, k) => (MathF.Sin(t * 2 * MathF.PI * (1100 - 700 * k)) * 0.5f + Noise() * 0.4f * (1 - k)) * Env(k, 0.005f), 3, 0.45f);
        Coin = Make(0.14f, (t, k) => Square(t * (k < 0.35f ? 1320 : 1760)) * 0.3f * Env(k, 0.002f), 4, 0.25f);
        Kill = Make(0.22f, (t, k) => (Noise() * (1 - k) * 0.6f + MathF.Sin(t * 2 * MathF.PI * (300 - 220 * k)) * 0.5f) * Env(k, 0.005f), 3, 0.4f);
        BossKill = Make(0.9f, (t, k) => (Noise() * (1 - k) * 0.5f + MathF.Sin(t * 2 * MathF.PI * (90 - 50 * k)) * 0.9f) * Env(k, 0.01f), 1, 0.7f);
        LevelUp = Make(0.3f, (t, k) => Arp(t, k, new[] { 523f, 659f, 784f }) * 0.4f, 3, 0.4f);
        Milestone = Make(0.9f, (t, k) => (Arp(t, k * 1.6f, new[] { 523f, 659f, 784f, 1047f, 1319f }) * 0.4f
            + MathF.Sin(t * 2 * MathF.PI * 2093) * 0.08f * MathF.Sin(t * 40)) * (1 - k * 0.6f), 2, 0.55f);
        Deny = Make(0.15f, (t, k) => Square(t * 110) * 0.25f * Env(k, 0.005f), 1, 0.35f);
        Ability = Make(0.6f, (t, k) => (Noise() * 0.5f * MathF.Sin(k * MathF.PI) + MathF.Sin(t * 2 * MathF.PI * (200 + 800 * k)) * 0.3f) * Env(k, 0.02f), 2, 0.5f);
        Roar = Make(1.3f, (t, k) => (Noise() * 0.5f + Saw(t * (70 + 30 * MathF.Sin(k * 9)))) * 0.6f * MathF.Sin(MathF.Min(1, k * 4) * MathF.PI / 2) * (1 - k), 1, 0.6f);
        Boom = Make(0.4f, (t, k) => (Noise() * 0.6f + MathF.Sin(t * 2 * MathF.PI * (80 - 40 * k))) * (1 - k) * (1 - k), 4, 0.4f);
    }

    static float Env(float k, float attack) => (k < attack ? k / attack : 1) * (1 - k) * (1 - k);
    static float Square(float phase) => (phase % 1) < 0.5f ? 1 : -1;
    static float Saw(float phase) => (phase % 1) * 2 - 1;

    static float Arp(float t, float k, float[] notes)
    {
        int n = Math.Min(notes.Length - 1, (int)(Math.Min(0.999f, k) * notes.Length));
        float local = k * notes.Length - n;
        return (MathF.Sin(t * 2 * MathF.PI * notes[n]) + 0.3f * Square(t * notes[n])) * (1 - local * 0.6f);
    }

    Sound[] Make(float dur, Func<float, float, float> fn, int voices, float vol)
    {
        int n = (int)(dur * Rate);
        var bytes = new byte[44 + n * 2];
        void W32(int o, int v) => BitConverter.GetBytes(v).CopyTo(bytes, o);
        void W16(int o, short v) => BitConverter.GetBytes(v).CopyTo(bytes, o);
        "RIFF"u8.ToArray().CopyTo(bytes, 0); W32(4, 36 + n * 2);
        "WAVE"u8.ToArray().CopyTo(bytes, 8); "fmt "u8.ToArray().CopyTo(bytes, 12);
        W32(16, 16); W16(20, 1); W16(22, 1); W32(24, Rate); W32(28, Rate * 2); W16(32, 2); W16(34, 16);
        "data"u8.ToArray().CopyTo(bytes, 36); W32(40, n * 2);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float v = Math.Clamp(fn(t, i / (float)n), -1, 1);
            // tiny fade in to kill clicks
            if (i < 64) v *= i / 64f;
            W16(44 + i * 2, (short)(v * 30000));
        }
        var wave = Raylib.LoadWaveFromMemory(".wav", bytes);
        var baseSound = Raylib.LoadSoundFromWave(wave);
        Raylib.UnloadWave(wave);
        var arr = new Sound[voices];
        arr[0] = baseSound;
        for (int i = 1; i < voices; i++) arr[i] = Raylib.LoadSoundAlias(baseSound);
        baseVol[arr] = vol;
        return arr;
    }

    public void Play(Sound[] s, float vol = 1f)
    {
        if (!ok || Muted || s == null) return;
        double now = Raylib.GetTime();
        if (lastPlay.TryGetValue(s, out var lp) && now - lp < 0.04) return;
        lastPlay[s] = now;
        next.TryGetValue(s, out int idx);
        var snd = s[idx % s.Length];
        next[s] = idx + 1;
        Raylib.SetSoundPitch(snd, 0.92f + (float)rng.NextDouble() * 0.16f);
        Raylib.SetSoundVolume(snd, vol * baseVol[s]);
        Raylib.PlaySound(snd);
    }
}
