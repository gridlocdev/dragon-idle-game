using System.Numerics;
using Raylib_cs;

namespace DragonIdle;

/// <summary>Immediate-mode 2D UI: top bar, shop panel, stage header, ability bar.</summary>
public static class Ui
{
    public static Font Font;
    static bool customFont;

    public const int TopH = 66;
    public static int PanelW => Math.Clamp(Raylib.GetScreenWidth() * 38 / 100, 460, 580);
    public static Rectangle Battle => new(0, TopH, Raylib.GetScreenWidth() - PanelW, Raylib.GetScreenHeight() - TopH);
    public static Rectangle Panel => new(Raylib.GetScreenWidth() - PanelW, TopH, PanelW, Raylib.GetScreenHeight() - TopH);

    static readonly Color Bg = new(22, 18, 34, 255);
    static readonly Color Card = new(38, 32, 58, 255);
    static readonly Color CardHi = new(52, 44, 78, 255);
    static readonly Color Txt = new(235, 230, 245, 255);
    static readonly Color Dim = new(150, 140, 175, 255);
    static readonly Color GoldC = new(255, 205, 60, 255);
    static readonly Color Green = new(110, 235, 120, 255);

    public static readonly Color[] CurrencyColors =
    {
        new(255, 205, 60, 255), new(90, 230, 255, 255), new(255, 110, 70, 255), new(200, 120, 255, 255),
    };

    static readonly Vector2[] iconPos = new Vector2[4];
    static readonly float[] iconPulse = new float[4];
    static readonly double[] shownLog = { double.NaN, double.NaN, double.NaN, double.NaN };

    static int tab;
    static readonly float[] scroll = new float[4];
    static readonly float[] contentH = new float[4];
    static float ascendConfirm;
    static double pressTime = -1;
    static double lastRepeat;
    static float shownHpFrac = 1;
    static Enemy? lastEnemy;
    public static bool MouseOverUi;
    /// <summary>While a modal (the pause menu) is open, only modal widgets react to the mouse.</summary>
    public static bool Modal;

    public static void Init()
    {
        string[] candidates =
        {
            "/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf",
            "/System/Library/Fonts/Supplemental/Trebuchet MS Bold.ttf",
            "C:/Windows/Fonts/trebucbd.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        };
        foreach (var f in candidates)
        {
            if (!File.Exists(f)) continue;
            Font = Raylib.LoadFontEx(f, 64, null!, 0);
            if (Font.Texture.Id != 0)
            {
                Raylib.GenTextureMipmaps(ref Font.Texture);
                Raylib.SetTextureFilter(Font.Texture, TextureFilter.Trilinear);
                customFont = true;
                break;
            }
        }
        if (!customFont) Font = Raylib.GetFontDefault();
    }

    public static Vector2 IconPos(int type) => iconPos[Math.Clamp(type, 0, 3)];
    public static void Pulse(int type) => iconPulse[type] = 1;

    // ---------- text ----------
    static float Spacing(float size) => customFont ? 0.5f : size / 10;

    public static float Measure(string s, float size) => Raylib.MeasureTextEx(Font, s, size, Spacing(size)).X;

    public static void Text(string s, float x, float y, float size, Color c, bool shadow = false)
    {
        if (shadow) Raylib.DrawTextEx(Font, s, new Vector2(x + 2, y + 2), size, Spacing(size), new Color((byte)0, (byte)0, (byte)0, (byte)(c.A * 0.7f)));
        Raylib.DrawTextEx(Font, s, new Vector2(x, y), size, Spacing(size), c);
    }

    public static void TextCentered(string s, Vector2 center, float size, Color c, bool shadow)
    {
        var m = Raylib.MeasureTextEx(Font, s, size, Spacing(size));
        if (shadow)
        {
            var sh = new Color((byte)0, (byte)0, (byte)0, (byte)(c.A * 0.8f));
            for (int k = 0; k < 4; k++)
            {
                var o = new Vector2(k % 2 == 0 ? -2 : 2, k < 2 ? -2 : 2);
                Raylib.DrawTextEx(Font, s, center - m / 2 + o, size, Spacing(size), sh);
            }
        }
        Raylib.DrawTextEx(Font, s, center - m / 2, size, Spacing(size), c);
    }

    static void TextRight(string s, float right, float y, float size, Color c) => Text(s, right - Measure(s, size), y, size, c);

    // ---------- icons ----------
    public static void CurrencyIcon(int type, Vector2 c, float r, float alpha)
    {
        byte a = (byte)(255 * alpha);
        var col = CurrencyColors[type];
        col.A = a;
        switch (type)
        {
            case 0:
                Raylib.DrawCircleV(c, r, new Color((byte)180, (byte)120, (byte)20, a));
                Raylib.DrawCircleV(c, r * 0.8f, col);
                Raylib.DrawCircleV(c - new Vector2(r * 0.25f, r * 0.25f), r * 0.25f, new Color((byte)255, (byte)250, (byte)200, a));
                break;
            case 1:
                Raylib.DrawPoly(c, 4, r * 1.1f, 0, new Color((byte)30, (byte)120, (byte)160, a));
                Raylib.DrawPoly(c, 4, r * 0.85f, 0, col);
                Raylib.DrawPoly(c - new Vector2(r * 0.2f, r * 0.2f), 4, r * 0.3f, 0, new Color((byte)230, (byte)255, (byte)255, a));
                break;
            case 2:
                Raylib.DrawTriangle(c + new Vector2(0, -r * 1.2f), c + new Vector2(-r * 0.85f, r * 0.2f), c + new Vector2(r * 0.85f, r * 0.2f), col);
                Raylib.DrawCircleV(c + new Vector2(0, r * 0.2f), r * 0.85f, col);
                Raylib.DrawCircleV(c + new Vector2(-r * 0.25f, 0), r * 0.25f, new Color((byte)255, (byte)210, (byte)180, a));
                break;
            default:
                Raylib.DrawCircleV(c + new Vector2(0, r * 0.25f), r * 0.8f, col);
                Raylib.DrawTriangle(c + new Vector2(0, -r * 1.3f), c + new Vector2(-r * 0.8f, r * 0.2f), c + new Vector2(r * 0.8f, r * 0.2f), col);
                Raylib.DrawCircleV(c + new Vector2(0, r * 0.35f), r * 0.4f, new Color((byte)250, (byte)220, (byte)255, a));
                break;
        }
    }

    // ---------- widgets ----------
    static bool Hover(Rectangle r, bool modal = false) => (modal || !Modal) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);

    /// <summary>Returns true on click, and repeatedly while held (for bulk buying).</summary>
    static bool Button(Rectangle r, Color baseCol, bool enabled, bool repeat = false, bool modal = false)
    {
        bool hover = Hover(r, modal);
        var col = enabled ? baseCol : new Color(70, 64, 88, 255);
        if (hover && enabled) col = Render.Mul(col, 1.15f);
        bool down = hover && Raylib.IsMouseButtonDown(MouseButton.Left);
        var rr = down ? new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4) : r;
        Raylib.DrawRectangleRounded(new Rectangle(rr.X, rr.Y + 3, rr.Width, rr.Height), 0.3f, 6, Render.Mul(col, 0.55f));
        Raylib.DrawRectangleRounded(rr, 0.3f, 6, col);
        Raylib.DrawRectangleRounded(new Rectangle(rr.X + 3, rr.Y + 2, rr.Width - 6, rr.Height * 0.4f), 0.4f, 6, new Color(255, 255, 255, enabled ? 28 : 10));
        if (!hover) return false;
        double now = Raylib.GetTime();
        if (Raylib.IsMouseButtonPressed(MouseButton.Left)) { pressTime = now; lastRepeat = now; return true; }
        if (repeat && Raylib.IsMouseButtonDown(MouseButton.Left) && pressTime > 0 && now - pressTime > 0.45 && now - lastRepeat > 0.07)
        {
            lastRepeat = now;
            return true;
        }
        return false;
    }

    static void Bar(Rectangle r, float frac, Color c, Color bg)
    {
        Raylib.DrawRectangleRounded(r, 0.5f, 6, bg);
        if (frac > 0.001f)
            Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y, Math.Max(r.Height, r.Width * Math.Clamp(frac, 0, 1)), r.Height), 0.5f, 6, c);
    }

    static void Portrait(Vector2 c, float r, Color main, Color ring, string letter)
    {
        Raylib.DrawCircleV(c, r + 3, ring);
        Raylib.DrawCircleV(c, r, main);
        Raylib.DrawCircleV(c - new Vector2(r * 0.3f, r * 0.35f), r * 0.35f, new Color(255, 255, 255, 40));
        TextCentered(letter, c, r * 1.1f, Color.White, true);
    }

    // ---------- top bar ----------
    public static void Update(float dt)
    {
        for (int i = 0; i < 4; i++) iconPulse[i] = Math.Max(0, iconPulse[i] - dt * 4);
        ascendConfirm = Math.Max(0, ascendConfirm - dt);
        if (Raylib.IsMouseButtonReleased(MouseButton.Left)) pressTime = -1;
    }

    static string Smooth(int i, BigNum v, float dt)
    {
        double target = v.IsPositive ? v.Log10() : -1;
        if (double.IsNaN(shownLog[i]) || target < shownLog[i] - 0.5 || target < 0) shownLog[i] = target;
        else shownLog[i] += (target - shownLog[i]) * Math.Min(1, dt * 9);
        if (shownLog[i] < 0) return "0";
        if (Math.Abs(target - shownLog[i]) < 1e-4) return v.Floor().Format();
        return BigNum.FromLog10(shownLog[i]).Floor().Format();
    }

    public static void DrawTopBar(Game g, float dt)
    {
        int w = Raylib.GetScreenWidth();
        Raylib.DrawRectangleGradientV(0, 0, w, TopH, new Color(40, 28, 62, 255), new Color(24, 18, 38, 255));
        Raylib.DrawRectangle(0, TopH - 3, w, 3, new Color(255, 190, 70, 160));
        BigNum[] vals = { g.Gold, g.Gems, g.Scales, g.Souls };
        string[] names = { "GOLD", "GEMS", "DRAGON SCALES", "DRAGON SOULS" };
        float x = 16;
        float slot = Math.Min(250, (w - 360) / 4f);
        for (int i = 0; i < 4; i++)
        {
            var c = new Vector2(x + 22, TopH / 2f);
            iconPos[i] = c;
            float pr = 16 * (1 + iconPulse[i] * 0.35f);
            if (iconPulse[i] > 0) Raylib.DrawCircleV(c, pr + 10 * iconPulse[i], new Color(CurrencyColors[i].R, CurrencyColors[i].G, CurrencyColors[i].B, (byte)(90 * iconPulse[i])));
            CurrencyIcon(i, c, pr, 1);
            Text(names[i], x + 48, 9, 13, Dim);
            Text(Smooth(i, vals[i], dt), x + 48, 23, 28, i == 0 ? GoldC : Txt, true);
            if (i == 0 && g.GoldPerSec.IsPositive) Text($"+{g.GoldPerSec}/s", x + 48 + Measure(Smooth(0, vals[0], 0), 28) + 8, 32, 15, Green);
            x += slot;
        }
        TextRight($"DPS  {g.TotalDps}", w - 16, 10, 22, new Color(255, 150, 90, 255));
        TextRight($"CLICK  {g.ClickDamage}", w - 16, 36, 18, new Color(200, 220, 255, 255));
    }

    // ---------- battle overlay ----------
    public static void DrawBattleOverlay(Game g, float t, float dt)
    {
        var b = Battle;
        float cx = b.X + b.Width / 2;
        var biome = Defs.Biomes[Defs.BiomeIndex(g.Stage)];

        // Stage header
        var hdr = new Rectangle(cx - 190, b.Y + 10, 380, 60);
        Raylib.DrawRectangleRounded(hdr, 0.4f, 8, new Color(15, 10, 25, 190));
        if (Button(new Rectangle(hdr.X + 8, hdr.Y + 12, 36, 36), new Color(80, 70, 110, 255), g.Stage > 1)) g.ChangeStage(g.Stage - 1);
        TextCentered("<", new Vector2(hdr.X + 26, hdr.Y + 30), 24, Txt, false);
        if (Button(new Rectangle(hdr.X + hdr.Width - 44, hdr.Y + 12, 36, 36), new Color(80, 70, 110, 255), g.Stage < g.MaxStage)) g.ChangeStage(g.Stage + 1);
        TextCentered(">", new Vector2(hdr.X + hdr.Width - 26, hdr.Y + 30), 24, Txt, false);
        string stageLabel = Game.IsDragonStage(g.Stage) ? $"STAGE {g.Stage}  -  DRAGON" : Game.IsBossStage(g.Stage) ? $"STAGE {g.Stage}  -  BOSS" : $"STAGE {g.Stage}";
        TextCentered(stageLabel, new Vector2(cx, hdr.Y + 20), 26, Game.IsBossStage(g.Stage) ? new Color(255, 120, 90, 255) : Txt, true);
        string realm = biome.Name + (Defs.BiomeCycle(g.Stage) > 0 ? " " + Defs.Roman(Defs.BiomeCycle(g.Stage) + 1) : "");
        TextCentered(realm, new Vector2(cx, hdr.Y + 45), 15, biome.Accent, false);

        // Progress: kill pips or boss timer
        float py = hdr.Y + hdr.Height + 10;
        if (Game.IsBossStage(g.Stage) && g.Enemy.IsBoss)
        {
            float frac = g.BossTimerMax > 0 ? g.BossTimer / g.BossTimerMax : 0;
            var br = new Rectangle(cx - 150, py, 300, 14);
            Bar(br, frac, frac < 0.3f && (int)(t * 6) % 2 == 0 ? new Color(255, 60, 60, 255) : new Color(230, 90, 60, 255), new Color(20, 10, 20, 200));
            TextCentered($"{g.BossTimer:0.0}s", new Vector2(cx, py + 7), 13, Color.White, true);
        }
        else if (g.Stage >= g.MaxStage)
        {
            for (int i = 0; i < Game.KillsPerStage; i++)
            {
                var pc = new Vector2(cx - 99 + i * 22, py + 7);
                bool done = i < g.Kills;
                Raylib.DrawCircleV(pc, 8, new Color(15, 10, 25, 200));
                if (done) Raylib.DrawCircleV(pc, 6, GoldC);
            }
        }
        else TextCentered("Stage cleared - farming", new Vector2(cx, py + 7), 14, Dim, true);

        // Auto-advance / fight boss
        var ab = new Rectangle(hdr.X + hdr.Width + 10, hdr.Y + 12, 120, 36);
        if (!g.AutoAdvance && g.MaxStage > g.Stage || !g.AutoAdvance && Game.IsBossStage(g.MaxStage) && g.Stage == g.MaxStage)
        {
            float pulse = 0.85f + 0.15f * MathF.Sin(t * 6);
            if (Button(ab, Render.Mul(new Color(200, 60, 50, 255), pulse), true)) g.ChallengeBoss();
            TextCentered("FIGHT BOSS!", new Vector2(ab.X + ab.Width / 2, ab.Y + 18), 17, Color.White, true);
        }
        else
        {
            if (Button(ab, g.AutoAdvance ? new Color(60, 140, 80, 255) : new Color(90, 80, 110, 255), true)) g.AutoAdvance = !g.AutoAdvance;
            TextCentered(g.AutoAdvance ? "AUTO: ON" : "AUTO: OFF", new Vector2(ab.X + ab.Width / 2, ab.Y + 18), 16, Color.White, true);
        }

        // Enemy HP bar
        var e = g.Enemy;
        if (e != lastEnemy) { lastEnemy = e; shownHpFrac = 1; }
        float hpFrac = e.MaxHp.IsPositive ? (float)Math.Clamp((e.Hp / e.MaxHp).ToDouble(), 0, 1) : 0;
        shownHpFrac = Math.Max(hpFrac, shownHpFrac - dt * Math.Max(0.6f, (shownHpFrac - hpFrac) * 4));
        var hb = new Rectangle(cx - 220, py + 26, 440, 22);
        Raylib.DrawRectangleRounded(new Rectangle(hb.X - 3, hb.Y - 3, hb.Width + 6, hb.Height + 6), 0.5f, 6, new Color(10, 5, 15, 220));
        Bar(hb, shownHpFrac, new Color(255, 240, 200, 255), new Color(50, 20, 30, 255));
        Bar(hb, hpFrac, e.IsDragon ? new Color(230, 70, 200, 255) : e.IsBoss ? new Color(230, 80, 50, 255) : new Color(220, 50, 60, 255), new Color(0, 0, 0, 0));
        TextCentered($"{BigNum.Max(BigNum.Zero, e.Hp).Ceil()} / {e.MaxHp}", new Vector2(cx, hb.Y + 11), 15, Color.White, true);
        TextCentered(e.Name, new Vector2(cx, hb.Y + hb.Height + 16), 20, e.IsBoss ? new Color(255, 190, 90, 255) : Txt, true);

        // Onboarding hint
        if (g.Clicks < 25)
        {
            float a = 0.6f + 0.4f * MathF.Sin(t * 5);
            TextCentered("CLICK THE BATTLEFIELD TO ATTACK!", new Vector2(cx, b.Y + b.Height - 150), 26, new Color((byte)255, (byte)230, (byte)120, (byte)(255 * a)), true);
        }

        DrawAbilities(g, t);

        // Active buffs readout
        float bx = b.X + 14, by = b.Y + b.Height - 40;
        Text($"Crit {g.CritChance() * 100:0}%  x{g.CritMult():0.#}", bx, by, 15, Dim, true);
        Text($"Best stage {g.BestStage}   Ascensions {g.Ascensions}", bx, by + 18, 13, Dim, true);
    }

    static void DrawAbilities(Game g, float t)
    {
        var b = Battle;
        int n = Defs.Abilities.Length;
        float size = 66, gap = 12;
        float total = n * size + (n - 1) * gap;
        float x0 = b.X + b.Width / 2 - total / 2;
        float y = b.Y + b.Height - size - 34;
        for (int i = 0; i < n; i++)
        {
            var a = Defs.Abilities[i];
            var r = new Rectangle(x0 + i * (size + gap), y, size, size);
            bool unlocked = g.AbilityUnlocked(i);
            bool ready = unlocked && g.AbilityCd[i] <= 0;
            bool active = g.AbilityActive[i] > 0;
            if (active)
            {
                float glow = 0.5f + 0.5f * MathF.Sin(t * 10);
                Raylib.DrawRectangleRounded(new Rectangle(r.X - 6, r.Y - 6, r.Width + 12, r.Height + 12), 0.35f, 8, new Color(a.Color.R, a.Color.G, a.Color.B, (byte)(120 + 100 * glow)));
            }
            else if (ready)
            {
                float glow = 0.5f + 0.5f * MathF.Sin(t * 4 + i);
                Raylib.DrawRectangleRounded(new Rectangle(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6), 0.35f, 8, new Color(a.Color.R, a.Color.G, a.Color.B, (byte)(60 + 80 * glow)));
            }
            if (Button(r, unlocked ? Render.Mul(a.Color, ready ? 0.75f : 0.35f) : new Color(45, 40, 60, 255), unlocked) && unlocked) g.UseAbility(i);
            AbilityGlyph(i, new Vector2(r.X + size / 2, r.Y + size / 2 - 4), unlocked ? Color.White : Dim);
            if (unlocked && g.AbilityCd[i] > 0 && !active)
            {
                float frac = g.AbilityCd[i] / (a.Cooldown * g.CooldownMult());
                Raylib.DrawCircleSector(new Vector2(r.X + size / 2, r.Y + size / 2), size * 0.62f, -90, -90 + 360 * frac, 24, new Color(0, 0, 0, 120));
                TextCentered($"{Math.Ceiling(g.AbilityCd[i])}", new Vector2(r.X + size / 2, r.Y + size / 2), 22, Color.White, true);
            }
            if (active)
            {
                Bar(new Rectangle(r.X + 4, r.Y + size - 10, size - 8, 6), g.AbilityActive[i] / a.Duration, Color.White, new Color(0, 0, 0, 120));
            }
            Text($"{i + 1}", r.X + 5, r.Y + 3, 14, Color.White, true);
            TextCentered(unlocked ? a.Name : $"Stage {a.UnlockStage}", new Vector2(r.X + size / 2, r.Y + size + 12), 12, unlocked ? Txt : Dim, true);
            if (Hover(r))
            {
                string tip = $"{a.Name}: {a.Desc}  ({a.Duration:0}s, cooldown {a.Cooldown * g.CooldownMult():0}s)";
                float tw = Measure(tip, 15) + 20;
                var tr = new Rectangle(Math.Clamp(r.X + size / 2 - tw / 2, b.X + 4, b.X + b.Width - tw - 4), r.Y - 40, tw, 28);
                Raylib.DrawRectangleRounded(tr, 0.4f, 6, new Color(10, 5, 20, 235));
                TextCentered(tip, new Vector2(tr.X + tw / 2, tr.Y + 14), 15, Txt, false);
            }
        }
    }

    static void AbilityGlyph(int i, Vector2 c, Color col)
    {
        switch (i)
        {
            case 0: // claw marks
                for (int k = -1; k <= 1; k++) Raylib.DrawLineEx(c + new Vector2(k * 9 - 8, -16), c + new Vector2(k * 9 + 6, 16), 5, col);
                break;
            case 1: // meteor
                Raylib.DrawLineEx(c + new Vector2(-16, -16), c + new Vector2(2, 2), 6, new Color(255, 200, 100, (int)col.A));
                Raylib.DrawCircleV(c + new Vector2(6, 6), 10, col);
                break;
            case 2: // coin pile
                CurrencyIcon(0, c + new Vector2(-8, 6), 10, 1); CurrencyIcon(0, c + new Vector2(8, 6), 10, 1); CurrencyIcon(0, c + new Vector2(0, -6), 10, 1);
                break;
            case 3: // war banner
                Raylib.DrawLineEx(c + new Vector2(-10, 18), c + new Vector2(-10, -18), 4, col);
                Raylib.DrawTriangle(c + new Vector2(-8, -18), c + new Vector2(-8, 2), c + new Vector2(16, -8), col);
                break;
            default: // lightning hands
                Raylib.DrawLineEx(c + new Vector2(4, -18), c + new Vector2(-6, 0), 5, col);
                Raylib.DrawLineEx(c + new Vector2(-6, 0), c + new Vector2(6, 0), 5, col);
                Raylib.DrawLineEx(c + new Vector2(6, 0), c + new Vector2(-4, 18), 5, col);
                break;
        }
    }

    // ---------- shop panel ----------
    static readonly string[] TabNames = { "HEROES", "DRAGONS", "ARTIFACTS", "ASCEND" };

    public static void DrawPanel(Game g, float t)
    {
        var p = Panel;
        MouseOverUi = Hover(p) || Raylib.GetMousePosition().Y < TopH;
        Raylib.DrawRectangleRec(p, Bg);
        Raylib.DrawRectangle((int)p.X, (int)p.Y, 3, (int)p.Height, new Color(255, 190, 70, 120));

        // Tabs
        float tw = (p.Width - 20) / 4f;
        for (int i = 0; i < 4; i++)
        {
            var r = new Rectangle(p.X + 10 + i * tw, p.Y + 10, tw - 6, 40);
            bool sel = tab == i;
            if (Button(r, sel ? new Color(120, 80, 170, 255) : new Color(55, 46, 80, 255), true)) tab = i;
            TextCentered(TabNames[i], new Vector2(r.X + r.Width / 2, r.Y + 20), 17, sel ? Color.White : Txt, true);
            if (TabHasAffordable(g, i))
            {
                float bounce = MathF.Abs(MathF.Sin(t * 5)) * 3;
                Raylib.DrawCircleV(new Vector2(r.X + r.Width - 8, r.Y + 6 - bounce), 8, new Color(255, 80, 60, 255));
                TextCentered("!", new Vector2(r.X + r.Width - 8, r.Y + 6 - bounce), 13, Color.White, false);
            }
        }

        float top = p.Y + 60;
        if (tab == 0)
        {
            string[] modes = { "x1", "x10", "x25", "x100", "NEXT", "MAX" };
            float mw = (p.Width - 20) / modes.Length;
            for (int i = 0; i < modes.Length; i++)
            {
                var r = new Rectangle(p.X + 10 + i * mw, top, mw - 6, 28);
                bool sel = (int)g.Mode == i;
                if (Button(r, sel ? new Color(200, 150, 50, 255) : new Color(50, 42, 72, 255), true)) g.Mode = (BuyMode)i;
                TextCentered(modes[i], new Vector2(r.X + r.Width / 2, r.Y + 14), 15, Color.White, false);
            }
            top += 36;
        }

        var view = new Rectangle(p.X, top, p.Width, p.Y + p.Height - top);
        if (Hover(view) && !Modal) scroll[tab] -= Raylib.GetMouseWheelMove() * 60;
        scroll[tab] = Math.Clamp(scroll[tab], 0, Math.Max(0, contentH[tab] - view.Height + 20));
        Raylib.BeginScissorMode((int)view.X, (int)view.Y, (int)view.Width, (int)view.Height);
        float y = view.Y + 6 - scroll[tab];
        float start = y;
        switch (tab)
        {
            case 0: y = HeroesTab(g, p, y, t, view); break;
            case 1: y = DragonsTab(g, p, y, t, view); break;
            case 2: y = ArtifactsTab(g, p, y, t, view); break;
            case 3: y = AscendTab(g, p, y, t, view); break;
        }
        contentH[tab] = y - start;
        Raylib.EndScissorMode();
        if (contentH[tab] > view.Height)
        {
            float frac = view.Height / contentH[tab];
            float pos = scroll[tab] / Math.Max(1, contentH[tab] - view.Height + 20);
            Raylib.DrawRectangleRounded(new Rectangle(p.X + p.Width - 7, view.Y + pos * view.Height * (1 - frac), 4, view.Height * frac), 1, 4, new Color(255, 255, 255, 50));
        }
    }

    static bool TabHasAffordable(Game g, int tab)
    {
        switch (tab)
        {
            case 0:
                if (g.Gold >= g.TamerBaseCost(g.TamerLevel)) return true;
                for (int i = 0; i < Defs.Heroes.Length; i++)
                    if (g.HeroVisibleInShop(i) && g.Gold >= g.HeroBaseCost(i, g.HeroLevels[i])) return true;
                return false;
            case 1: return AnyAffordable(Defs.Dragons, g.DragonLevels, g.Scales);
            case 2: return AnyAffordable(Defs.Artifacts, g.ArtifactLevels, g.Gems);
            default: return g.SoulsOnAscend() >= BigNum.Max(10, g.LifetimeSouls * 0.5) || AnyAffordable(Defs.SoulUpgrades, g.SoulLevels, g.Souls);
        }
    }

    static bool AnyAffordable(UpgradeDef[] defs, int[] lv, BigNum wallet)
    {
        for (int i = 0; i < defs.Length; i++)
            if ((defs[i].MaxLevel == 0 || lv[i] < defs[i].MaxLevel) && wallet >= Game.UpgradeCost(defs[i], lv[i])) return true;
        return false;
    }

    static bool Visible(float y, float h, Rectangle view) => y + h > view.Y && y < view.Y + view.Height;

    static float HeroesTab(Game g, Rectangle p, float y, float t, Rectangle view)
    {
        // Tamer (click damage)
        {
            var (n, cost) = g.TamerPurchase();
            var gain = g.TamerClickAt(g.TamerLevel + n) - g.TamerClickAt(g.TamerLevel);
            HeroCard(g, p, ref y, t, view, "Dragon Tamer", "You  -  click damage", new Color(120, 50, 150, 255), new Color(255, 210, 120, 255),
                g.TamerLevel, g.ClickDamage, "CLICK", n, cost, gain, g.TamerLevelFx, false, () => g.BuyTamer());
        }
        for (int i = 0; i < Defs.Heroes.Length; i++)
        {
            if (!g.HeroVisibleInShop(i))
            {
                var r = new Rectangle(p.X + 10, y, p.Width - 22, 50);
                if (Visible(y, 50, view))
                {
                    Raylib.DrawRectangleRounded(r, 0.25f, 6, new Color(30, 26, 44, 255));
                    Text("??? - hire the previous hero to reveal", r.X + 16, r.Y + 16, 17, Dim);
                }
                y += 58;
                break;
            }
            var d = Defs.Heroes[i];
            var (n, cost) = g.HeroPurchase(i);
            var gain = g.HeroDpsAt(i, g.HeroLevels[i] + n) - g.HeroDps[i];
            HeroCard(g, p, ref y, t, view, d.Name, d.Title, d.Primary, d.Secondary, g.HeroLevels[i], g.HeroDps[i], "DPS", n, cost, gain,
                g.HeroLevelFx[i], g.HeroLevels[i] == 0, () => g.BuyHero(i));
        }
        return y;
    }

    static void HeroCard(Game g, Rectangle p, ref float y, float t, Rectangle view, string name, string title, Color c1, Color c2,
        int level, BigNum power, string unit, int n, BigNum cost, BigNum gain, float fx, bool hire, Action buy)
    {
        const float h = 96;
        if (!Visible(y, h, view)) { y += h + 8; return; }
        var r = new Rectangle(p.X + 10, y, p.Width - 22, h);
        bool afford = g.Gold >= cost;
        Raylib.DrawRectangleRounded(r, 0.2f, 6, fx > 0 ? Render.Lerp(Card, new Color(120, 100, 40, 255), fx) : Card);
        Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y, 6, r.Height), 1, 4, c1);
        Portrait(new Vector2(r.X + 44, r.Y + 42), 28, c1, c2, name[..1]);
        if (!hire)
        {
            var lr = new Rectangle(r.X + 18, r.Y + 70, 52, 20);
            Raylib.DrawRectangleRounded(lr, 0.5f, 6, new Color(15, 10, 25, 230));
            TextCentered($"Lv {level}", new Vector2(lr.X + 26, lr.Y + 10), 14, GoldC, false);
        }
        float tx = r.X + 84;
        Text(name, tx, r.Y + 8, 21, Txt, true);
        Text(title, tx, r.Y + 31, 14, Dim);
        if (hire) Text("Not yet hired", tx, r.Y + 52, 16, Dim);
        else Text($"{power} {unit}", tx, r.Y + 50, 19, unit == "DPS" ? new Color(255, 160, 100, 255) : new Color(170, 210, 255, 255), true);

        // Milestone progress
        int next = Defs.NextMilestone(level), prev = Defs.PrevMilestone(level);
        float frac = (level - prev) / (float)(next - prev);
        float bw = r.Width - 84 - 250;
        var mb = new Rectangle(tx, r.Y + 76, bw, 10);
        Bar(mb, frac, new Color(255, 200, 70, 255), new Color(20, 15, 30, 255));
        Text($"Lv {next}: x{Defs.MilestoneMult(next):0}", tx + bw + 6, r.Y + 73, 13, GoldC);

        // Buy button
        var br = new Rectangle(r.X + r.Width - 172, r.Y + 10, 162, 76);
        if (afford)
        {
            float glow = 0.5f + 0.5f * MathF.Sin(t * 5);
            Raylib.DrawRectangleRounded(new Rectangle(br.X - 3, br.Y - 3, br.Width + 6, br.Height + 6), 0.3f, 6, new Color(255, 210, 80, (int)(40 + 70 * glow)));
        }
        if (Button(br, hire ? new Color(60, 150, 90, 255) : new Color(170, 110, 40, 255), afford, true)) buy();
        string head = hire ? "HIRE" : $"LEVEL UP x{n}";
        TextCentered(head, new Vector2(br.X + br.Width / 2, br.Y + 15), 16, Color.White, true);
        string cs = cost.Format();
        float cw = Measure(cs, 20) + 22;
        CurrencyIcon(0, new Vector2(br.X + br.Width / 2 - cw / 2 + 8, br.Y + 39), 8, 1);
        Text(cs, br.X + br.Width / 2 - cw / 2 + 20, br.Y + 29, 20, afford ? Color.White : new Color(255, 150, 150, 255), true);
        TextCentered($"+{gain} {unit}", new Vector2(br.X + br.Width / 2, br.Y + 62), 14, afford ? Green : Dim, false);
        y += h + 8;
    }

    static string DragonEffect(Game g, int i, int lv)
    {
        double d = lv * (1 + 0.2 * g.SoulLevels[6]);
        return i switch
        {
            0 => $"Damage x{((1 + 0.25 * d) * Math.Pow(2, lv / 10)):0.##}",
            1 => $"Click x{1 + 0.5 * d:0.#}, +{d:0.#}% DPS/click",
            2 => $"Gold x{1 + 0.3 * d:0.##}",
            3 => $"Crit damage x{5 + 0.5 * d:0.#}",
            4 => $"Souls x{1 + 0.15 * d:0.##}",
            _ => $"Damage x{BigNum.Pow(1.12, d)}",
        };
    }

    static string ArtifactEffect(Game g, int i, int lv) => i switch
    {
        0 => $"Hero DPS x{1 + 0.5 * lv:0.#}",
        1 => $"Click x{1 + lv}",
        2 => $"Gold x{1 + 0.4 * lv:0.#}",
        3 => $"Boss timer {30 + 2 * lv}s",
        4 => $"Gem chance {(1 + 0.5 * lv):0.#}%",
        5 => $"Crit chance +{lv}%",
        6 => $"Hero costs x{Math.Pow(0.96, lv):0.00}",
        _ => $"Scales x{1 + 0.25 * lv:0.##}",
    };

    static string SoulEffect(Game g, int i, int lv) => i switch
    {
        0 => $"Damage x{BigNum.Pow(1.5, lv)}",
        1 => $"Gold x{BigNum.Pow(1.4, lv)}",
        2 => $"{2 * lv} auto-clicks/s",
        3 => $"Cooldowns -{6 * lv}%",
        4 => $"Crit chance +{2 * lv}%",
        5 => $"Souls x{1 + 0.15 * lv:0.##}",
        _ => $"Dragon power x{1 + 0.2 * lv:0.#}",
    };

    static float UpgradeList(Game g, Rectangle p, float y, float t, Rectangle view, UpgradeDef[] defs, int[] levels, BigNum wallet,
        int currency, Func<int, int, string> effect, Action<int> buy, string verbFirst, string verb, bool isDragon = false)
    {
        for (int i = 0; i < defs.Length; i++)
        {
            const float h = 92;
            var d = defs[i];
            if (!Visible(y, h, view)) { y += h + 8; continue; }
            int lv = levels[i];
            bool maxed = d.MaxLevel > 0 && lv >= d.MaxLevel;
            var cost = Game.UpgradeCost(d, lv);
            bool afford = !maxed && wallet >= cost;
            var r = new Rectangle(p.X + 10, y, p.Width - 22, h);
            Raylib.DrawRectangleRounded(r, 0.2f, 6, lv > 0 ? CardHi : Card);
            Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y, 6, r.Height), 1, 4, d.Color);
            var pc = new Vector2(r.X + 44, r.Y + h / 2);
            if (isDragon)
            {
                Raylib.DrawCircleV(pc, 31, Render.Mul(d.Color, 0.5f));
                Raylib.DrawCircleV(pc, 28, lv > 0 ? d.Color : new Color(60, 55, 75, 255));
                // little egg / dragon glyph
                if (lv == 0) Raylib.DrawEllipse((int)pc.X, (int)pc.Y + 2, 13, 17, new Color(240, 230, 210, 255));
                else
                {
                    Raylib.DrawTriangle(pc + new Vector2(-18, -4), pc + new Vector2(0, 6), pc + new Vector2(-4, -18), Render.Mul(d.Color, 0.6f));
                    Raylib.DrawTriangle(pc + new Vector2(18, -4), pc + new Vector2(4, -18), pc + new Vector2(0, 6), Render.Mul(d.Color, 0.6f));
                    Raylib.DrawCircleV(pc + new Vector2(0, 4), 10, Render.Mul(d.Color, 1.2f));
                    Raylib.DrawCircleV(pc + new Vector2(-4, 2), 2.5f, Color.Black);
                    Raylib.DrawCircleV(pc + new Vector2(4, 2), 2.5f, Color.Black);
                }
            }
            else
            {
                Raylib.DrawPoly(pc, currency == 3 ? 5 : 6, 30, t * 20 + i * 10, Render.Mul(d.Color, 0.5f));
                Raylib.DrawPoly(pc, currency == 3 ? 5 : 6, 25, t * 20 + i * 10, d.Color);
                TextCentered(d.Name[..1], pc, 26, Color.White, true);
            }
            float tx = r.X + 84;
            Text(d.Name, tx, r.Y + 8, 20, Txt, true);
            if (lv > 0) Text($"Lv {lv}{(d.MaxLevel > 0 ? $"/{d.MaxLevel}" : "")}", tx + Measure(d.Name, 20) + 10, r.Y + 12, 15, GoldC);
            Text(d.Desc, tx, r.Y + 32, 14, Dim);
            string cur = lv > 0 ? effect(i, lv) : "Inactive";
            string nxt = maxed ? "MAX" : effect(i, lv + 1);
            Text(cur, tx, r.Y + 54, 16, lv > 0 ? Green : Dim);
            if (!maxed) Text($"next: {nxt}", tx, r.Y + 72, 13, new Color(180, 170, 200, 255));

            var br = new Rectangle(r.X + r.Width - 140, r.Y + 14, 130, 64);
            if (afford)
            {
                float glow = 0.5f + 0.5f * MathF.Sin(t * 5);
                Raylib.DrawRectangleRounded(new Rectangle(br.X - 3, br.Y - 3, br.Width + 6, br.Height + 6), 0.3f, 6,
                    new Color(CurrencyColors[currency].R, CurrencyColors[currency].G, CurrencyColors[currency].B, (byte)(40 + 70 * glow)));
            }
            if (Button(br, Render.Mul(d.Color, 0.55f), afford, true) && afford) buy(i);
            TextCentered(maxed ? "MAXED" : lv == 0 ? verbFirst : verb, new Vector2(br.X + br.Width / 2, br.Y + 16), 16, Color.White, true);
            if (!maxed)
            {
                string cs = cost.Format();
                float cw = Measure(cs, 19) + 22;
                CurrencyIcon(currency, new Vector2(br.X + br.Width / 2 - cw / 2 + 8, br.Y + 43), 8, 1);
                Text(cs, br.X + br.Width / 2 - cw / 2 + 20, br.Y + 33, 19, afford ? Color.White : new Color(255, 150, 150, 255), true);
            }
            y += h + 8;
        }
        return y;
    }

    static float Blurb(Rectangle p, float y, string s, Color c)
    {
        Text(s, p.X + 16, y, 15, c);
        return y + 24;
    }

    static float DragonsTab(Game g, Rectangle p, float y, float t, Rectangle view)
    {
        y = Blurb(p, y, "Slay a dragon boss every 10 stages to earn Dragon Scales.", Dim);
        y = Blurb(p, y, "Pet dragons fly into battle and are never lost on Ascension.", Dim);
        return UpgradeList(g, p, y, t, view, Defs.Dragons, g.DragonLevels, g.Scales, 2, (i, lv) => DragonEffect(g, i, lv),
            i => g.BuyDragon(i), "HATCH", "FEED", true);
    }

    static float ArtifactsTab(Game g, Rectangle p, float y, float t, Rectangle view)
    {
        y = Blurb(p, y, "Gems drop from bosses (and rarely from monsters).", Dim);
        y = Blurb(p, y, "Artifacts are permanent and survive Ascension.", Dim);
        return UpgradeList(g, p, y, t, view, Defs.Artifacts, g.ArtifactLevels, g.Gems, 1, (i, lv) => ArtifactEffect(g, i, lv),
            i => g.BuyArtifact(i), "FORGE", "EMPOWER");
    }

    static float AscendTab(Game g, Rectangle p, float y, float t, Rectangle view)
    {
        var gain = g.SoulsOnAscend();
        var r = new Rectangle(p.X + 10, y, p.Width - 22, 190);
        Raylib.DrawRectangleRounded(r, 0.12f, 8, new Color(50, 30, 75, 255));
        Raylib.DrawRectangleRoundedLinesEx(r, 0.12f, 8, 2, new Color(200, 120, 255, 180));
        TextCentered("ASCENSION", new Vector2(r.X + r.Width / 2, r.Y + 24), 30, new Color(220, 160, 255, 255), true);
        TextCentered("Resets stages, gold and heroes. Everything else is kept.", new Vector2(r.X + r.Width / 2, r.Y + 52), 14, Dim, false);
        TextCentered($"Highest stage this run: {g.MaxStage}", new Vector2(r.X + r.Width / 2, r.Y + 76), 17, Txt, false);
        var bonus = g.LifetimeSouls * 10;
        TextCentered($"Lifetime souls: {g.LifetimeSouls}   (+{bonus}% damage)", new Vector2(r.X + r.Width / 2, r.Y + 98), 16, new Color(200, 160, 255, 255), false);
        var br = new Rectangle(r.X + 40, r.Y + 118, r.Width - 80, 58);
        bool can = gain.IsPositive;
        if (can)
        {
            float glow = 0.5f + 0.5f * MathF.Sin(t * 4);
            Raylib.DrawRectangleRounded(new Rectangle(br.X - 4, br.Y - 4, br.Width + 8, br.Height + 8), 0.3f, 6, new Color(200, 120, 255, (int)(60 + 80 * glow)));
        }
        if (Button(br, ascendConfirm > 0 ? new Color(200, 60, 90, 255) : new Color(130, 60, 190, 255), can) && can)
        {
            if (ascendConfirm > 0) { g.Ascend(); ascendConfirm = 0; }
            else ascendConfirm = 3;
        }
        string label = !can ? "Reach stage 30 to ascend" : ascendConfirm > 0 ? "CLICK AGAIN TO CONFIRM" : $"ASCEND  for  +{gain} souls";
        TextCentered(label, new Vector2(br.X + br.Width / 2, br.Y + br.Height / 2), 21, Color.White, true);
        y += r.Height + 14;
        y = Blurb(p, y, "SOUL SHOP  -  each soul earned also grants +10% damage forever", new Color(220, 180, 255, 255));
        y = UpgradeList(g, p, y, t, view, Defs.SoulUpgrades, g.SoulLevels, g.Souls, 3, (i, lv) => SoulEffect(g, i, lv),
            i => g.BuySoulUpgrade(i), "AWAKEN", "EMPOWER");
        var span = TimeSpan.FromSeconds(g.PlayTime);
        y += 6;
        y = Blurb(p, y, $"Play time {(int)span.TotalHours}h {span.Minutes}m   Clicks {g.Clicks}   Ascensions {g.Ascensions}", Dim);
        y = Blurb(p, y, $"Total gold earned {g.TotalGold}", Dim);
        y = Blurb(p, y, "Keys: 1-5 abilities, Space attack, M mute, Tab switch tab, F5 save, Esc menu", Dim);
        return y;
    }

    public static void NextTab() => tab = (tab + 1) % 4;
    public static void SetTab(int t) { tab = t; scroll[t] = 0; }

    // ---------- pause menu ----------
    public enum PauseAction { None, Resume, Quit, LoadSlot, NewGame }

    static string confirmKey = "";
    static float confirmTimer, savedToast;

    public static bool PauseButton()
    {
        var b = Battle;
        var r = new Rectangle(b.X + 12, b.Y + 12, 44, 44);
        bool clicked = Button(r, new Color(60, 50, 90, 255), true);
        Raylib.DrawRectangle((int)r.X + 15, (int)r.Y + 13, 5, 18, Color.White);
        Raylib.DrawRectangle((int)r.X + 24, (int)r.Y + 13, 5, 18, Color.White);
        Text("Esc", r.X + 11, r.Y + r.Height + 2, 12, Dim, true);
        return clicked;
    }

    static bool Confirm(string key)
    {
        if (confirmKey == key && confirmTimer > 0) { confirmKey = ""; return true; }
        confirmKey = key;
        confirmTimer = 3;
        return false;
    }

    static bool Confirming(string key) => confirmKey == key && confirmTimer > 0;

    static string Ago(long unix)
    {
        var span = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix);
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    static bool MenuButton(Rectangle r, string label, Color col, bool enabled = true)
    {
        bool clicked = Button(r, col, enabled, false, true);
        TextCentered(label, new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2), Math.Min(20, r.Height * 0.45f), enabled ? Color.White : Dim, true);
        return clicked && enabled;
    }

    public static void SavedIndicator(Game g, float t)
    {
        if (g.AutosaveFlash <= 0) return;
        var b = Battle;
        byte a = (byte)(255 * Math.Min(1, g.AutosaveFlash));
        var c = new Vector2(b.X + b.Width - 100, b.Y + 20);
        Raylib.DrawRing(c, 6, 9, t * 360, t * 360 + 270, 16, new Color((byte)110, (byte)235, (byte)120, a));
        Text("Autosaved", c.X + 16, c.Y - 9, 16, new Color((byte)110, (byte)235, (byte)120, a), true);
    }

    public static void QuickSaved() => savedToast = 2;

    public static (PauseAction action, int slot) DrawPauseMenu(Game g, Sfx sfx, Settings settings, float dt)
    {
        confirmTimer = Math.Max(0, confirmTimer - dt);
        savedToast = Math.Max(0, savedToast - dt);
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, sw, sh, new Color(8, 4, 16, 190));

        float w = Math.Min(600, sw - 40), h = 560;
        var m = new Rectangle(sw / 2f - w / 2, Math.Max(10, sh / 2f - h / 2), w, h);
        Raylib.DrawRectangleRounded(m, 0.06f, 8, Bg);
        Raylib.DrawRectangleRoundedLinesEx(m, 0.06f, 8, 3, new Color(255, 190, 70, 200));
        float cx = m.X + w / 2;
        TextCentered("PAUSED", new Vector2(cx, m.Y + 38), 44, GoldC, true);
        string when = g.LastSaved == default ? "Not saved yet this session" : $"Last saved {(int)(DateTime.Now - g.LastSaved).TotalSeconds}s ago";
        string saved = settings.Autosave ? $"{when}  (autosave every {Settings.Describe(settings.AutosaveSeconds)})" : $"{when}  -  autosave is OFF, save with F5";
        TextCentered(savedToast > 0 ? "Game saved!" : saved, new Vector2(cx, m.Y + 72), 15,
            savedToast > 0 ? Green : settings.Autosave ? Dim : new Color(255, 170, 90, 255), false);

        var result = (PauseAction.None, 0);
        float bw = (w - 60) / 2, y = m.Y + 96;
        if (MenuButton(new Rectangle(m.X + 20, y, bw, 50), "RESUME", new Color(60, 150, 90, 255))) result = (PauseAction.Resume, 0);
        if (MenuButton(new Rectangle(m.X + 40 + bw, y, bw, 50), "SAVE GAME", new Color(170, 110, 40, 255), g.SavingEnabled))
        { g.Save(); savedToast = 2; sfx.Play(sfx.LevelUp); }
        y += 62;
        float tw = (w - 60) / 3;
        if (MenuButton(new Rectangle(m.X + 20, y, tw, 44), sfx.Muted ? "SOUND: OFF" : "SOUND: ON", new Color(80, 70, 120, 255)))
        { sfx.Muted = !sfx.Muted; settings.Muted = sfx.Muted; settings.Save(); }
        if (MenuButton(new Rectangle(m.X + 30 + tw, y, tw, 44), $"AUTOSAVE: {Settings.Describe(settings.AutosaveSeconds)}",
                settings.Autosave ? new Color(60, 120, 90, 255) : new Color(110, 70, 50, 255)))
        {
            settings.CycleAutosave();
            g.AutosaveSeconds = settings.AutosaveSeconds;
        }
        if (MenuButton(new Rectangle(m.X + 40 + 2 * tw, y, tw, 44), "SAVE & QUIT", new Color(150, 50, 60, 255))) result = (PauseAction.Quit, 0);
        y += 64;

        Text("SAVE SLOTS", m.X + 22, y, 20, Txt, true);
        y += 30;
        for (int slot = 1; slot <= Game.SlotCount; slot++)
        {
            bool current = slot == g.Slot;
            var info = current ? null : Game.PeekSlot(slot);
            var r = new Rectangle(m.X + 20, y, w - 40, 76);
            Raylib.DrawRectangleRounded(r, 0.2f, 6, current ? CardHi : Card);
            if (current) Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 6, 2, GoldC);
            Text($"Slot {slot}" + (current ? "  -  PLAYING" : ""), r.X + 14, r.Y + 10, 19, current ? GoldC : Txt, true);
            string line1, line2;
            if (current)
            {
                var span = TimeSpan.FromSeconds(g.PlayTime);
                line1 = $"Stage {g.Stage}  -  best {g.BestStage}  -  {Defs.Biomes[Defs.BiomeIndex(g.Stage)].Name}";
                line2 = $"{(int)span.TotalHours}h {span.Minutes}m played  -  {g.Ascensions} ascensions  -  {g.Gold} gold";
            }
            else if (info != null)
            {
                var span = TimeSpan.FromSeconds(info.PlayTime);
                line1 = $"Stage {info.Stage}  -  best {info.BestStage}  -  {Defs.Biomes[Defs.BiomeIndex(Math.Max(1, info.Stage))].Name}";
                line2 = $"{(int)span.TotalHours}h {span.Minutes}m played  -  {info.Ascensions} ascensions  -  saved {Ago(info.SavedAt)}";
            }
            else { line1 = "Empty"; line2 = "Start a fresh adventure here"; }
            Text(line1, r.X + 14, r.Y + 34, 15, info != null || current ? Txt : Dim);
            Text(line2, r.X + 14, r.Y + 53, 13, Dim);

            var b1 = new Rectangle(r.X + r.Width - 118, r.Y + 14, 104, 48);
            var b2 = new Rectangle(r.X + r.Width - 230, r.Y + 14, 104, 48);
            string key = $"slot{slot}";
            if (current)
            {
                if (MenuButton(b1, Confirming(key + "restart") ? "SURE?" : "RESTART", new Color(150, 50, 60, 255)) && Confirm(key + "restart"))
                    result = (PauseAction.NewGame, slot);
            }
            else if (info != null)
            {
                if (MenuButton(b1, "LOAD", new Color(60, 120, 170, 255))) result = (PauseAction.LoadSlot, slot);
                if (MenuButton(b2, Confirming(key + "del") ? "SURE?" : "DELETE", new Color(110, 45, 55, 255)) && Confirm(key + "del"))
                    Game.DeleteSlot(slot);
            }
            else if (MenuButton(b1, "NEW GAME", new Color(60, 150, 90, 255))) result = (PauseAction.NewGame, slot);
            y += 86;
        }
        TextCentered(settings.Autosave ? "Your current game is saved automatically before switching slots."
                : "Autosave is off: unsaved progress is lost when switching slots or closing the window.",
            new Vector2(cx, m.Y + h - 22), 13, settings.Autosave ? Dim : new Color(255, 170, 90, 255), false);
        return result;
    }
}
