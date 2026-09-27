using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace DragonIdle;

public enum BuyMode { One, Ten, TwentyFive, Hundred, Next, Max }

public class Enemy
{
    public EnemyKind Kind;
    public bool IsBoss, IsDragon;
    public string Name = "";
    public BigNum MaxHp, Hp;
    public Color Tint;
    public float Scale = 1;
    public float HitFlash, Dying, Spawn, Wobble;
    public int Seed;
    public bool Alive => Dying <= 0 && Hp.IsPositive;
}

public class Projectile
{
    public Vector3 From, To;
    public float T, Duration;
    public ProjKind Kind;
    public Color Color;
    public BigNum Damage;
    public int Source;
    public float Arc;
}

public class SaveData
{
    public string Gold { get; set; } = "";
    public string Gems { get; set; } = "";
    public string Scales { get; set; } = "";
    public string Souls { get; set; } = "";
    public string LifetimeSouls { get; set; } = "";
    public string TotalGold { get; set; } = "";
    public int TamerLevel { get; set; } = 1;
    public int[] Heroes { get; set; } = Array.Empty<int>();
    public int[] Artifacts { get; set; } = Array.Empty<int>();
    public int[] Dragons { get; set; } = Array.Empty<int>();
    public int[] SoulUps { get; set; } = Array.Empty<int>();
    public float[] Cooldowns { get; set; } = Array.Empty<float>();
    public long Stage { get; set; } = 1;
    public long MaxStage { get; set; } = 1;
    public long BestStage { get; set; } = 1;
    public int Kills { get; set; }
    public bool AutoAdvance { get; set; } = true;
    public int Ascensions { get; set; }
    public long Clicks { get; set; }
    public double PlayTime { get; set; }
    public long SavedAt { get; set; }
}

public class Game
{
    public const int KillsPerStage = 10;
    public const double HeroGrowth = 1.075, TamerGrowth = 1.07;
    public const int SlotCount = 3;
    public int Slot = 1;
    public bool SavingEnabled = true;
    public DateTime LastSaved;
    /// <summary>Seconds between autosaves; 0 disables autosave. Set from Settings.</summary>
    public int AutosaveSeconds = 30;
    /// <summary>Counts down after an autosave so the UI can show a "Saved" indicator.</summary>
    public float AutosaveFlash;

    public static string SaveDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonIdle");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string SlotPath(int slot) => Path.Combine(SaveDir, $"slot{slot}.json");
    static string LastSlotPath => Path.Combine(SaveDir, "last_slot.txt");
    string SavePath => SlotPath(Slot);

    public static int LastUsedSlot()
    {
        try { if (int.TryParse(File.ReadAllText(LastSlotPath), out int s) && s >= 1 && s <= SlotCount) return s; } catch { }
        return 1;
    }

    /// <summary>Reads a slot's summary without loading it (null when empty or unreadable).</summary>
    public static SaveData? PeekSlot(int slot)
    {
        try { return File.Exists(SlotPath(slot)) ? JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SlotPath(slot))) : null; }
        catch { return null; }
    }

    public static void DeleteSlot(int slot)
    {
        try { File.Delete(SlotPath(slot)); } catch { }
    }

    /// <summary>Moves a save from the old working-directory location into slot 1.</summary>
    public static void MigrateLegacySave()
    {
        const string legacy = "dragonidle_save.json";
        try { if (File.Exists(legacy) && !File.Exists(SlotPath(1))) File.Move(legacy, SlotPath(1)); } catch { }
    }

    // Currencies
    public BigNum Gold, Gems, Scales, Souls, LifetimeSouls, TotalGold;

    public int TamerLevel = 1;
    public int[] HeroLevels = new int[Defs.Heroes.Length];
    public int[] ArtifactLevels = new int[Defs.Artifacts.Length];
    public int[] DragonLevels = new int[Defs.Dragons.Length];
    public int[] SoulLevels = new int[Defs.SoulUpgrades.Length];

    public long Stage = 1, MaxStage = 1, BestStage = 1;
    public int Kills;
    public bool AutoAdvance = true;
    public int Ascensions;
    public long Clicks;
    public double PlayTime;

    public float[] AbilityCd = new float[Defs.Abilities.Length];
    public float[] AbilityActive = new float[Defs.Abilities.Length];

    public Enemy Enemy = new();
    public float RespawnTimer;
    public float BossTimer, BossTimerMax;
    public readonly List<Projectile> Projectiles = new();
    public float[] HeroAttackTimer = new float[Defs.Heroes.Length];
    public float[] HeroAnim = new float[Defs.Heroes.Length];
    public float[] HeroLevelFx = new float[Defs.Heroes.Length];
    public float TamerAnim, TamerLevelFx;
    public float[] DragonBreath = new float[Defs.Dragons.Length];
    public BuyMode Mode = BuyMode.One;

    // Cached per-frame stats
    public BigNum[] HeroDps = new BigNum[Defs.Heroes.Length];
    public BigNum TotalDps, ClickDamage;
    float autoClickAcc, meteorAcc;
    BigNum meteorPool;
    readonly Queue<(double t, BigNum g)> goldLog = new();
    public BigNum GoldPerSec;
    float saveTimer;

    public readonly Random Rng = new();
    public Fx Fx = null!;
    public Sfx Sfx = null!;

    public static readonly Vector3 EnemyPos = new(3.6f, 0, 0.2f);
    public static readonly Vector3 TamerPos = new(-0.6f, 0, 2.6f);

    public static Vector3 HeroPos(int i)
    {
        int col = i / 3, row = i % 3;
        float[] zs = { 1.3f, -1.1f, 3.5f };
        return new Vector3(-2.2f - col * 1.85f - row * 0.35f, 0, zs[row] - col * 0.15f);
    }

    // ---------- Multipliers ----------
    float DragonBond => 1 + 0.2f * SoulLevels[6];
    float DLv(int i) => DragonLevels[i] * DragonBond;

    public BigNum GlobalMult()
    {
        BigNum m = 1 + LifetimeSouls * 0.1;
        m *= BigNum.Pow(1.5, SoulLevels[0]);
        m *= (1 + 0.25 * DLv(0)) * BigNum.Pow(2, DragonLevels[0] / 10);
        m *= BigNum.Pow(1.12, DLv(5));
        if (AbilityActive[3] > 0) m *= 3;
        return m;
    }

    public double HeroMult() => 1 + 0.5 * ArtifactLevels[0];
    public double ClickMult() => (1 + ArtifactLevels[1]) * (1 + 0.5 * DLv(1)) * (AbilityActive[0] > 0 ? 10 : 1);
    public double ClickDpsPct() => 0.005 * Defs.MilestoneCount(TamerLevel) + 0.01 * DLv(1);

    public BigNum GoldMult()
    {
        BigNum m = (1 + 0.4 * ArtifactLevels[2]) * (1 + 0.3 * DLv(2));
        m *= BigNum.Pow(1.4, SoulLevels[1]);
        if (AbilityActive[2] > 0) m *= 3;
        return m;
    }

    public double CritChance() => Math.Min(0.75, 0.05 + 0.01 * ArtifactLevels[5] + 0.02 * SoulLevels[4]);
    public double CritMult() => 5 + 0.5 * DLv(3);
    public float BossTime() => 30 + 2 * ArtifactLevels[3];
    public float CooldownMult() => 1 - 0.06f * SoulLevels[3];
    public double GemChance() => 0.01 + 0.005 * ArtifactLevels[4];
    public double ScaleMult() => 1 + 0.25 * ArtifactLevels[7];
    public double SoulMult() => (1 + 0.15 * DLv(4)) * (1 + 0.15 * SoulLevels[5]);
    public double CostMult() => Math.Pow(0.96, ArtifactLevels[6]);

    // ---------- Heroes ----------
    public BigNum HeroBaseCost(int i, int level) =>
        BigNum.Pow(HeroGrowth, level) * (Defs.Heroes[i].BaseCost * CostMult());

    public BigNum TamerBaseCost(int level) => BigNum.Pow(TamerGrowth, level - 1) * 5.0;

    static BigNum SeriesCost(BigNum first, double r, int n) =>
        n <= 0 ? BigNum.Zero : first * ((BigNum.Pow(r, n) - 1) / (r - 1));

    static int MaxAffordable(BigNum first, double r, BigNum gold)
    {
        if (gold < first) return 0;
        var x = gold * (r - 1) / first + 1;
        return Math.Max(1, (int)Math.Min(100000, Math.Floor(x.Log10() / Math.Log10(r) + 1e-9)));
    }

    int LevelsToBuy(int level, BigNum first, double r)
    {
        return Mode switch
        {
            BuyMode.One => 1,
            BuyMode.Ten => 10,
            BuyMode.TwentyFive => 25,
            BuyMode.Hundred => 100,
            BuyMode.Next => Defs.NextMilestone(level) - level,
            _ => Math.Max(1, MaxAffordable(first, r, Gold)),
        };
    }

    public (int n, BigNum cost) HeroPurchase(int i)
    {
        int lv = HeroLevels[i];
        var first = HeroBaseCost(i, lv);
        int n = LevelsToBuy(lv, first, HeroGrowth);
        return (n, SeriesCost(first, HeroGrowth, n));
    }

    public (int n, BigNum cost) TamerPurchase()
    {
        var first = TamerBaseCost(TamerLevel);
        int n = LevelsToBuy(TamerLevel, first, TamerGrowth);
        return (n, SeriesCost(first, TamerGrowth, n));
    }

    public BigNum HeroDpsAt(int i, int level)
    {
        if (level <= 0) return BigNum.Zero;
        return BigNum.FromLog10(Defs.MilestoneLog10(level)) * (Defs.Heroes[i].BaseDps * level * HeroMult()) * GlobalMult();
    }

    public BigNum TamerClickAt(int level) =>
        BigNum.FromLog10(Defs.MilestoneLog10(level)) * (double)level * ClickMult() * GlobalMult();

    public bool HeroVisibleInShop(int i) => i == 0 || HeroLevels[i - 1] > 0 || HeroLevels[i] > 0;

    public void BuyHero(int i)
    {
        var (n, cost) = HeroPurchase(i);
        if (Gold < cost) { Sfx.Play(Sfx.Deny); return; }
        Gold -= cost;
        int old = HeroLevels[i];
        HeroLevels[i] += n;
        HeroLevelFx[i] = 1f;
        OnLevelsGained(Defs.Heroes[i].Name, old, HeroLevels[i], HeroPos(i), Defs.Heroes[i].Secondary);
        if (old == 0)
        {
            Fx.Banner($"{Defs.Heroes[i].Name} joins your party!", Defs.Heroes[i].Title, Defs.Heroes[i].Secondary);
            Fx.Burst3D(HeroPos(i) + new Vector3(0, 1, 0), Defs.Heroes[i].Secondary, 60, 6);
            Fx.Shake(0.3f);
            Sfx.Play(Sfx.Milestone);
        }
    }

    public void BuyTamer()
    {
        var (n, cost) = TamerPurchase();
        if (Gold < cost) { Sfx.Play(Sfx.Deny); return; }
        Gold -= cost;
        int old = TamerLevel;
        TamerLevel += n;
        TamerLevelFx = 1f;
        OnLevelsGained("Dragon Tamer", old, TamerLevel, TamerPos, new Color(255, 220, 120, 255));
    }

    void OnLevelsGained(string who, int oldLv, int newLv, Vector3 pos, Color c)
    {
        Fx.LevelUpRing(pos, c);
        Fx.WorldText(pos + new Vector3(0, 2.4f, 0), $"+{newLv - oldLv} LV", new Color(255, 240, 150, 255), 30, 1.2f);
        var ms = new List<int>();
        for (int m = Defs.NextMilestone(oldLv); m <= newLv; m = Defs.NextMilestone(m)) ms.Add(m);
        if (ms.Count > 0 && oldLv > 0)
        {
            double mult = 1;
            foreach (var m in ms) mult *= Defs.MilestoneMult(m);
            Fx.Banner($"{who.ToUpper()} MILESTONE!", $"Level {ms[^1]} reached  -  DPS x{mult:0}", c);
            Fx.Burst3D(pos + new Vector3(0, 1.2f, 0), new Color(255, 230, 100, 255), 80, 8);
            Fx.Burst3D(pos + new Vector3(0, 1.2f, 0), c, 50, 5);
            Fx.Pillar(pos, c);
            Fx.Shake(0.35f);
            Fx.Flash(new Color(255, 240, 180, 255), 0.25f);
            Sfx.Play(Sfx.Milestone);
        }
        else Sfx.Play(Sfx.LevelUp);
    }

    // ---------- Generic upgrade shops ----------
    public static BigNum UpgradeCost(UpgradeDef d, int level) => (BigNum.Pow(d.Growth, level) * d.BaseCost).Ceil();

    public bool TryBuyUpgrade(UpgradeDef d, int[] levels, int i, ref BigNum wallet, string verb)
    {
        if (d.MaxLevel > 0 && levels[i] >= d.MaxLevel) return false;
        var cost = UpgradeCost(d, levels[i]);
        if (wallet < cost) { Sfx.Play(Sfx.Deny); return false; }
        wallet -= cost;
        levels[i]++;
        Sfx.Play(levels[i] % 10 == 0 ? Sfx.Milestone : Sfx.LevelUp);
        if (levels[i] == 1) Fx.Banner($"{d.Name} {verb}!", d.Desc, d.Color);
        else if (levels[i] % 10 == 0) Fx.Banner($"{d.Name} reached level {levels[i]}!", d.Desc, d.Color);
        return true;
    }

    public void BuyArtifact(int i) => TryBuyUpgrade(Defs.Artifacts[i], ArtifactLevels, i, ref Gems, "discovered");
    public void BuySoulUpgrade(int i) => TryBuyUpgrade(Defs.SoulUpgrades[i], SoulLevels, i, ref Souls, "awakened");

    public void BuyDragon(int i)
    {
        if (TryBuyUpgrade(Defs.Dragons[i], DragonLevels, i, ref Scales, "hatched"))
        {
            var p = Render.DragonPetPos(this, i, (float)Raylib.GetTime());
            Fx.Burst3D(p, Defs.Dragons[i].Color, DragonLevels[i] == 1 ? 90 : 30, 6);
            if (DragonLevels[i] == 1) { Fx.Shake(0.4f); Fx.Flash(Defs.Dragons[i].Color, 0.3f); }
        }
    }

    // ---------- Enemies ----------
    public static bool IsBossStage(long s) => s % 5 == 0;
    public static bool IsDragonStage(long s) => s % 10 == 0;

    public static BigNum EnemyHp(long s)
    {
        double l = Math.Log10(8) + (Math.Min(s, 100) - 1) * Math.Log10(1.36) + Math.Max(0, s - 100) * Math.Log10(1.2);
        var hp = BigNum.FromLog10(l);
        if (IsDragonStage(s)) hp *= 15;
        else if (IsBossStage(s)) hp *= 10;
        return hp.Ceil();
    }

    public BigNum GoldForKill(long s) => BigNum.Max(1, EnemyHp(s) / 6 * GoldMult()).Ceil();

    public void SpawnEnemy()
    {
        var biome = Defs.Biomes[Defs.BiomeIndex(Stage)];
        var e = new Enemy { Seed = Rng.Next(), Spawn = 1 };
        e.Kind = biome.Enemies[Rng.Next(biome.Enemies.Length)];
        e.IsBoss = IsBossStage(Stage);
        e.IsDragon = IsDragonStage(Stage);
        int bi = Defs.BiomeIndex(Stage);
        e.Tint = biome.Accent;
        if (e.IsDragon)
        {
            int n = (int)((Stage / 10 - 1) % Defs.DragonNames.Length);
            e.Name = $"{Defs.DragonNames[n]} {Defs.DragonEpithets[bi]}";
            e.Tint = Defs.DragonBossColors[bi];
            e.Scale = 2.0f;
        }
        else if (e.IsBoss)
        {
            e.Name = $"{Defs.BiomeAdj[bi]} {Defs.EnemyNames[(int)e.Kind]} Warlord";
            e.Scale = 1.7f;
        }
        else
        {
            e.Name = $"{Defs.BiomeAdj[bi]} {Defs.EnemyNames[(int)e.Kind]}";
            e.Scale = 0.9f + (float)Rng.NextDouble() * 0.25f;
        }
        e.MaxHp = EnemyHp(Stage);
        e.Hp = e.MaxHp;
        Enemy = e;
        if (e.IsBoss)
        {
            BossTimerMax = BossTime();
            BossTimer = BossTimerMax;
            if (e.IsDragon) { Sfx.Play(Sfx.Roar); Fx.Shake(0.5f); }
        }
    }

    public int KillsNeeded => IsBossStage(Stage) ? 1 : KillsPerStage;

    public Vector3 EnemyHitPoint() =>
        EnemyPos + new Vector3(0, (Enemy.IsDragon ? 2.2f : 0.9f) * Enemy.Scale, 0);

    public void DealDamage(BigNum dmg, bool crit, int source, Color c)
    {
        if (!Enemy.Alive) return;
        Enemy.Hp -= dmg;
        Enemy.HitFlash = 0.12f;
        Enemy.Wobble = crit ? 1f : Math.Max(Enemy.Wobble, 0.4f);
        var p = EnemyHitPoint() + new Vector3((float)Rng.NextDouble() - 0.5f, (float)Rng.NextDouble() * 0.8f, 0.8f);
        if (source == -1)
            Fx.WorldText(p, crit ? $"{dmg}!" : dmg.ToString(), crit ? new Color(255, 140, 30, 255) : Color.White, crit ? 44 : 28, crit ? 1.1f : 0.8f);
        else if (source >= 0)
            Fx.WorldText(p, dmg.ToString(), c, 20, 0.7f);
        else
            Fx.WorldText(p, dmg.ToString(), c, 34, 1.0f);
        Fx.Sparks(p, c, crit ? 18 : 6);
        if (Enemy.Hp <= BigNum.Zero) Kill();
    }

    void Kill()
    {
        var e = Enemy;
        e.Hp = BigNum.Zero;
        e.Dying = 1f;
        RespawnTimer = e.IsBoss ? 0.9f : 0.3f;
        var at = EnemyHitPoint();

        var gold = GoldForKill(Stage);
        AddGold(gold);
        Fx.CoinBurst(at, 0, e.IsBoss ? 24 : 6);
        Fx.WorldText(at + new Vector3(0, 1.2f, 0), $"+{gold}", new Color(255, 215, 60, 255), e.IsBoss ? 40 : 26, 1.3f);
        Fx.Burst3D(at, e.IsDragon ? e.Tint : Defs.Biomes[Defs.BiomeIndex(Stage)].Accent, e.IsBoss ? 90 : 25, e.IsBoss ? 7 : 4);
        Sfx.Play(e.IsBoss ? Sfx.BossKill : Sfx.Kill);
        Sfx.Play(Sfx.Coin);

        if (e.IsBoss)
        {
            var gems = (BigNum.Pow(1.015, Stage) * (Stage / 20.0) + 1).Floor();
            Gems += gems;
            Fx.CoinBurst(at, 1, 8);
            Fx.WorldText(at + new Vector3(0, 2.0f, 0), $"+{gems} Gems", new Color(90, 230, 255, 255), 32, 1.6f);
            Fx.Shake(0.6f);
            Fx.Flash(Color.White, 0.2f);
        }
        else if (Rng.NextDouble() < GemChance())
        {
            Gems += 1;
            Fx.CoinBurst(at, 1, 1);
            Fx.WorldText(at + new Vector3(0, 1.8f, 0), "+1 Gem", new Color(90, 230, 255, 255), 26, 1.3f);
        }
        if (e.IsDragon)
        {
            var scales = BigNum.Max(1, BigNum.Pow(1.025, Stage) * (Stage / 10.0 * ScaleMult())).Floor();
            Scales += scales;
            Fx.CoinBurst(at, 2, 12);
            Fx.WorldText(at + new Vector3(0, 2.8f, 0), $"+{scales} Dragon Scales", new Color(255, 120, 80, 255), 34, 1.8f);
            Fx.Banner($"{e.Name} SLAIN!", $"+{scales} Dragon Scales  -  hatch dragons in the Dragons tab", e.Tint);
        }
        else if (e.IsBoss)
            Fx.Banner("BOSS DEFEATED!", e.Name, new Color(255, 200, 80, 255));

        Kills++;
        if (Kills >= KillsNeeded)
        {
            if (Stage >= MaxStage)
            {
                MaxStage = Stage + 1;
                BestStage = Math.Max(BestStage, MaxStage);
            }
            if (AutoAdvance && Stage < MaxStage) ChangeStage(Stage + 1);
        }
    }

    public void ChangeStage(long s)
    {
        s = Math.Clamp(s, 1, MaxStage);
        if (s == Stage) return;
        int oldBiome = Defs.BiomeIndex(Stage);
        Stage = s;
        Kills = 0;
        BossTimer = 0;
        if (Enemy.Alive || RespawnTimer <= 0) { Enemy.Dying = 0.01f; Enemy.Hp = BigNum.Zero; RespawnTimer = 0.25f; }
        if (Defs.BiomeIndex(Stage) != oldBiome)
        {
            var b = Defs.Biomes[Defs.BiomeIndex(Stage)];
            Fx.Banner(b.Name + (Defs.BiomeCycle(Stage) > 0 ? " " + Defs.Roman(Defs.BiomeCycle(Stage) + 1) : ""), "A new realm awaits...", b.Accent);
        }
    }

    public void AddGold(BigNum g)
    {
        Gold += g;
        TotalGold += g;
        goldLog.Enqueue((PlayTime, g));
    }

    // ---------- Actions ----------
    public void ClickAttack(bool auto)
    {
        if (!Enemy.Alive) return;
        bool crit = Rng.NextDouble() < CritChance();
        var dmg = ClickDamage * (crit ? CritMult() : 1);
        Clicks++;
        TamerAnim = 1;
        if (!auto) { Sfx.Play(crit ? Sfx.Crit : Sfx.Hit); if (crit) Fx.Shake(0.15f); }
        else if (Rng.Next(4) == 0) Sfx.Play(Sfx.Hit, 0.4f);
        DealDamage(dmg, crit, -1, Color.White);
    }

    public bool AbilityUnlocked(int i) => BestStage >= Defs.Abilities[i].UnlockStage;

    public void UseAbility(int i)
    {
        if (!AbilityUnlocked(i) || AbilityCd[i] > 0) { Sfx.Play(Sfx.Deny); return; }
        var a = Defs.Abilities[i];
        AbilityActive[i] = a.Duration;
        AbilityCd[i] = a.Cooldown * CooldownMult();
        Fx.Banner(a.Name.ToUpper() + "!", a.Desc, a.Color);
        Fx.Flash(a.Color, 0.3f);
        Fx.Shake(0.3f);
        Sfx.Play(Sfx.Ability);
        if (i == 1) { meteorPool = TotalDps * 60; meteorAcc = 0; }
    }

    public BigNum SoulsOnAscend()
    {
        if (MaxStage < 30) return BigNum.Zero;
        long s = MaxStage - 30;
        return ((BigNum.Pow(1.06, s) * 5 + s) * SoulMult()).Floor();
    }

    public void Ascend()
    {
        var gain = SoulsOnAscend();
        if (!gain.IsPositive) return;
        Souls += gain;
        LifetimeSouls += gain;
        Ascensions++;
        Gold = BigNum.Zero;
        TamerLevel = 1;
        Array.Clear(HeroLevels);
        Stage = 1; MaxStage = 1; Kills = 0; AutoAdvance = true;
        Array.Clear(AbilityCd); Array.Clear(AbilityActive);
        Projectiles.Clear();
        SpawnEnemy();
        Fx.Banner("ASCENSION!", $"+{gain} Dragon Souls  -  your legend grows stronger", new Color(200, 120, 255, 255));
        Fx.Flash(new Color(200, 120, 255, 255), 0.8f);
        Fx.Shake(0.8f);
        Sfx.Play(Sfx.Milestone);
        Sfx.Play(Sfx.BossKill);
        Save();
    }

    // ---------- Frame update ----------
    public void RecalcStats()
    {
        TotalDps = BigNum.Zero;
        for (int i = 0; i < HeroDps.Length; i++)
        {
            HeroDps[i] = HeroDpsAt(i, HeroLevels[i]);
            TotalDps += HeroDps[i];
        }
        ClickDamage = TamerClickAt(TamerLevel) + TotalDps * ClickDpsPct();
        if (AbilityActive[0] > 0) ClickDamage = ClickDamage + TotalDps * ClickDpsPct() * 9;
    }

    public void Update(float dt)
    {
        PlayTime += dt;
        RecalcStats();

        for (int i = 0; i < AbilityCd.Length; i++)
        {
            AbilityCd[i] = Math.Max(0, AbilityCd[i] - dt);
            AbilityActive[i] = Math.Max(0, AbilityActive[i] - dt);
        }

        // Enemy lifecycle
        if (!Enemy.Alive)
        {
            Enemy.Dying = Math.Max(0, Enemy.Dying - dt * 2.2f);
            RespawnTimer -= dt;
            if (RespawnTimer <= 0) SpawnEnemy();
        }
        else
        {
            Enemy.Spawn = Math.Max(0, Enemy.Spawn - dt * 3);
            if (Enemy.IsBoss)
            {
                BossTimer -= dt;
                if (BossTimer <= 0) BossFailed();
            }
        }
        Enemy.HitFlash = Math.Max(0, Enemy.HitFlash - dt);
        Enemy.Wobble = Math.Max(0, Enemy.Wobble - dt * 3);

        // Auto clicks
        float cps = 2 * SoulLevels[2] + (AbilityActive[4] > 0 ? 20 : 0);
        autoClickAcc += cps * dt;
        while (autoClickAcc >= 1) { autoClickAcc -= 1; ClickAttack(true); }

        // Meteors
        if (AbilityActive[1] > 0)
        {
            meteorAcc += dt;
            while (meteorAcc >= 0.1f)
            {
                meteorAcc -= 0.1f;
                var target = EnemyHitPoint() + new Vector3((float)Rng.NextDouble() * 2 - 1, 0, (float)Rng.NextDouble() * 2 - 1);
                Projectiles.Add(new Projectile
                {
                    From = target + new Vector3(-6 + (float)Rng.NextDouble() * 4, 14, -4),
                    To = target, Duration = 0.5f, Kind = ProjKind.Meteor, Color = new Color(255, 140, 40, 255),
                    Damage = meteorPool / 30, Source = -2,
                });
            }
        }

        // Hero attacks
        for (int i = 0; i < HeroLevels.Length; i++)
        {
            HeroAnim[i] = Math.Max(0, HeroAnim[i] - dt * 2.5f);
            HeroLevelFx[i] = Math.Max(0, HeroLevelFx[i] - dt);
            if (HeroLevels[i] <= 0) continue;
            var def = Defs.Heroes[i];
            HeroAttackTimer[i] += dt;
            if (HeroAttackTimer[i] >= def.Interval)
            {
                HeroAttackTimer[i] -= def.Interval;
                if (HeroAttackTimer[i] > def.Interval) HeroAttackTimer[i] = 0;
                HeroAttack(i);
            }
        }
        TamerAnim = Math.Max(0, TamerAnim - dt * 5);
        TamerLevelFx = Math.Max(0, TamerLevelFx - dt);

        // Projectiles
        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            var p = Projectiles[i];
            p.T += dt / p.Duration;
            Fx.Trail(p);
            if (p.T >= 1)
            {
                Projectiles.RemoveAt(i);
                Fx.Impact(p.To, p.Color, p.Kind);
                if (p.Kind == ProjKind.Meteor) { Fx.Shake(0.12f); Sfx.Play(Sfx.Boom, 0.5f); }
                DealDamage(p.Damage, false, p.Source, p.Color);
            }
        }

        // Pet dragons breathe fire occasionally (cosmetic + a bit of real damage)
        float t = (float)Raylib.GetTime();
        for (int i = 0; i < DragonLevels.Length; i++)
        {
            if (DragonLevels[i] <= 0) continue;
            DragonBreath[i] -= dt;
            if (DragonBreath[i] <= 0)
            {
                DragonBreath[i] = 3.5f + (float)Rng.NextDouble() * 3;
                var from = Render.DragonPetPos(this, i, t);
                Projectiles.Add(new Projectile
                {
                    From = from, To = EnemyHitPoint(), Duration = 0.45f, Kind = ProjKind.DragonFire,
                    Color = Defs.Dragons[i].Color, Damage = TotalDps * (0.5 + 0.1 * DragonLevels[i]), Source = -3,
                });
            }
        }

        // Gold/sec rolling window
        while (goldLog.Count > 0 && goldLog.Peek().t < PlayTime - 10) goldLog.Dequeue();
        BigNum sum = BigNum.Zero;
        foreach (var g in goldLog) sum += g.g;
        GoldPerSec = sum / Math.Min(10, Math.Max(1, PlayTime));

        AutosaveFlash = Math.Max(0, AutosaveFlash - dt);
        if (AutosaveSeconds > 0)
        {
            saveTimer += dt;
            if (saveTimer >= AutosaveSeconds) { Save(); AutosaveFlash = 2; }
        }
    }

    void HeroAttack(int i)
    {
        var def = Defs.Heroes[i];
        HeroAnim[i] = 1;
        var dmg = HeroDps[i] * def.Interval;
        if (def.Proj == ProjKind.None)
        {
            // Knight dashes a sword wave
            Projectiles.Add(new Projectile
            {
                From = HeroPos(i) + new Vector3(0.6f, 1.0f, 0), To = EnemyHitPoint(), Duration = 0.3f,
                Kind = ProjKind.Spear, Color = def.Secondary, Damage = dmg, Source = i,
            });
            return;
        }
        Projectiles.Add(new Projectile
        {
            From = HeroPos(i) + new Vector3(0.5f, def.Kind == HeroKind.Titan ? 2.8f : 1.4f, 0.2f),
            To = EnemyHitPoint() + new Vector3(0, (float)Rng.NextDouble() * 0.6f - 0.3f, 0),
            Duration = def.Proj == ProjKind.Lightning ? 0.12f : 0.45f,
            Kind = def.Proj, Color = def.Secondary, Damage = dmg, Source = i,
            Arc = def.Proj is ProjKind.Arrow or ProjKind.Axe ? 1.5f : 0.4f,
        });
    }

    void BossFailed()
    {
        Fx.Banner("THE BOSS ESCAPED!", "Level up your heroes, then challenge it again", new Color(255, 80, 80, 255));
        Sfx.Play(Sfx.Deny);
        AutoAdvance = false;
        Enemy.Dying = 0.01f;
        Enemy.Hp = BigNum.Zero;
        long target = Math.Max(1, Stage - 1);
        Stage = target;
        Kills = 0;
        RespawnTimer = 0.5f;
    }

    public void ChallengeBoss()
    {
        AutoAdvance = true;
        if (Stage < MaxStage) ChangeStage(MaxStage);
    }

    // ---------- Save / load ----------
    public void Save()
    {
        if (!SavingEnabled) return;
        var d = new SaveData
        {
            Gold = Gold.Serialize(), Gems = Gems.Serialize(), Scales = Scales.Serialize(), Souls = Souls.Serialize(),
            LifetimeSouls = LifetimeSouls.Serialize(), TotalGold = TotalGold.Serialize(), TamerLevel = TamerLevel,
            Heroes = HeroLevels, Artifacts = ArtifactLevels, Dragons = DragonLevels, SoulUps = SoulLevels,
            Cooldowns = AbilityCd, Stage = Stage, MaxStage = MaxStage, BestStage = BestStage, Kills = Kills,
            AutoAdvance = AutoAdvance, Ascensions = Ascensions, Clicks = Clicks, PlayTime = PlayTime,
            SavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        try
        {
            // Write to a temp file first so a crash mid-write can't corrupt the slot.
            string tmp = SavePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(d));
            File.Move(tmp, SavePath, true);
            File.WriteAllText(LastSlotPath, Slot.ToString());
            LastSaved = DateTime.Now;
            saveTimer = 0;
        }
        catch { /* best effort */ }
    }

    static void CopyInto(int[] src, int[] dst) => Array.Copy(src, dst, Math.Min(src.Length, dst.Length));

    public void Load()
    {
        SpawnEnemy();
        if (!File.Exists(SavePath)) return;
        SaveData? d;
        try { d = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SavePath)); } catch { return; }
        if (d == null) return;
        Gold = BigNum.Parse(d.Gold); Gems = BigNum.Parse(d.Gems); Scales = BigNum.Parse(d.Scales);
        Souls = BigNum.Parse(d.Souls); LifetimeSouls = BigNum.Parse(d.LifetimeSouls); TotalGold = BigNum.Parse(d.TotalGold);
        TamerLevel = Math.Max(1, d.TamerLevel);
        CopyInto(d.Heroes, HeroLevels); CopyInto(d.Artifacts, ArtifactLevels);
        CopyInto(d.Dragons, DragonLevels); CopyInto(d.SoulUps, SoulLevels);
        Array.Copy(d.Cooldowns, AbilityCd, Math.Min(d.Cooldowns.Length, AbilityCd.Length));
        Stage = Math.Max(1, d.Stage); MaxStage = Math.Max(Stage, d.MaxStage); BestStage = Math.Max(MaxStage, d.BestStage);
        Kills = d.Kills; AutoAdvance = d.AutoAdvance; Ascensions = d.Ascensions; Clicks = d.Clicks; PlayTime = d.PlayTime;
        SpawnEnemy();

        // Offline progress: heroes keep farming the current stage at half efficiency.
        double away = Math.Min(12 * 3600, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - d.SavedAt);
        if (away > 60)
        {
            RecalcStats();
            var hp = EnemyHp(Stage);
            if (TotalDps.IsPositive)
            {
                double killsPerSec = Math.Min(3, (TotalDps / hp).ToDouble());
                var earned = GoldForKill(Stage) * (killsPerSec * away * 0.5);
                if (earned.IsPositive)
                {
                    AddGold(earned);
                    goldLog.Clear();
                    var span = TimeSpan.FromSeconds(away);
                    Fx.Banner("WELCOME BACK, TAMER!", $"Away {(int)span.TotalHours}h {span.Minutes}m  -  your heroes earned {earned} gold", new Color(255, 215, 60, 255));
                }
            }
            for (int i = 0; i < AbilityCd.Length; i++) AbilityCd[i] = Math.Max(0, AbilityCd[i] - (float)away);
        }
    }
}
