using System.Numerics;
using Raylib_cs;
using static DragonIdle.Render;

namespace DragonIdle;

/// <summary>All the juice: particles, damage numbers, banners, flying coins, shake and flashes.</summary>
public class Fx
{
    struct Particle
    {
        public Vector3 Pos, Vel;
        public Color Col;
        public float Life, MaxLife, Size, Grav;
        public bool Glow;
    }

    class FloatText
    {
        public Vector3 World;
        public string Text = "";
        public Color Col;
        public float Size, Life, MaxLife, Dx;
    }

    public class BannerMsg
    {
        public string Title = "", Sub = "";
        public Color Col;
        public float Life;
    }

    struct Coin
    {
        public Vector2 Pos, Vel;
        public int Type;
        public float Age, Delay;
    }

    struct PillarFx { public Vector3 Pos; public Color Col; public float Life; }

    readonly List<Particle> parts = new();
    readonly List<FloatText> texts = new();
    public readonly List<BannerMsg> Banners = new();
    readonly List<Coin> coins = new();
    readonly List<PillarFx> pillars = new();
    readonly Random rng = new();

    public float ShakeAmt;
    public Color FlashCol;
    public float FlashAlpha;
    public Func<Vector3, Vector2> Project = v => Vector2.Zero;
    public Func<int, Vector2> IconPos = i => Vector2.Zero;
    public Action<int>? OnCoinArrive;

    float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    Vector3 RandDir() { var v = new Vector3(Rf(-1, 1), Rf(-1, 1), Rf(-1, 1)); return v.LengthSquared() < 0.001f ? Vector3.UnitY : Vector3.Normalize(v); }

    void Add(Vector3 pos, Vector3 vel, Color c, float life, float size, float grav, bool glow)
    {
        if (parts.Count > 2500) return;
        parts.Add(new Particle { Pos = pos, Vel = vel, Col = c, Life = life, MaxLife = life, Size = size, Grav = grav, Glow = glow });
    }

    // ---------- spawners ----------
    public void Sparks(Vector3 p, Color c, int n)
    {
        for (int i = 0; i < n; i++) Add(p, RandDir() * Rf(2, 6), c, Rf(0.2f, 0.45f), Rf(0.05f, 0.1f), 6, true);
    }

    public void Burst3D(Vector3 p, Color c, int n, float speed)
    {
        for (int i = 0; i < n; i++)
        {
            var d = RandDir(); d.Y = Math.Abs(d.Y) + 0.2f;
            Add(p, d * Rf(speed * 0.3f, speed), i % 3 == 0 ? Color.White : c, Rf(0.5f, 1.2f), Rf(0.08f, 0.2f), 7, i % 2 == 0);
        }
    }

    public void LevelUpRing(Vector3 p, Color c)
    {
        for (int i = 0; i < 28; i++)
        {
            float a = i / 28f * MathF.PI * 2;
            Add(p + new Vector3(MathF.Cos(a) * 0.6f, 0.1f, MathF.Sin(a) * 0.6f), new Vector3(MathF.Cos(a) * 0.8f, Rf(2, 4), MathF.Sin(a) * 0.8f),
                i % 2 == 0 ? new Color(255, 240, 140, 255) : c, Rf(0.6f, 1.0f), 0.08f, -1, true);
        }
    }

    public void Pillar(Vector3 p, Color c) => pillars.Add(new PillarFx { Pos = p, Col = c, Life = 1.2f });

    public void Impact(Vector3 p, Color c, ProjKind k)
    {
        int n = k switch { ProjKind.Meteor => 30, ProjKind.Lightning => 14, ProjKind.DragonFire => 12, _ => 7 };
        for (int i = 0; i < n; i++) Add(p, RandDir() * Rf(1.5f, k == ProjKind.Meteor ? 8 : 4), c, Rf(0.2f, 0.5f), Rf(0.06f, 0.14f), 5, true);
    }

    public void Trail(Projectile pr)
    {
        var pos = ProjPos(pr);
        switch (pr.Kind)
        {
            case ProjKind.Fireball:
            case ProjKind.Meteor:
            case ProjKind.DragonFire:
            case ProjKind.SunFlare:
                for (int i = 0; i < (pr.Kind == ProjKind.Meteor ? 3 : 1); i++)
                    Add(pos + RandDir() * 0.1f, RandDir() * 0.4f, rng.Next(2) == 0 ? pr.Color : new Color(255, 230, 120, 255), Rf(0.2f, 0.4f), Rf(0.08f, 0.16f), -1.5f, true);
                break;
            case ProjKind.IceShard:
            case ProjKind.VoidOrb:
            case ProjKind.SkullBolt:
            case ProjKind.TimeRune:
                Add(pos, RandDir() * 0.3f, pr.Color, 0.3f, 0.06f, 0, true);
                break;
        }
    }

    public void WorldText(Vector3 p, string s, Color c, float size, float life)
    {
        if (texts.Count > 80) texts.RemoveAt(0);
        texts.Add(new FloatText { World = p, Text = s, Col = c, Size = size, Life = life, MaxLife = life, Dx = Rf(-25, 25) });
    }

    public void Banner(string title, string sub, Color c)
    {
        Banners.Insert(0, new BannerMsg { Title = title, Sub = sub, Col = c, Life = 3.2f });
        if (Banners.Count > 3) Banners.RemoveAt(Banners.Count - 1);
    }

    public void CoinBurst(Vector3 worldPos, int type, int n)
    {
        var sp = Project(worldPos);
        for (int i = 0; i < Math.Min(n, 30); i++)
        {
            float a = Rf(0, MathF.PI * 2);
            coins.Add(new Coin { Pos = sp, Vel = new Vector2(MathF.Cos(a), MathF.Sin(a) - 0.6f) * Rf(150, 420), Type = type, Delay = Rf(0.25f, 0.5f) });
        }
    }

    public void Shake(float a) => ShakeAmt = Math.Min(1.2f, ShakeAmt + a);

    public void Flash(Color c, float a) { FlashCol = c; FlashAlpha = Math.Max(FlashAlpha, a); }

    public void Ambient(int biome, float dt)
    {
        var b = Defs.Biomes[biome];
        if (rng.NextDouble() > dt * 30) return;
        var p = new Vector3(Rf(-12, 8), Rf(0.2f, 6), Rf(-8, 5));
        switch (biome)
        {
            case 2: Add(new Vector3(p.X, 0.1f, p.Z), new Vector3(Rf(-0.3f, 0.3f), Rf(1, 2.5f), 0), new Color(255, 140, 40, 255), 2.5f, 0.05f, 0, true); break;
            case 3: Add(new Vector3(p.X, 7, p.Z), new Vector3(Rf(-0.5f, 0.5f), -1.2f, 0), Color.White, 6, 0.06f, 0, false); break;
            default: Add(p, new Vector3(Rf(-0.3f, 0.3f), Rf(-0.2f, 0.4f), Rf(-0.3f, 0.3f)), b.Accent, 3, 0.05f, 0, true); break;
        }
    }

    // ---------- update ----------
    public void Update(float dt)
    {
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            var p = parts[i];
            p.Life -= dt;
            if (p.Life <= 0) { parts.RemoveAt(i); continue; }
            p.Vel.Y -= p.Grav * dt;
            p.Vel *= 1 - dt * 1.5f;
            p.Pos += p.Vel * dt;
            if (p.Pos.Y < 0.02f && p.Grav > 0) { p.Pos.Y = 0.02f; p.Vel.Y *= -0.4f; }
            parts[i] = p;
        }
        for (int i = texts.Count - 1; i >= 0; i--) { texts[i].Life -= dt; if (texts[i].Life <= 0) texts.RemoveAt(i); }
        for (int i = Banners.Count - 1; i >= 0; i--) { Banners[i].Life -= dt; if (Banners[i].Life <= 0) Banners.RemoveAt(i); }
        for (int i = pillars.Count - 1; i >= 0; i--)
        {
            var p = pillars[i]; p.Life -= dt;
            if (p.Life <= 0) pillars.RemoveAt(i); else pillars[i] = p;
        }
        for (int i = coins.Count - 1; i >= 0; i--)
        {
            var c = coins[i];
            c.Age += dt;
            if (c.Age < c.Delay)
            {
                c.Vel.Y += 900 * dt;
                c.Vel *= 1 - dt * 2.5f;
                c.Pos += c.Vel * dt;
            }
            else
            {
                var target = IconPos(c.Type);
                var d = target - c.Pos;
                float dist = d.Length();
                float speed = 300 + (c.Age - c.Delay) * 2400;
                if (dist < speed * dt + 8) { coins.RemoveAt(i); OnCoinArrive?.Invoke(c.Type); continue; }
                c.Pos += d / dist * speed * dt;
            }
            coins[i] = c;
        }
        ShakeAmt = Math.Max(0, ShakeAmt - dt * 2.2f);
        FlashAlpha = Math.Max(0, FlashAlpha - dt * 1.8f);
    }

    public Vector3 ShakeOffset(float t)
    {
        float s = ShakeAmt * ShakeAmt * 0.35f;
        return new Vector3(MathF.Sin(t * 53) * s, MathF.Sin(t * 47 + 1) * s, MathF.Sin(t * 41 + 2) * s * 0.5f);
    }

    // ---------- 3D drawing ----------
    public static Vector3 ProjPos(Projectile p)
    {
        float t = Math.Clamp(p.T, 0, 1);
        return Vector3.Lerp(p.From, p.To, t) + new Vector3(0, p.Arc * 4 * t * (1 - t), 0);
    }

    public void DrawProjectiles(Game g, float time)
    {
        foreach (var p in g.Projectiles)
        {
            var pos = ProjPos(p);
            var ahead = ProjPos(new Projectile { From = p.From, To = p.To, T = p.T + 0.05f, Arc = p.Arc });
            var d = ahead - pos;
            float yaw = MathF.Atan2(-d.Z, d.X) * 180 / MathF.PI;
            float pitch = MathF.Atan2(d.Y, new Vector2(d.X, d.Z).Length()) * 180 / MathF.PI;
            switch (p.Kind)
            {
                case ProjKind.Arrow:
                    P(pos); RY(yaw); RZ(pitch);
                    Box(0, 0, 0, 0.7f, 0.035f, 0.035f, new Color(140, 100, 60, 255));
                    Box(0.38f, 0, 0, 0.1f, 0.07f, 0.07f, new Color(200, 200, 210, 255));
                    Box(-0.3f, 0, 0, 0.14f, 0.1f, 0.02f, p.Color);
                    Pop();
                    break;
                case ProjKind.Spear:
                    P(pos); RY(yaw); RZ(pitch);
                    SetGlow(1); Box(0, 0, 0, 0.9f, 0.08f, 0.2f, p.Color); SetGlow(0);
                    Orb(0, 0, 0, 0.12f, p.Color, 3);
                    Pop();
                    break;
                case ProjKind.Axe:
                    P(pos); RY(yaw); RZ(-time * 900);
                    Box(0, 0, 0, 0.5f, 0.06f, 0.06f, new Color(110, 70, 40, 255));
                    Box(0.22f, 0.1f, 0, 0.18f, 0.28f, 0.04f, p.Color);
                    Pop();
                    break;
                case ProjKind.IceShard:
                    P(pos); RY(yaw); RZ(pitch - 90);
                    SetGlow(0.7f); Cone(0, -0.3f, 0, 0.1f, 0.6f, p.Color); SetGlow(0);
                    Pop();
                    Orb(pos.X, pos.Y, pos.Z, 0.05f, p.Color, 4);
                    break;
                case ProjKind.SkullBolt:
                    Sph(pos.X, pos.Y, pos.Z, 0.14f, new Color(235, 230, 210, 255));
                    Orb(pos.X + 0.1f, pos.Y + 0.03f, pos.Z, 0.05f, p.Color, 5);
                    break;
                case ProjKind.Lightning:
                    DrawBolt(p.From, p.To, p.Color, time);
                    break;
                case ProjKind.TimeRune:
                    P(pos); RY(time * 300); SetGlow(1); Torus(0, 0, 0, 0.22f, p.Color); SetGlow(0); Pop();
                    Orb(pos.X, pos.Y, pos.Z, 0.08f, p.Color, 3);
                    break;
                case ProjKind.Meteor:
                    Sph(pos.X, pos.Y, pos.Z, 0.35f, new Color(90, 50, 40, 255));
                    Orb(pos.X, pos.Y, pos.Z, 0.38f, p.Color, 2.5f);
                    break;
                case ProjKind.VoidOrb:
                    Sph(pos.X, pos.Y, pos.Z, 0.14f, new Color(10, 0, 20, 255));
                    Orb(pos.X, pos.Y, pos.Z, 0.2f, p.Color, 2.5f);
                    break;
                default:
                    Orb(pos.X, pos.Y, pos.Z, p.Kind == ProjKind.SunFlare ? 0.25f : 0.18f, p.Color, 3);
                    break;
            }
        }
    }

    void DrawBolt(Vector3 a, Vector3 b, Color c, float time)
    {
        var r = new Random((int)(time * 30));
        var prev = a;
        Raylib.BeginBlendMode(BlendMode.Additive);
        for (int i = 1; i <= 7; i++)
        {
            var next = Vector3.Lerp(a, b, i / 7f);
            if (i < 7) next += new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f) * 0.6f;
            Raylib.DrawCylinderEx(prev, next, 0.07f, 0.07f, 5, c);
            Raylib.DrawCylinderEx(prev, next, 0.025f, 0.025f, 5, Color.White);
            prev = next;
        }
        Raylib.EndBlendMode();
    }

    public void DrawParticles()
    {
        foreach (var p in parts)
            if (!p.Glow)
            {
                float k = p.Life / p.MaxLife;
                Raylib.DrawCube(p.Pos, p.Size, p.Size, p.Size, p.Col);
            }
        Rlgl.DisableDepthMask();
        Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (var p in parts)
            if (p.Glow)
            {
                float k = Math.Clamp(p.Life / p.MaxLife, 0, 1);
                float s = p.Size * (0.4f + 0.6f * k);
                Raylib.DrawCube(p.Pos, s, s, s, new Color(p.Col.R, p.Col.G, p.Col.B, (byte)(255 * k)));
            }
        foreach (var pl in pillars)
        {
            float k = pl.Life / 1.2f;
            Raylib.DrawCylinder(pl.Pos, 0.6f * k, 0.9f * k, 12, 16, new Color(pl.Col.R, pl.Col.G, pl.Col.B, (byte)(90 * k)));
            Raylib.DrawCylinder(pl.Pos, 0.25f * k, 0.4f * k, 12, 12, new Color((byte)255, (byte)255, (byte)230, (byte)(120 * k)));
        }
        Raylib.EndBlendMode();
        Rlgl.EnableDepthMask();
    }

    // ---------- 2D drawing ----------
    public void DrawTexts()
    {
        foreach (var t in texts)
        {
            float age = t.MaxLife - t.Life;
            float k = t.Life / t.MaxLife;
            var sp = Project(t.World);
            float pop = age < 0.12f ? 0.6f + age / 0.12f * 0.6f : age < 0.22f ? 1.2f - (age - 0.12f) / 0.1f * 0.2f : 1f;
            float size = t.Size * pop;
            var pos = new Vector2(sp.X + t.Dx * age * 2, sp.Y - age * 70);
            byte al = (byte)(255 * Math.Min(1, k * 2.5f));
            Ui.TextCentered(t.Text, pos, size, new Color(t.Col.R, t.Col.G, t.Col.B, al), true);
        }
    }

    public void DrawCoins()
    {
        foreach (var c in coins) Ui.CurrencyIcon(c.Type, c.Pos, 9, 1);
    }

    public void DrawBanners(Rectangle area, float time)
    {
        float y = area.Y + 110;
        foreach (var b in Banners)
        {
            float age = 3.2f - b.Life;
            float s = age < 0.15f ? age / 0.15f * 1.15f : age < 0.3f ? 1.15f - (age - 0.15f) / 0.15f * 0.15f : 1;
            float a = Math.Min(1, b.Life / 0.5f);
            float w = Math.Max(Ui.Measure(b.Title, 40), Ui.Measure(b.Sub, 20)) + 80;
            var r = new Rectangle(area.X + area.Width / 2 - w * s / 2, y, w * s, 84 * s);
            Raylib.DrawRectangleRounded(r, 0.35f, 8, new Color((byte)15, (byte)10, (byte)25, (byte)(210 * a)));
            Raylib.DrawRectangleRoundedLinesEx(r, 0.35f, 8, 3, new Color(b.Col.R, b.Col.G, b.Col.B, (byte)(255 * a)));
            float shimmer = 0.75f + 0.25f * MathF.Sin(time * 8);
            Ui.TextCentered(b.Title, new Vector2(r.X + r.Width / 2, r.Y + 28 * s), 40 * s,
                new Color((byte)Math.Min(255, b.Col.R * shimmer + 60), (byte)Math.Min(255, b.Col.G * shimmer + 60), (byte)Math.Min(255, b.Col.B * shimmer + 60), (byte)(255 * a)), true);
            Ui.TextCentered(b.Sub, new Vector2(r.X + r.Width / 2, r.Y + 62 * s), 20 * s, new Color((byte)230, (byte)230, (byte)240, (byte)(255 * a)), false);
            y += 94 * s;
        }
    }
}
