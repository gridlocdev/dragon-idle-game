using System.Numerics;
using Raylib_cs;
using DragonIdle;

bool demo = args.Contains("--demo");
string? shotDir = Environment.GetEnvironmentVariable("DRAGON_SHOTS");

Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint | ConfigFlags.HighDpiWindow);
Raylib.InitWindow(1440, 860, "Dragon Idle");
Raylib.SetWindowMinSize(1000, 640);
{
    // Fit the window to the current display (small laptop screens included).
    int mon = Raylib.GetCurrentMonitor();
    int mw = Raylib.GetMonitorWidth(mon), mh = Raylib.GetMonitorHeight(mon);
    int ww = Math.Min(1440, mw - 40), wh = Math.Min(860, mh - 70);
    if (ww != Raylib.GetScreenWidth() || wh != Raylib.GetScreenHeight())
    {
        Raylib.SetWindowSize(ww, wh);
        Raylib.SetWindowPosition((mw - ww) / 2, Math.Max(30, (mh - wh) / 2));
    }
}
Raylib.SetExitKey(KeyboardKey.Null);
Raylib.SetTargetFPS(60);

Render.Init();
Ui.Init();
var settings = Settings.Load();
var sfx = new Sfx { Muted = settings.Muted };
sfx.Init();
Fx fx = null!;
Game game = null!;

// Creates a fresh game bound to a save slot; load=false starts a new adventure in that slot.
void StartGame(int slot, bool load)
{
    fx = new Fx { IconPos = Ui.IconPos, OnCoinArrive = type => { Ui.Pulse(type); sfx.Play(sfx.Coin, 0.35f); } };
    game = new Game { Fx = fx, Sfx = sfx, Slot = slot, AutosaveSeconds = settings.AutosaveSeconds };
    if (load) game.Load();
    else
    {
        Game.DeleteSlot(slot);
        game.SpawnEnemy();
        game.Save();
        fx.Banner($"A NEW LEGEND BEGINS", $"Save slot {slot}", new Color(255, 215, 60, 255));
    }
}

ShotTour? tour = null;
if (shotDir != null)
{
    sfx.Muted = true;
    ShotTour.RenderIcons(shotDir);
    tour = new ShotTour(shotDir, () =>
    {
        fx = new Fx { IconPos = Ui.IconPos, OnCoinArrive = type => Ui.Pulse(type) };
        return new Game { Fx = fx, Sfx = sfx, SavingEnabled = false };
    });
    game = tour.Tick(null!)!;
}
else if (demo)
{
    fx = new Fx { IconPos = Ui.IconPos, OnCoinArrive = type => { Ui.Pulse(type); sfx.Play(sfx.Coin, 0.35f); } };
    game = new Game { Fx = fx, Sfx = sfx, SavingEnabled = false };
    Demo(game);
}
else
{
    Game.MigrateLegacySave();
    StartGame(Game.LastUsedSlot(), true);
}
bool paused = false;
bool quit = false;

// Saves that happen without the player asking only run while autosave is on.
void AutoSaveNow() { if (settings.Autosave) game.Save(); }

var cam = new Camera3D(new Vector3(-2.0f, 6.4f, 15.5f), new Vector3(-2.2f, 1.9f, 0), Vector3.UnitY, 46, CameraProjection.Perspective);
var basePos = cam.Position;
var baseTarget = cam.Target;

while (!Raylib.WindowShouldClose() && !quit)
{
    float dt = Math.Min(Raylib.GetFrameTime(), 0.1f);
    float t = (float)Raylib.GetTime();
    var battle = Ui.Battle;
    var mouse = Raylib.GetMousePosition();

    if (tour != null)
    {
        if (tour.Tick(game) is { } next) game = next;
        paused = tour.Paused;
    }

    // ---- input ----
    if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.P))
    {
        paused = !paused;
        if (paused) AutoSaveNow();
    }
    Ui.Modal = paused;
    bool inPlayfield = !paused && Raylib.CheckCollisionPointRec(mouse, battle) && mouse.Y > battle.Y + 150 && mouse.Y < battle.Y + battle.Height - 125;
    if (Raylib.IsMouseButtonPressed(MouseButton.Left) && inPlayfield) game.ClickAttack(false);
    if (!paused)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Space)) game.ClickAttack(false);
        for (int i = 0; i < Defs.Abilities.Length; i++)
            if (Raylib.IsKeyPressed(KeyboardKey.One + i)) game.UseAbility(i);
        if (Raylib.IsKeyPressed(KeyboardKey.Tab)) Ui.NextTab();
    }
    if (Raylib.IsKeyPressed(KeyboardKey.M)) { sfx.Muted = !sfx.Muted; settings.Muted = sfx.Muted; settings.Save(); }
    if (Raylib.IsKeyPressed(KeyboardKey.F5) && game.SavingEnabled)
    {
        game.Save();
        Ui.QuickSaved();
        fx.Banner("GAME SAVED", $"Slot {game.Slot}", new Color(110, 235, 120, 255));
        sfx.Play(sfx.LevelUp);
    }

    // ---- simulate (frozen while paused) ----
    if (!paused)
    {
        game.Update(dt);
        fx.Update(dt);
        fx.Ambient(Defs.BiomeIndex(game.Stage), dt);
    }
    Ui.Update(dt);

    // ---- camera ----
    var sway = new Vector3(MathF.Sin(t * 0.21f) * 0.6f, MathF.Sin(t * 0.17f) * 0.25f, 0);
    var shake = fx.ShakeOffset(t);
    cam.Position = basePos + sway + shake;
    cam.Target = baseTarget + shake * 0.5f;
    int bw = (int)battle.Width, bh = (int)battle.Height;
    var camCopy = cam;
    fx.Project = v => Raylib.GetWorldToScreenEx(v, camCopy, bw, bh) + new Vector2(battle.X, battle.Y);

    // ---- draw ----
    var biome = Defs.Biomes[Defs.BiomeIndex(game.Stage)];
    Raylib.BeginDrawing();
    Raylib.ClearBackground(new Color(22, 18, 34, 255));
    Raylib.DrawRectangleGradientV((int)battle.X, (int)battle.Y, bw, bh, biome.SkyTop, biome.SkyBottom);

    Raylib.BeginMode3D(cam);
    Rlgl.DrawRenderBatchActive();
    float sx = Raylib.GetRenderWidth() / (float)Raylib.GetScreenWidth();
    float sy = Raylib.GetRenderHeight() / (float)Raylib.GetScreenHeight();
    Rlgl.Viewport((int)(battle.X * sx), (int)((Raylib.GetScreenHeight() - battle.Y - bh) * sy), (int)(bw * sx), (int)(bh * sy));
    Rlgl.MatrixMode(MatrixMode.Projection);
    Rlgl.LoadIdentity();
    Rlgl.MultMatrixf(Raymath.MatrixPerspective(cam.FovY * Raylib.DEG2RAD, bw / (double)bh, 0.1, 300));
    Rlgl.MatrixMode(MatrixMode.ModelView);

    Render.BeginFrame(cam, biome.SkyBottom);
    Render.DrawWorld(Defs.BiomeIndex(game.Stage), Defs.BiomeCycle(game.Stage), t);
    for (int i = 0; i < Defs.Heroes.Length; i++)
        if (game.HeroLevels[i] > 0) Render.DrawHero(game, i, t);
    Render.DrawTamer(game, t);
    Render.DrawEnemy(game, t);
    Render.DrawPetDragons(game, t);
    fx.DrawProjectiles(game, t);
    Render.DrawHeroAuras(game, t);
    Render.DrawGlowPass();
    fx.DrawParticles();
    Raylib.EndMode3D();
    Rlgl.Viewport(0, 0, Raylib.GetRenderWidth(), Raylib.GetRenderHeight());

    fx.DrawTexts();
    Ui.DrawBattleOverlay(game, t, dt);
    fx.DrawBanners(battle, t);
    if (fx.FlashAlpha > 0)
        Raylib.DrawRectangleRec(battle, new Color(fx.FlashCol.R, fx.FlashCol.G, fx.FlashCol.B, (byte)(Math.Min(1, fx.FlashAlpha) * 150)));
    Ui.DrawPanel(game, t);
    Ui.DrawTopBar(game, dt);
    fx.DrawCoins();
    Ui.SavedIndicator(game, t);
    if (!paused && Ui.PauseButton()) { paused = true; AutoSaveNow(); }
    if (paused)
    {
        var (action, slot) = Ui.DrawPauseMenu(game, sfx, settings, dt);
        switch (action)
        {
            case Ui.PauseAction.Resume: paused = false; break;
            case Ui.PauseAction.Quit: quit = true; break;
            case Ui.PauseAction.LoadSlot: AutoSaveNow(); StartGame(slot, true); paused = false; break;
            case Ui.PauseAction.NewGame: if (slot != game.Slot) AutoSaveNow(); StartGame(slot, false); paused = false; break;
        }
    }
    if (sfx.Muted && tour == null) Ui.Text("MUTED (M)", battle.X + battle.Width - 110, battle.Y + battle.Height - 26, 15, Color.White, true);
    if (tour is { WantsCapture: true } && tour.Capture(game) is { } nextGame) game = nextGame;
    Raylib.EndDrawing();
    if (tour is { Done: true }) break;
}

// "Save & Quit" always saves; closing the window only saves when autosave is on.
if (quit) game.Save(); else if (tour == null) AutoSaveNow();
Raylib.CloseAudioDevice();
Raylib.CloseWindow();

static void Demo(Game g)
{
    int[] lv = { 260, 180, 140, 120, 100, 80, 60, 50, 30, 25, 15, 10 };
    Array.Copy(lv, g.HeroLevels, lv.Length);
    g.TamerLevel = 120;
    for (int i = 0; i < g.DragonLevels.Length; i++) g.DragonLevels[i] = 5 + i * 3;
    g.Stage = 40; g.MaxStage = 40; g.BestStage = 40;
    g.Gold = BigNum.FromLog10(20);
    g.Gems = 500; g.Scales = 1234; g.Souls = 77; g.LifetimeSouls = 300;
    g.SpawnEnemy();
}
