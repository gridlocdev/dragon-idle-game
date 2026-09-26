using Raylib_cs;

namespace DragonIdle;

public enum HeroKind { Knight, Ranger, FireMage, Berserker, FrostWitch, Necromancer, Valkyrie, DragonKnight, Titan, VoidLich, Phoenix, Chronomancer }
public enum ProjKind { None, Arrow, Fireball, IceShard, SkullBolt, Lightning, VoidOrb, SunFlare, TimeRune, Meteor, DragonFire, Axe, Spear }

public record HeroDef(string Name, string Title, HeroKind Kind, double BaseCost, double BaseDps, float Interval,
    ProjKind Proj, Color Primary, Color Secondary);

public enum EnemyKind { Slime, Goblin, Skeleton, Mushroom, Orc, Wraith, Golem, Beholder, Treant, Imp }

public record Biome(string Name, Color Ground, Color SkyTop, Color SkyBottom, Color Prop, Color Accent, EnemyKind[] Enemies);

public record UpgradeDef(string Name, string Desc, double BaseCost, double Growth, int MaxLevel, Color Color);

public record AbilityDef(string Name, string Desc, float Duration, float Cooldown, int UnlockStage, Color Color);

public static class Defs
{
    static Color C(int r, int g, int b) => new(r, g, b, 255);

    public static readonly HeroDef[] Heroes =
    {
        new("Sir Brambleheart", "Knight of the Thorn", HeroKind.Knight, 15, 3, 0.9f, ProjKind.None, C(70, 110, 200), C(200, 205, 215)),
        new("Lyra Swiftwind", "Elven Ranger", HeroKind.Ranger, 150, 20, 0.7f, ProjKind.Arrow, C(60, 150, 70), C(140, 90, 50)),
        new("Pyrrhus", "Ember Archmage", HeroKind.FireMage, 2.2e3, 180, 1.2f, ProjKind.Fireball, C(190, 40, 40), C(255, 170, 40)),
        new("Grimhilde", "Dwarven Berserker", HeroKind.Berserker, 4e4, 2.1e3, 0.8f, ProjKind.Axe, C(150, 80, 40), C(230, 120, 40)),
        new("Seraphine", "Frost Witch", HeroKind.FrostWitch, 8e5, 2.6e4, 1.0f, ProjKind.IceShard, C(90, 190, 230), C(230, 245, 255)),
        new("Morthos", "Bone Necromancer", HeroKind.Necromancer, 2e7, 3.8e5, 1.3f, ProjKind.SkullBolt, C(90, 40, 130), C(120, 255, 140)),
        new("Astrid", "Storm Valkyrie", HeroKind.Valkyrie, 6e8, 6.5e6, 1.0f, ProjKind.Spear, C(230, 200, 90), C(245, 245, 255)),
        new("Draconis", "Dragon Knight", HeroKind.DragonKnight, 2e10, 1.2e8, 1.1f, ProjKind.DragonFire, C(170, 30, 50), C(40, 40, 50)),
        new("Thundrak", "Storm Titan", HeroKind.Titan, 1e12, 2.8e9, 1.5f, ProjKind.Lightning, C(80, 100, 160), C(140, 220, 255)),
        new("Nyx", "Void Lich", HeroKind.VoidLich, 7e13, 8.5e10, 1.2f, ProjKind.VoidOrb, C(40, 20, 60), C(200, 80, 255)),
        new("Aurelion", "Celestial Phoenix", HeroKind.Phoenix, 6e15, 3e12, 0.9f, ProjKind.SunFlare, C(255, 140, 30), C(255, 230, 100)),
        new("Chronos", "Keeper of Ages", HeroKind.Chronomancer, 8e17, 1.2e14, 1.4f, ProjKind.TimeRune, C(40, 170, 160), C(240, 210, 120)),
    };

    public static readonly string TamerName = "You, the Dragon Tamer";

    public static readonly Biome[] Biomes =
    {
        new("Emerald Meadows", C(86, 160, 70), C(110, 180, 255), C(215, 240, 255), C(50, 120, 50), C(255, 230, 120),
            new[] { EnemyKind.Slime, EnemyKind.Goblin, EnemyKind.Mushroom }),
        new("Whispering Woods", C(48, 105, 60), C(50, 90, 110), C(160, 205, 190), C(30, 80, 45), C(180, 255, 200),
            new[] { EnemyKind.Treant, EnemyKind.Mushroom, EnemyKind.Goblin, EnemyKind.Wraith }),
        new("Scorched Badlands", C(125, 62, 38), C(60, 18, 22), C(235, 120, 55), C(80, 40, 30), C(255, 120, 30),
            new[] { EnemyKind.Imp, EnemyKind.Orc, EnemyKind.Golem }),
        new("Frostfang Peaks", C(215, 228, 240), C(90, 130, 195), C(210, 230, 250), C(150, 200, 235), C(200, 240, 255),
            new[] { EnemyKind.Skeleton, EnemyKind.Wraith, EnemyKind.Golem, EnemyKind.Orc }),
        new("Crystal Caverns", C(58, 48, 88), C(18, 10, 40), C(90, 60, 140), C(170, 110, 255), C(120, 255, 240),
            new[] { EnemyKind.Beholder, EnemyKind.Golem, EnemyKind.Slime }),
        new("Abyssal Void", C(28, 22, 38), C(5, 0, 12), C(70, 20, 90), C(120, 40, 160), C(230, 80, 255),
            new[] { EnemyKind.Wraith, EnemyKind.Beholder, EnemyKind.Imp, EnemyKind.Skeleton }),
        new("Celestial Spire", C(230, 212, 165), C(255, 190, 120), C(255, 245, 220), C(250, 250, 240), C(255, 220, 90),
            new[] { EnemyKind.Golem, EnemyKind.Beholder, EnemyKind.Treant, EnemyKind.Imp }),
    };

    public static readonly string[] EnemyNames =
    {
        "Slime", "Goblin", "Skeleton", "Shroomling", "Orc Brute", "Wraith", "Golem", "Beholder", "Treant", "Imp",
    };

    public static readonly string[] BiomeAdj =
    {
        "Mossy", "Shadow", "Molten", "Frozen", "Crystal", "Void", "Radiant",
    };

    public static readonly string[] DragonNames =
    {
        "Ignis", "Vermithrax", "Glaurung", "Ancalagon", "Tiamat", "Nidhogg", "Fafnir", "Smaugrath", "Balerion", "Kalameet",
        "Zyrothax", "Mordrake", "Aurenth", "Velkhana", "Xaltharion", "Pyroclast",
    };

    public static readonly string[] DragonEpithets =
    {
        "the Verdant Wyrm", "the Shade Serpent", "the Crimson Tyrant", "the Frost Sovereign", "the Prism Drake",
        "the Devourer of Stars", "the Solar Emperor",
    };

    public static readonly Color[] DragonBossColors =
    {
        C(60, 170, 70), C(40, 70, 60), C(200, 40, 30), C(150, 210, 245), C(170, 90, 230), C(30, 20, 45), C(245, 200, 80),
    };

    // Artifacts: bought with gems, never reset.
    public static readonly UpgradeDef[] Artifacts =
    {
        new("Emberheart Amulet", "+50% hero DPS", 3, 1.32, 0, C(255, 110, 60)),
        new("Titan's Gauntlet", "+100% click damage", 3, 1.32, 0, C(200, 170, 120)),
        new("Crown of Avarice", "+40% gold found", 4, 1.35, 0, C(255, 215, 60)),
        new("Chrono Hourglass", "+2s boss timer", 6, 1.6, 15, C(120, 220, 255)),
        new("Four-Leaf Talisman", "+0.5% gem drop chance", 8, 1.55, 20, C(90, 220, 110)),
        new("Eye of the Storm", "+1% critical chance", 10, 1.5, 30, C(170, 120, 255)),
        new("Monocle of Thrift", "-4% hero costs", 12, 1.7, 20, C(230, 230, 230)),
        new("Dragonbone Horn", "+25% dragon scales", 15, 1.45, 0, C(240, 230, 200)),
    };

    // Pet dragons: bought with scales, never reset.
    public static readonly UpgradeDef[] Dragons =
    {
        new("Ember Wyrmling", "+25% all damage, x2 every 10 levels", 1, 1.38, 0, C(230, 70, 40)),
        new("Frost Drake", "+50% click damage, clicks deal +1% DPS", 4, 1.4, 0, C(110, 200, 255)),
        new("Gilded Wyvern", "+30% gold", 12, 1.42, 0, C(255, 200, 50)),
        new("Storm Serpent", "+50% critical damage", 30, 1.45, 0, C(150, 110, 255)),
        new("Void Leviathan", "+15% souls on ascension", 80, 1.5, 0, C(80, 30, 110)),
        new("Elder Sunwyrm", "x1.12 all damage (compounding)", 200, 1.55, 0, C(255, 245, 210)),
    };

    // Soul shop: bought with souls, never reset.
    public static readonly UpgradeDef[] SoulUpgrades =
    {
        new("Soul Might", "x1.5 all damage", 2, 1.8, 0, C(200, 120, 255)),
        new("Midas Soul", "x1.4 gold", 2, 1.8, 0, C(255, 210, 80)),
        new("Phantom Hands", "+2 auto-clicks per second", 3, 2.0, 15, C(180, 240, 255)),
        new("Time Warp", "-6% ability cooldowns", 5, 2.2, 10, C(120, 255, 220)),
        new("Savage Strikes", "+2% critical chance", 4, 2.0, 15, C(255, 90, 90)),
        new("Soul Harvest", "+15% souls on ascension", 10, 2.0, 0, C(150, 90, 255)),
        new("Dragon Bond", "+20% dragon power", 20, 2.5, 0, C(255, 130, 60)),
    };

    public static readonly AbilityDef[] Abilities =
    {
        new("Dragon Fury", "Clicks deal x10 damage", 15, 90, 3, C(255, 90, 40)),
        new("Meteor Storm", "Rain 60s of DPS from the sky", 3, 120, 8, C(255, 150, 40)),
        new("Golden Hoard", "x3 gold from all sources", 30, 180, 15, C(255, 215, 60)),
        new("War Cry", "x3 all damage", 30, 240, 25, C(230, 60, 60)),
        new("Frenzy", "20 auto-clicks per second", 10, 150, 35, C(120, 220, 255)),
    };

    public static int BiomeIndex(long stage) => (int)(((stage - 1) / 10) % Biomes.Length);
    public static int BiomeCycle(long stage) => (int)((stage - 1) / 10 / Biomes.Length);

    // Hero level milestones: early x2, mid x3 every 25, late x5 every 100. Infinite.
    public static int NextMilestone(int level)
    {
        int[] early = { 10, 25, 50, 75, 100 };
        foreach (var m in early) if (level < m) return m;
        if (level < 1000) return (level / 25 + 1) * 25;
        return (level / 100 + 1) * 100;
    }

    public static int PrevMilestone(int level)
    {
        int prev = 0;
        int m = NextMilestone(0);
        while (m <= level) { prev = m; m = NextMilestone(m); }
        return prev;
    }

    public static double MilestoneMult(int milestone) => milestone <= 100 ? 2 : milestone < 1000 ? 3 : 5;

    public static double MilestoneLog10(int level)
    {
        int c2 = 0;
        foreach (var m in new[] { 10, 25, 50, 75, 100 }) if (level >= m) c2++;
        int c3 = level >= 125 ? Math.Min((level - 100) / 25, 35) : 0;
        int c5 = level >= 1000 ? (level - 1000) / 100 + 1 : 0;
        return c2 * Math.Log10(2) + c3 * Math.Log10(3) + c5 * Math.Log10(5);
    }

    public static int MilestoneCount(int level)
    {
        int n = 0;
        for (int m = NextMilestone(0); m <= level; m = NextMilestone(m)) n++;
        return n;
    }

    public static string Roman(int n)
    {
        if (n <= 0) return "";
        string[] r = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return n <= 10 ? r[n - 1] : n.ToString();
    }
}
