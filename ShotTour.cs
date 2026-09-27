using Raylib_cs;

namespace DragonIdle;

/// <summary>
/// Dev tool: plays a scripted tour of staged scenes and saves a screenshot of each.
/// Run with DRAGON_SHOTS=/some/dir dotnet run. Nothing is saved to the real save slots.
/// </summary>
public unsafe class ShotTour
{
    record Step(string Name, int Frames, Action<Game> Setup, Action<Game, int>? EachFrame = null, bool Paused = false);

    readonly string dir;
    readonly Func<Game> newGame;
    readonly List<Step> steps;
    int index = -1, frame;

    public bool Paused { get; private set; }
    public bool Done => index >= steps.Count;

    public ShotTour(string dir, Func<Game> newGame)
    {
        this.dir = dir;
        this.newGame = newGame;
        Directory.CreateDirectory(dir);
        steps = new()
        {
            new("early-game", 120, EarlyGame, Clicker(9)),
            new("pause-menu", 30, EarlyGame, Paused: true),
        };
    }

    static Action<Game, int> Clicker(int every) => (g, f) => { if (f % every == 0) g.ClickAttack(false); };

    /// <summary>Stage 3 with the first two heroes. Screenshots stay early so later content is a surprise.</summary>
    static void EarlyGame(Game g)
    {
        g.Stage = g.MaxStage = g.BestStage = 3;
        g.TamerLevel = 8;
        g.HeroLevels[0] = 12;
        g.HeroLevels[1] = 4;
        g.Gold = 180;
        g.Clicks = 60;
        g.PlayTime = 7 * 60;
        g.SpawnEnemy();
        Toughen(g, 25);
    }

    static readonly string[] IconNames = { "gold", "gems", "dragon-scales", "dragon-souls" };

    /// <summary>
    /// Renders each currency icon to a transparent PNG in dir/icons. Drawn at 4x and scaled down,
    /// since render textures don't get MSAA.
    /// </summary>
    public static void RenderIcons(string dir)
    {
        string outDir = Path.Combine(dir, "icons");
        Directory.CreateDirectory(outDir);
        const int big = 512, small = 128;
        var rt = Raylib.LoadRenderTexture(big, big);
        for (int i = 0; i < IconNames.Length; i++)
        {
            Raylib.BeginTextureMode(rt);
            Raylib.ClearBackground(new Color(0, 0, 0, 0));
            Ui.CurrencyIcon(i, new System.Numerics.Vector2(big / 2f, big / 2f + 12), 190, 1);
            Raylib.EndTextureMode();
            var img = Raylib.LoadImageFromTexture(rt.Texture);
            Raylib.ImageFlipVertical(ref img);
            Raylib.ImageResize(ref img, small, small);
            string path = Path.Combine(outDir, IconNames[i] + ".png");
            Raylib.ExportImage(img, path);
            Raylib.UnloadImage(img);
            Console.WriteLine($"Saved {path}");
        }
        Raylib.UnloadRenderTexture(rt);
    }

    /// <summary>Gives the current enemy enough HP to survive the shot, so it's on screen.</summary>
    static void Toughen(Game g, double seconds)
    {
        g.RecalcStats();
        var hp = BigNum.Max(g.Enemy.MaxHp, g.TotalDps * seconds + g.ClickDamage * seconds * 3);
        g.Enemy.MaxHp = hp;
        g.Enemy.Hp = hp * 0.82;
        g.Enemy.Spawn = 0;
    }

    /// <summary>Advances the tour. Returns a fresh game when a new step starts.</summary>
    public Game? Tick(Game current)
    {
        if (index >= 0 && index < steps.Count)
        {
            steps[index].EachFrame?.Invoke(current, frame);
            frame++;
            return null;
        }
        return Next();
    }

    Game? Next()
    {
        index++;
        frame = 0;
        if (Done) return null;
        Ui.SetTab(0);
        var g = newGame();
        steps[index].Setup(g);
        Paused = steps[index].Paused;
        return g;
    }

    public bool WantsCapture => !Done && index >= 0 && frame >= steps[index].Frames;

    /// <summary>Saves the back buffer. Call after drawing and before EndDrawing.</summary>
    public Game? Capture(Game current)
    {
        Rlgl.DrawRenderBatchActive();
        int w = Raylib.GetRenderWidth(), h = Raylib.GetRenderHeight();
        var img = new Image
        {
            Data = Rlgl.ReadScreenPixels(w, h),
            Width = w, Height = h, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8,
        };
        string path = Path.Combine(dir, steps[index].Name + ".png");
        Raylib.ExportImage(img, path);
        Raylib.UnloadImage(img);
        Console.WriteLine($"Saved {path}");
        return Next();
    }
}
