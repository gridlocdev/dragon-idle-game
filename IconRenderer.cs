using System.Numerics;
using Raylib_cs;
using static DragonIdle.Render;

namespace DragonIdle;

// `dotnet run -- --render-icon <path.png>` renders the 1024x1024 app icon from the real in-game dragon,
// plus a multi-size Windows .ico next to it.
static class IconRenderer
{
    const int Size = 1024;
    const int Supersample = 2;
    static readonly int[] IcoSizes = { 16, 24, 32, 48, 64, 128, 256 };

    public static void Render(string path)
    {
        int n = Size * Supersample;
        var rt = Raylib.LoadRenderTexture(n, n);

        var cam = new Camera3D
        {
            Position = new Vector3(0.4f, 2.6f, 9.0f),
            Target = new Vector3(-0.1f, 1.45f, 0),
            Up = Vector3.UnitY,
            FovY = 40,
            Projection = CameraProjection.Perspective,
        };

        Raylib.BeginTextureMode(rt);
        Raylib.ClearBackground(new Color(0, 0, 0, 0));

        // Full-bleed, opaque square: macOS applies its own rounded mask. Any transparent margin makes
        // macOS 26+ shrink the artwork onto a grey tile instead.
        float s = Supersample;
        Raylib.DrawRectangleGradientV(0, 0, n, n, new Color(38, 18, 70, 255), new Color(236, 118, 52, 255));
        Raylib.DrawCircleV(new Vector2(400 * s, 330 * s), 230 * s, new Color(255, 214, 120, 70));
        Raylib.DrawCircleV(new Vector2(400 * s, 330 * s), 175 * s, new Color(255, 230, 160, 110));

        Raylib.BeginMode3D(cam);
        BeginFrame(cam, new Color(236, 118, 52, 255));

        // A hoard of gold under the dragon's feet
        var gold = new Color(255, 200, 60, 255);
        var rng = new Random(7);
        for (int k = 0; k < 26; k++)
        {
            float a = (float)(rng.NextDouble() * Math.PI * 2), r = (float)Math.Sqrt(rng.NextDouble()) * 2.1f;
            P(MathF.Cos(a) * r + 0.2f, 0.03f + (float)rng.NextDouble() * 0.12f, MathF.Sin(a) * r);
            RX((float)rng.NextDouble() * 40 - 20); RZ((float)rng.NextDouble() * 40 - 20);
            Cyl(0, 0, 0, 0.26f, 0.06f, k % 3 == 0 ? Mul(gold, 1.1f) : gold);
            Pop();
        }
        Ellip(0.2f, -0.25f, 0, 2.2f, 0.45f, 1.9f, new Color(230, 170, 40, 255));

        // The dragon, rearing up with wings raised and fire in its mouth
        P(0.3f, 0.1f, 0); RY(-122); S(1.05f);
        DrawDragon(new Color(210, 45, 40, 255), new Color(255, 205, 130, 255), new Color(150, 30, 35, 255), 0.12f, 0.9f, 0);
        Pop();
        Raylib.EndMode3D();
        Raylib.EndTextureMode();

        var img = Raylib.LoadImageFromTexture(rt.Texture);
        Raylib.ImageFlipVertical(ref img);
        Raylib.ImageResize(ref img, Size, Size);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        WriteIco(img, Path.ChangeExtension(path, ".ico"));

        var rgb = Raylib.ImageCopy(img);
        Raylib.ImageFormat(ref rgb, PixelFormat.UncompressedR8G8B8);   // no alpha channel at all
        Raylib.ExportImage(rgb, path);
        Raylib.UnloadImage(rgb);
        Raylib.UnloadImage(img);
        Raylib.UnloadRenderTexture(rt);
        Console.WriteLine($"Icon written to {path} and {Path.ChangeExtension(path, ".ico")}");
    }

    /// <summary>Writes a Windows .ico holding PNG-compressed images at the standard sizes.</summary>
    static void WriteIco(Image master, string path)
    {
        var pngs = new List<byte[]>();
        string tmp = Path.Combine(Path.GetTempPath(), $"dragonidle-ico-{Environment.ProcessId}.png");
        foreach (int size in IcoSizes)
        {
            var copy = Raylib.ImageCopy(master);
            Raylib.ImageResize(ref copy, size, size);
            Raylib.ImageFormat(ref copy, PixelFormat.UncompressedR8G8B8A8);
            Raylib.ExportImage(copy, tmp);
            Raylib.UnloadImage(copy);
            pngs.Add(File.ReadAllBytes(tmp));
        }
        File.Delete(tmp);

        using var w = new BinaryWriter(File.Create(path));
        w.Write((ushort)0);            // reserved
        w.Write((ushort)1);            // type: icon
        w.Write((ushort)pngs.Count);
        int offset = 6 + 16 * pngs.Count;
        for (int i = 0; i < pngs.Count; i++)
        {
            int size = IcoSizes[i];
            w.Write((byte)(size >= 256 ? 0 : size));   // 0 means 256
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0);          // palette colours
            w.Write((byte)0);          // reserved
            w.Write((ushort)1);        // colour planes
            w.Write((ushort)32);       // bits per pixel
            w.Write(pngs[i].Length);
            w.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var png in pngs) w.Write(png);
    }
}
