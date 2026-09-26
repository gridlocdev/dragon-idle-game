using System.Numerics;
using Raylib_cs;

namespace DragonIdle;

/// <summary>Builds every 3D character out of lit primitive meshes using the rlgl matrix stack.</summary>
public static unsafe class Render
{
    static Mesh cube, sphere, cyl, cone, torus;
    static Material mat;
    static Shader shader;
    static int locViewPos, locFog, locGlow, locFlash;
    static float curGlow = -1, curFlash = -1;

    public readonly record struct Glow(Vector3 Pos, float Radius, Color Color);
    public static readonly List<Glow> Glows = new();

    const string Vs = @"#version 330
in vec3 vertexPosition;
in vec3 vertexNormal;
uniform mat4 mvp;
uniform mat4 matModel;
uniform mat4 matNormal;
out vec3 fragPos;
out vec3 fragNormal;
void main() {
    fragPos = vec3(matModel * vec4(vertexPosition, 1.0));
    fragNormal = normalize(vec3(matNormal * vec4(vertexNormal, 0.0)));
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}";

    const string Fs = @"#version 330
in vec3 fragPos;
in vec3 fragNormal;
uniform vec4 colDiffuse;
uniform vec3 viewPos;
uniform vec3 fogColor;
uniform float glow;
uniform float flash;
out vec4 finalColor;
void main() {
    vec3 n = normalize(fragNormal);
    vec3 L = normalize(vec3(-0.45, 1.0, 0.55));
    float d = max(dot(n, L), 0.0);
    float band = d > 0.72 ? 1.0 : (d > 0.3 ? 0.75 : 0.5);
    vec3 V = normalize(viewPos - fragPos);
    float rim = pow(1.0 - max(dot(n, V), 0.0), 3.0);
    vec3 base = colDiffuse.rgb;
    vec3 lit = base * (0.32 + 0.78 * band) + rim * 0.28 * mix(vec3(1.0), base, 0.4);
    vec3 c = mix(lit, min(base * 1.25, vec3(1.0)), glow);
    c = mix(c, vec3(1.0), flash);
    float dist = length(viewPos - fragPos);
    float f = clamp((dist - 24.0) / 30.0, 0.0, 0.7);
    c = mix(c, fogColor, f * (1.0 - glow));
    finalColor = vec4(c, colDiffuse.a);
}";

    public static void Init()
    {
        cube = Raylib.GenMeshCube(1, 1, 1);
        sphere = Raylib.GenMeshSphere(1, 12, 16);
        cyl = Raylib.GenMeshCylinder(1, 1, 16);
        cone = Raylib.GenMeshCone(1, 1, 16);
        torus = Raylib.GenMeshTorus(0.12f, 1f, 12, 24);
        shader = Raylib.LoadShaderFromMemory(Vs, Fs);
        locViewPos = Raylib.GetShaderLocation(shader, "viewPos");
        locFog = Raylib.GetShaderLocation(shader, "fogColor");
        locGlow = Raylib.GetShaderLocation(shader, "glow");
        locFlash = Raylib.GetShaderLocation(shader, "flash");
        mat = Raylib.LoadMaterialDefault();
        mat.Shader = shader;
    }

    public static void BeginFrame(Camera3D cam, Color fog)
    {
        Raylib.SetShaderValue(shader, locViewPos, cam.Position, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locFog, new Vector3(fog.R / 255f, fog.G / 255f, fog.B / 255f), ShaderUniformDataType.Vec3);
        curGlow = -1; curFlash = -1;
        SetGlow(0); SetFlash(0);
        Glows.Clear();
    }

    public static void SetGlow(float g)
    {
        if (g == curGlow) return;
        curGlow = g;
        Raylib.SetShaderValue(shader, locGlow, g, ShaderUniformDataType.Float);
    }

    public static void SetFlash(float f)
    {
        if (f == curFlash) return;
        curFlash = f;
        Raylib.SetShaderValue(shader, locFlash, f, ShaderUniformDataType.Float);
    }

    // ---------- primitive helpers ----------
    static void DrawUnit(Mesh m, Color c)
    {
        mat.Maps[0].Color = c;
        Raylib.DrawMesh(m, mat, Matrix4x4.Identity);
    }

    public static void P(float x, float y, float z) { Rlgl.PushMatrix(); Rlgl.Translatef(x, y, z); }
    public static void P(Vector3 v) => P(v.X, v.Y, v.Z);
    public static void Pop() => Rlgl.PopMatrix();
    public static void RX(float d) => Rlgl.Rotatef(d, 1, 0, 0);
    public static void RY(float d) => Rlgl.Rotatef(d, 0, 1, 0);
    public static void RZ(float d) => Rlgl.Rotatef(d, 0, 0, 1);
    public static void S(float s) => Rlgl.Scalef(s, s, s);

    public static void Box(float x, float y, float z, float sx, float sy, float sz, Color c)
    {
        P(x, y, z); Rlgl.Scalef(sx, sy, sz); DrawUnit(cube, c); Pop();
    }

    public static void Ellip(float x, float y, float z, float sx, float sy, float sz, Color c)
    {
        P(x, y, z); Rlgl.Scalef(sx, sy, sz); DrawUnit(sphere, c); Pop();
    }

    public static void Sph(float x, float y, float z, float r, Color c) => Ellip(x, y, z, r, r, r, c);

    public static void Cyl(float x, float y, float z, float r, float h, Color c)
    {
        P(x, y, z); Rlgl.Scalef(r, h, r); DrawUnit(cyl, c); Pop();
    }

    public static void Cone(float x, float y, float z, float r, float h, Color c)
    {
        P(x, y, z); Rlgl.Scalef(r, h, r); DrawUnit(cone, c); Pop();
    }

    public static void Torus(float x, float y, float z, float r, Color c)
    {
        P(x, y, z); S(r); DrawUnit(torus, c); Pop();
    }

    /// <summary>Emissive sphere plus a queued additive halo.</summary>
    public static void Orb(float x, float y, float z, float r, Color c, float halo = 2.5f)
    {
        SetGlow(1); Sph(x, y, z, r, c); SetGlow(0);
        var m = Rlgl.GetMatrixTransform();
        var wp = Raymath.Vector3Transform(new Vector3(x, y, z), m);
        var sc2 = (Raymath.Vector3Transform(new Vector3(x + 1, y, z), m) - wp).Length();
        Glows.Add(new Glow(wp, r * halo * Math.Max(sc2, 0.01f), c));
    }

    public static void GlowBox(float x, float y, float z, float sx, float sy, float sz, Color c)
    {
        SetGlow(1); Box(x, y, z, sx, sy, sz, c); SetGlow(0);
    }

    public static Color Mul(Color c, float f) =>
        new((byte)Math.Clamp(c.R * f, 0, 255), (byte)Math.Clamp(c.G * f, 0, 255), (byte)Math.Clamp(c.B * f, 0, 255), c.A);

    public static Color Lerp(Color a, Color b, float t) =>
        new((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t), (byte)(a.A + (b.A - a.A) * t));

    static Color C(int r, int g, int b, int a = 255) => new(r, g, b, a);

    static readonly Color Skin = C(240, 195, 160);
    static readonly Color Dark = C(40, 35, 45);
    static readonly Color Leather = C(110, 70, 40);
    static readonly Color Gold = C(255, 200, 60);
    static readonly Color Bone = C(235, 230, 210);
    static readonly Color Steel = C(190, 195, 210);

    public static void Shadow(float x, float z, float r)
    {
        Cyl(x, 0.015f, z, r, 0.01f, C(0, 0, 0, 70));
    }

    // ---------- humanoid rig (faces +X, right hand is +Z) ----------
    public struct Rig
    {
        public Color Body, Legs, Arms, Head, Boots;
        public float SwingR, SwingL, Bob, Bulk;
        public bool Robe, NoHead, NoLegs;
        public Action? RightHand, LeftHand;
    }

    public static void Humanoid(in Rig r)
    {
        float b = r.Bob;
        float bulk = r.Bulk <= 0 ? 1 : r.Bulk;
        if (r.Robe)
        {
            Cone(0, 0, 0, 0.42f * bulk, 1.45f, r.Body);
            Cyl(0, 0, 0, 0.42f * bulk, 0.08f, Mul(r.Body, 0.7f));
        }
        else if (!r.NoLegs)
        {
            Box(0, 0.36f, 0.13f, 0.2f, 0.7f, 0.2f, r.Legs);
            Box(0, 0.36f, -0.13f, 0.2f, 0.7f, 0.2f, r.Legs);
            Box(0.05f, 0.06f, 0.13f, 0.3f, 0.12f, 0.22f, r.Boots.A == 0 ? Dark : r.Boots);
            Box(0.05f, 0.06f, -0.13f, 0.3f, 0.12f, 0.22f, r.Boots.A == 0 ? Dark : r.Boots);
        }
        Box(0, 1.02f + b, 0, 0.38f * bulk, 0.62f, 0.56f * bulk, r.Body);
        Box(0, 0.74f + b, 0, 0.41f * bulk, 0.09f, 0.58f * bulk, Leather);
        Box(0.2f * bulk, 0.74f + b, 0, 0.04f, 0.1f, 0.12f, Gold);
        if (!r.NoHead)
        {
            Sph(0.02f, 1.56f + b, 0, 0.24f, r.Head);
            Sph(0.21f, 1.59f + b, 0.085f, 0.04f, Dark);
            Sph(0.21f, 1.59f + b, -0.085f, 0.04f, Dark);
        }
        Arm(0.38f * bulk, b, r.SwingR, r.Arms, r.Head, r.RightHand, 1);
        Arm(-0.38f * bulk, b, r.SwingL, r.Arms, r.Head, r.LeftHand, -1);
    }

    static void Arm(float z, float bob, float swing, Color arm, Color hand, Action? held, int side)
    {
        P(0, 1.27f + bob, z);
        RZ(swing);
        Box(0, -0.27f, 0, 0.17f, 0.56f, 0.17f, arm);
        Sph(0, -0.58f, 0, 0.1f, hand);
        held?.Invoke();
        Pop();
    }

    static float Swing(float anim, float idle = 15, float up = 150, float end = -10)
    {
        if (anim <= 0) return idle;
        float t = 1 - anim;
        if (t < 0.25f) return idle + (up - idle) * (t / 0.25f);
        float k = (t - 0.25f) / 0.75f;
        if (k < 0.3f) return up + (end - up) * (k / 0.3f);
        return end + (idle - end) * ((k - 0.3f) / 0.7f);
    }

    static float Cast(float anim, float idle, float peak) => idle + (peak - idle) * (float)Math.Sin(Math.Clamp(anim, 0, 1) * Math.PI);

    static void Sword(Color blade, bool glow)
    {
        Box(0.08f, -0.58f, 0, 0.06f, 0.3f, 0.1f, Gold);
        Box(-0.08f, -0.58f, 0, 0.22f, 0.06f, 0.06f, Leather);
        if (glow) SetGlow(0.6f);
        Box(0.58f, -0.58f, 0, 0.95f, 0.12f, 0.035f, blade);
        SetGlow(0);
        P(1.08f, -0.58f, 0); RZ(-90); Cone(0, 0, 0, 0.06f, 0.14f, blade); Pop();
    }

    static void Staff(Color wood, Color orb, float len = 1.6f, bool orbGlow = true)
    {
        Box(len * 0.3f, -0.58f, 0, len, 0.06f, 0.06f, wood);
        if (orbGlow) Orb(len * 0.8f + 0.1f, -0.58f, 0, 0.14f, orb);
    }

    static void WizardHat(float y, Color c, float tilt)
    {
        Cyl(0, y, 0, 0.4f, 0.04f, Mul(c, 0.8f));
        P(0, y, 0); RZ(tilt);
        Cone(0, 0, 0, 0.26f, 0.45f, c);
        P(0, 0.4f, 0); RZ(-25); Cone(0, 0, 0, 0.1f, 0.3f, c); Pop();
        Pop();
    }

    // ---------- heroes ----------
    public static void DrawHero(Game g, int i, float t)
    {
        var def = Defs.Heroes[i];
        int lv = g.HeroLevels[i];
        var p = Game.HeroPos(i);
        float a = g.HeroAnim[i];
        float bob = (float)Math.Sin(t * 2.2f + i) * 0.03f;
        float pop = 1 + 0.18f * g.HeroLevelFx[i] * (float)Math.Abs(Math.Sin(g.HeroLevelFx[i] * 12));
        float size = (1 + Math.Min(0.3f, (float)Math.Log10(Math.Max(1, lv)) * 0.08f)) * pop;
        int tier = lv >= 1000 ? 4 : lv >= 500 ? 3 : lv >= 100 ? 2 : lv >= 25 ? 1 : 0;
        bool shiny = tier >= 1;

        Shadow(p.X, p.Z, 0.5f * size);
        P(p); RY(-35); S(size);
        var pc = def.Primary; var sc = def.Secondary;
        switch (def.Kind)
        {
            case HeroKind.Knight:
            {
                Box(-0.23f, 1.0f + bob, 0, 0.05f, 0.95f, 0.55f, pc);
                Humanoid(new Rig
                {
                    Body = sc, Legs = Mul(sc, 0.7f), Arms = sc, Head = Skin, Bob = bob, Boots = Mul(sc, 0.5f),
                    SwingR = Swing(a), SwingL = 35,
                    RightHand = () => Sword(tier >= 2 ? C(160, 230, 255) : Steel, shiny),
                    LeftHand = () =>
                    {
                        Box(0.1f, -0.5f, -0.1f, 0.5f, 0.55f, 0.08f, pc);
                        Box(0.1f, -0.5f, -0.15f, 0.12f, 0.35f, 0.02f, Gold);
                    },
                });
                Box(0.2f, 0.98f + bob, 0, 0.03f, 0.5f, 0.3f, pc);
                Sph(0.02f, 1.58f + bob, 0, 0.27f, sc);
                Box(0.24f, 1.58f + bob, 0, 0.06f, 0.06f, 0.3f, Dark);
                Ellip(-0.08f, 1.9f + bob, 0, 0.26f, 0.09f, 0.06f, C(220, 40, 40));
                break;
            }
            case HeroKind.Ranger:
            {
                float draw = a > 0 ? 1 - a : 0;
                Humanoid(new Rig
                {
                    Body = pc, Legs = Leather, Arms = pc, Head = Skin, Bob = bob, Boots = Mul(Leather, 0.6f),
                    SwingR = 80 + draw * 10, SwingL = 88,
                    LeftHand = () =>
                    {
                        P(0, -0.62f, 0);
                        Box(0, 0, 0, 0.08f, 0.08f, 0.08f, Leather);
                        P(0, 0, 0); RZ(12); Box(0.32f, 0.02f, 0, 0.62f, 0.06f, 0.06f, C(120, 80, 40)); Pop();
                        P(0, 0, 0); RZ(-12); Box(-0.32f, 0.02f, 0, 0.62f, 0.06f, 0.06f, C(120, 80, 40)); Pop();
                        Box(0, 0.12f + draw * 0.1f, 0, 1.1f, 0.015f, 0.015f, C(240, 240, 240));
                        if (a > 0.2f) Box(0, 0.3f, 0, 0.03f, 0.7f, 0.03f, C(230, 220, 190));
                        Pop();
                    },
                });
                Ellip(-0.05f, 1.62f + bob, 0, 0.25f, 0.27f, 0.27f, Mul(pc, 0.85f));
                P(-0.2f, 1.72f + bob, 0); RZ(110); Cone(0, 0, 0, 0.15f, 0.35f, Mul(pc, 0.85f)); Pop();
                P(-0.26f, 1.05f + bob, -0.1f); RZ(-20);
                Cyl(0, 0, 0, 0.1f, 0.6f, Leather);
                for (int k = 0; k < 3; k++) Box(0, 0.65f, -0.05f + k * 0.05f, 0.03f, 0.12f, 0.03f, C(230, 60, 60));
                Pop();
                break;
            }
            case HeroKind.FireMage:
            case HeroKind.FrostWitch:
            {
                bool fire = def.Kind == HeroKind.FireMage;
                Humanoid(new Rig
                {
                    Body = pc, Arms = pc, Head = Skin, Bob = bob, Robe = true,
                    SwingR = Cast(a, 55, 110), SwingL = Cast(a, 20, 70),
                    RightHand = () =>
                    {
                        Staff(fire ? C(90, 55, 30) : C(200, 230, 255), sc);
                        if (!fire)
                        {
                            SetGlow(0.5f);
                            P(1.4f, -0.58f, 0); RY(t * 90); Box(0, 0, 0, 0.12f, 0.35f, 0.12f, C(170, 240, 255)); Pop();
                            SetGlow(0);
                        }
                    },
                });
                if (fire) Ellip(0.16f, 1.4f + bob, 0, 0.12f, 0.22f, 0.17f, C(235, 235, 235));
                else Ellip(-0.1f, 1.45f + bob, 0, 0.18f, 0.4f, 0.26f, C(245, 250, 255));
                WizardHat(1.72f + bob, fire ? pc : C(60, 130, 190), fire ? -15 : 10);
                for (int k = 0; k < 3; k++)
                {
                    float ang = t * (fire ? 2.5f : 1.5f) + k * 2.094f;
                    var col = fire ? C(255, 150 + k * 30, 40) : C(180, 240, 255);
                    Orb((float)Math.Cos(ang) * 0.7f, 1.2f + (float)Math.Sin(ang * 2) * 0.15f + bob, (float)Math.Sin(ang) * 0.7f,
                        shiny ? 0.07f : 0.05f, col, 3);
                }
                break;
            }
            case HeroKind.Berserker:
            {
                P(0, 0, 0); Rlgl.Scalef(1, 0.82f, 1);
                Action axe = () =>
                {
                    Box(0.3f, -0.58f, 0, 0.7f, 0.07f, 0.07f, Leather);
                    if (shiny) SetGlow(0.5f);
                    Box(0.62f, -0.45f, 0, 0.2f, 0.32f, 0.04f, tier >= 2 ? C(255, 140, 60) : Steel);
                    SetGlow(0);
                };
                Humanoid(new Rig
                {
                    Body = pc, Legs = Mul(pc, 0.6f), Arms = Skin, Head = Skin, Bob = bob, Bulk = 1.35f,
                    SwingR = Swing(a), SwingL = Swing(Math.Max(0, a - 0.3f)), RightHand = axe, LeftHand = axe,
                });
                Ellip(0.2f, 1.38f + bob, 0, 0.16f, 0.3f, 0.26f, sc);
                Sph(0.02f, 1.62f + bob, 0, 0.26f, Steel);
                P(0, 1.72f + bob, 0.2f); RX(-50); Cone(0, 0, 0, 0.07f, 0.35f, Bone); Pop();
                P(0, 1.72f + bob, -0.2f); RX(50); Cone(0, 0, 0, 0.07f, 0.35f, Bone); Pop();
                Pop();
                break;
            }
            case HeroKind.Necromancer:
            {
                Humanoid(new Rig
                {
                    Body = pc, Arms = pc, Head = C(200, 200, 190), Bob = bob, Robe = true,
                    SwingR = Cast(a, 45, 100), SwingL = Cast(a, 15, 60),
                    RightHand = () =>
                    {
                        Box(0.45f, -0.58f, 0, 1.6f, 0.06f, 0.06f, C(50, 40, 40));
                        Sph(1.3f, -0.58f, 0, 0.14f, Bone);
                        Orb(1.38f, -0.55f, 0.06f, 0.035f, sc, 5);
                        Orb(1.38f, -0.55f, -0.06f, 0.035f, sc, 5);
                    },
                });
                Ellip(-0.06f, 1.6f + bob, 0, 0.27f, 0.3f, 0.3f, Mul(pc, 0.7f));
                P(-0.12f, 1.8f + bob, 0); RZ(40); Cone(0, 0, 0, 0.18f, 0.35f, Mul(pc, 0.7f)); Pop();
                Orb(0.23f, 1.59f + bob, 0.085f, 0.045f, sc, 4);
                Orb(0.23f, 1.59f + bob, -0.085f, 0.045f, sc, 4);
                for (int k = 0; k < 2; k++)
                {
                    float ang = t * 1.2f + k * 3.14f;
                    Sph((float)Math.Cos(ang) * 0.8f, 0.6f + (float)Math.Sin(t * 3 + k) * 0.1f, (float)Math.Sin(ang) * 0.8f, 0.12f, Bone);
                }
                break;
            }
            case HeroKind.Valkyrie:
            {
                float flap = (float)Math.Sin(t * 2) * 12;
                for (int s = -1; s <= 1; s += 2)
                {
                    P(-0.25f, 1.25f + bob, s * 0.15f); RX(-s * (40 + flap)); RY(s * 20);
                    Box(-0.1f, 0, s * 0.45f, 0.35f, 0.05f, 0.9f, C(250, 250, 255));
                    Box(-0.3f, 0, s * 0.6f, 0.3f, 0.04f, 0.7f, C(230, 235, 250));
                    Pop();
                }
                Humanoid(new Rig
                {
                    Body = pc, Legs = Mul(pc, 0.7f), Arms = sc, Head = Skin, Bob = bob, Boots = Mul(pc, 0.6f),
                    SwingR = Cast(a, 30, 120), SwingL = 40,
                    RightHand = () =>
                    {
                        Box(0.4f, -0.58f, 0, 2.0f, 0.05f, 0.05f, Leather);
                        if (shiny) SetGlow(0.6f);
                        P(1.4f, -0.58f, 0); RZ(-90); Cone(0, 0, 0, 0.09f, 0.35f, tier >= 2 ? C(150, 220, 255) : Steel); Pop();
                        SetGlow(0);
                    },
                    LeftHand = () => { P(0.05f, -0.5f, -0.12f); RX(90); Cyl(0, 0, 0, 0.3f, 0.05f, Gold); Pop(); },
                });
                Sph(0.02f, 1.6f + bob, 0, 0.26f, pc);
                Box(-0.05f, 1.72f + bob, 0.27f, 0.25f, 0.2f, 0.03f, C(250, 250, 255));
                Box(-0.05f, 1.72f + bob, -0.27f, 0.25f, 0.2f, 0.03f, C(250, 250, 255));
                Ellip(-0.15f, 1.3f + bob, 0, 0.1f, 0.35f, 0.2f, C(250, 220, 120));
                break;
            }
            case HeroKind.DragonKnight:
            {
                Box(-0.24f, 1.0f + bob, 0, 0.05f, 1.0f, 0.6f, pc);
                Humanoid(new Rig
                {
                    Body = sc, Legs = sc, Arms = sc, Head = sc, Bob = bob, Boots = pc, Bulk = 1.1f,
                    SwingR = Cast(a, 20, 95), SwingL = 30,
                    RightHand = () =>
                    {
                        P(0.2f, -0.58f, 0); RZ(-90);
                        if (shiny) SetGlow(0.35f);
                        Cone(0, 0, 0, 0.14f, 1.8f, tier >= 2 ? C(255, 80, 60) : pc);
                        SetGlow(0);
                        Pop();
                        Cyl(0.15f, -0.63f, 0, 0.16f, 0.1f, Gold);
                    },
                });
                Box(0.28f, 1.52f + bob, 0, 0.2f, 0.14f, 0.2f, sc);
                Orb(0.2f, 1.62f + bob, 0.12f, 0.04f, C(255, 80, 40), 4);
                Orb(0.2f, 1.62f + bob, -0.12f, 0.04f, C(255, 80, 40), 4);
                P(-0.05f, 1.75f + bob, 0.15f); RZ(70); RX(-20); Cone(0, 0, 0, 0.07f, 0.45f, pc); Pop();
                P(-0.05f, 1.75f + bob, -0.15f); RZ(70); RX(20); Cone(0, 0, 0, 0.07f, 0.45f, pc); Pop();
                for (int s = -1; s <= 1; s += 2)
                {
                    P(-0.25f, 1.3f + bob, s * 0.2f); RX(-s * (30 + (float)Math.Sin(t * 3) * 10));
                    Box(-0.15f, 0, s * 0.35f, 0.4f, 0.04f, 0.7f, pc);
                    Pop();
                }
                break;
            }
            case HeroKind.Titan:
            {
                S(1.55f);
                Humanoid(new Rig
                {
                    Body = pc, Legs = Mul(pc, 0.75f), Arms = pc, Head = pc, Bob = bob * 0.5f, Bulk = 1.4f,
                    SwingR = Swing(a, 20, 160, -20), SwingL = 20,
                    RightHand = () =>
                    {
                        Box(0.2f, -0.58f, 0, 0.8f, 0.07f, 0.07f, Leather);
                        Box(0.6f, -0.58f, 0, 0.3f, 0.45f, 0.3f, C(120, 130, 150));
                        if (shiny) GlowBox(0.6f, -0.58f, 0, 0.32f, 0.08f, 0.32f, sc);
                    },
                });
                Orb(0.22f, 1.6f, 0.08f, 0.05f, sc, 4);
                Orb(0.22f, 1.6f, -0.08f, 0.05f, sc, 4);
                GlowBox(0.2f * 1.4f, 1.1f, 0, 0.03f, 0.25f, 0.08f, sc);
                GlowBox(0.2f * 1.4f, 1.1f, 0, 0.03f, 0.08f, 0.25f, sc);
                Ellip(0.05f, 1.35f, 0, 0.25f, 0.18f, 0.35f, C(230, 235, 245));
                if ((int)(t * 8) % 3 == 0)
                {
                    SetGlow(1);
                    Box((float)Math.Sin(t * 37) * 0.5f, 2.0f, (float)Math.Cos(t * 23) * 0.5f, 0.04f, 0.4f, 0.04f, sc);
                    SetGlow(0);
                }
                break;
            }
            case HeroKind.VoidLich:
            {
                float hover = 0.35f + (float)Math.Sin(t * 1.7f) * 0.12f;
                P(0, hover, 0);
                P(0, 1.2f, 0); RX(180); Cone(0, 0, 0, 0.45f, 1.1f, pc); Pop();
                Humanoid(new Rig
                {
                    Body = pc, Arms = C(210, 205, 190), Head = Bone, Bob = 0, NoLegs = true,
                    SwingR = Cast(a, 50, 110), SwingL = Cast(a, 50, 110),
                    RightHand = () => Orb(0.1f, -0.7f, 0, 0.1f, sc, 3),
                    LeftHand = () => Orb(0.1f, -0.7f, 0, 0.1f, sc, 3),
                });
                Orb(0.22f, 1.6f, 0.085f, 0.05f, sc, 5);
                Orb(0.22f, 1.6f, -0.085f, 0.05f, sc, 5);
                for (int k = 0; k < 6; k++)
                {
                    float ang = k * 60 * (float)Math.PI / 180;
                    P((float)Math.Cos(ang) * 0.2f, 1.8f, (float)Math.Sin(ang) * 0.2f);
                    Cone(0, 0, 0, 0.05f, 0.2f, Gold);
                    Pop();
                }
                Cyl(0, 1.76f, 0, 0.22f, 0.06f, Gold);
                for (int k = 0; k < 3; k++)
                {
                    float ang = t * 1.4f + k * 2.094f;
                    Orb((float)Math.Cos(ang) * 1.0f, 1.4f + (float)Math.Sin(ang * 1.5f) * 0.3f, (float)Math.Sin(ang) * 1.0f, 0.1f, C(150, 50, 230), 3);
                }
                Pop();
                break;
            }
            case HeroKind.Phoenix:
            {
                float fly = 1.6f + (float)Math.Sin(t * 2.5f) * 0.2f;
                float flap = (float)Math.Sin(t * 7) * 35;
                P(0, fly, 0);
                if (a > 0) RZ(-15 * a);
                SetGlow(0.35f);
                Ellip(0, 0, 0, 0.45f, 0.3f, 0.3f, pc);
                Sph(0.42f, 0.28f, 0, 0.2f, sc);
                SetGlow(0);
                P(0.6f, 0.26f, 0); RZ(-90); Cone(0, 0, 0, 0.07f, 0.22f, Gold); Pop();
                Sph(0.52f, 0.34f, 0.12f, 0.035f, Dark);
                Sph(0.52f, 0.34f, -0.12f, 0.035f, Dark);
                for (int s = -1; s <= 1; s += 2)
                {
                    P(0, 0.1f, s * 0.2f); RX(-s * flap);
                    SetGlow(0.5f);
                    Box(0, 0, s * 0.55f, 0.45f, 0.05f, 1.1f, pc);
                    Box(-0.2f, 0, s * 0.9f, 0.4f, 0.04f, 0.6f, sc);
                    SetGlow(0);
                    Pop();
                }
                for (int k = -1; k <= 1; k++)
                {
                    P(-0.4f, 0, k * 0.1f); RY(k * 15); RZ(-10 + (float)Math.Sin(t * 3 + k) * 8);
                    SetGlow(0.6f); Box(-0.5f, 0, 0, 1.0f, 0.04f, 0.1f, k == 0 ? sc : pc); SetGlow(0);
                    Orb(-1.0f, 0, 0, 0.06f, C(255, 240, 150), 3);
                    Pop();
                }
                Pop();
                break;
            }
            case HeroKind.Chronomancer:
            {
                P(-0.35f, 1.4f + bob, 0); RY(90);
                RZ(t * 20);
                SetGlow(0.7f); Torus(0, 0, 0, 0.55f, Gold); SetGlow(0);
                Box(0, 0.18f, 0, 0.03f, 0.36f, 0.03f, Dark);
                Pop();
                P(-0.35f, 1.4f + bob, 0); RY(90); RZ(-t * 120);
                Box(0, 0.25f, 0, 0.025f, 0.5f, 0.025f, Dark);
                Pop();
                Humanoid(new Rig
                {
                    Body = pc, Arms = pc, Head = Skin, Bob = bob, Robe = true,
                    SwingR = Cast(a, 60, 115), SwingL = 20,
                    RightHand = () =>
                    {
                        Box(0.4f, -0.58f, 0, 1.5f, 0.05f, 0.05f, Gold);
                        P(1.25f, -0.58f, 0); RZ(-90 + t * 40);
                        Cone(0, 0, 0, 0.12f, 0.16f, Gold);
                        P(0, 0.32f, 0); RX(180); Cone(0, 0, 0, 0.12f, 0.16f, Gold); Pop();
                        Pop();
                        Orb(1.25f, -0.58f, 0, 0.06f, sc, 4);
                    },
                });
                Ellip(-0.05f, 1.6f + bob, 0, 0.26f, 0.28f, 0.28f, Mul(pc, 0.8f));
                break;
            }
        }
        Pop();
    }

    public static void DrawTamer(Game g, float t)
    {
        var p = Game.TamerPos;
        float a = g.TamerAnim;
        float pop = 1 + 0.15f * g.TamerLevelFx * (float)Math.Abs(Math.Sin(g.TamerLevelFx * 12));
        Shadow(p.X, p.Z, 0.5f);
        P(p); RY(-30); S(1.05f * pop);
        var cape = C(120, 50, 150);
        Box(-0.24f, 1.0f, 0, 0.05f, 0.95f, 0.6f, cape);
        Humanoid(new Rig
        {
            Body = C(90, 60, 110), Legs = C(70, 55, 45), Arms = C(90, 60, 110), Head = Skin, Boots = Leather,
            Bob = (float)Math.Sin(t * 2) * 0.03f,
            SwingR = 20 + a * 90, SwingL = 20,
            RightHand = () =>
            {
                Box(0.25f, -0.58f, 0, 0.5f, 0.06f, 0.06f, Leather);
                for (int k = 0; k < 6; k++)
                {
                    float wav = (float)Math.Sin(t * 8 + k) * 0.05f * (1 - a);
                    Box(0.55f + k * 0.18f, -0.58f + wav - k * 0.03f * (1 - a), 0, 0.2f, 0.035f, 0.035f, C(90, 50, 30));
                }
                if (a > 0.5f) Orb(1.6f, -0.6f, 0, 0.08f, C(255, 240, 150), 4);
            },
        });
        Cyl(0, 1.72f, 0, 0.38f, 0.04f, Leather);
        Cyl(0, 1.74f, 0, 0.22f, 0.2f, Leather);
        P(-0.1f, 1.85f, 0.18f); RZ(60); Box(0, 0.2f, 0, 0.04f, 0.4f, 0.08f, C(230, 60, 60)); Pop();
        // baby dragon on the shoulder
        P(-0.05f, 1.45f, -0.35f); S(0.2f); RY(-10);
        DrawDragon(C(230, 90, 50), C(255, 200, 120), C(200, 60, 40), t * 1.5f, g.TamerAnim, 0);
        Pop();
        Pop();
    }

    // ---------- dragons ----------
    public static void DrawDragon(Color main, Color belly, Color wing, float t, float mouth, float flash)
    {
        float flap = 25 + (float)Math.Sin(t * 4) * 35;
        // tail
        for (int k = 0; k < 9; k++)
        {
            float f = k / 8f;
            float x = -0.9f - k * 0.28f;
            float y = 1.15f - k * 0.05f + (float)Math.Sin(t * 2 + k * 0.6f) * 0.08f * f;
            float z = (float)Math.Sin(t * 1.6f + k * 0.5f) * 0.25f * f;
            Sph(x, y, z, 0.34f * (1 - f * 0.8f), main);
            if (k % 2 == 0) { P(x, y + 0.25f * (1 - f * 0.8f), z); Cone(0, 0, 0, 0.07f * (1 - f * 0.6f), 0.2f, belly); Pop(); }
            if (k == 8) { P(x - 0.15f, y, z); RZ(90); Cone(0, 0, 0, 0.15f, 0.35f, wing); Pop(); }
        }
        Ellip(0, 1.2f, 0, 1.0f, 0.58f, 0.62f, main);
        Ellip(0.1f, 1.02f, 0, 0.85f, 0.42f, 0.52f, belly);
        for (int k = 0; k < 4; k++) { P(0.5f - k * 0.35f, 1.72f, 0); Cone(0, 0, 0, 0.1f, 0.28f, belly); Pop(); }
        // legs
        for (int s = -1; s <= 1; s += 2)
        {
            Box(0.5f, 0.45f, s * 0.38f, 0.26f, 0.7f, 0.24f, main);
            Box(-0.5f, 0.45f, s * 0.38f, 0.3f, 0.7f, 0.28f, main);
            Box(0.58f, 0.08f, s * 0.38f, 0.38f, 0.14f, 0.3f, Mul(main, 0.7f));
            Box(-0.42f, 0.08f, s * 0.38f, 0.38f, 0.14f, 0.32f, Mul(main, 0.7f));
        }
        // neck
        for (int k = 0; k < 5; k++)
        {
            float f = k / 4f;
            float nx = 0.8f + f * 0.6f + (float)Math.Sin(t * 1.3f) * 0.05f * f;
            float ny = 1.45f + f * 0.9f;
            Sph(nx, ny, 0, 0.3f - f * 0.08f, main);
        }
        // head
        P(1.55f, 2.45f, 0); RZ(-8 + (float)Math.Sin(t * 1.3f) * 4);
        Box(0, 0, 0, 0.55f, 0.36f, 0.42f, main);
        Box(0.42f, -0.02f, 0, 0.45f, 0.22f, 0.32f, main);
        P(0.2f, -0.12f, 0); RZ(-mouth * 30);
        Box(0.28f, -0.06f, 0, 0.5f, 0.1f, 0.28f, belly);
        Pop();
        if (mouth > 0.1f) Orb(0.65f, -0.12f, 0, 0.1f * mouth, C(255, 200, 80), 4);
        Orb(0.18f, 0.12f, 0.2f, 0.06f, C(255, 230, 60), 3);
        Orb(0.18f, 0.12f, -0.2f, 0.06f, C(255, 230, 60), 3);
        for (int s = -1; s <= 1; s += 2)
        {
            P(-0.15f, 0.2f, s * 0.14f); RZ(115); RX(s * -15); Cone(0, 0, 0, 0.08f, 0.55f, Bone); Pop();
        }
        Pop();
        // wings
        for (int s = -1; s <= 1; s += 2)
        {
            P(0.25f, 1.6f, s * 0.45f); RX(-s * flap);
            Box(0, 0, s * 0.8f, 0.14f, 0.12f, 1.6f, main);
            Box(-0.55f, -0.02f, s * 0.8f, 1.0f, 0.04f, 1.5f, wing);
            P(0, 0, s * 1.6f); RX(-s * flap * 0.5f);
            Box(0, 0, s * 0.6f, 0.1f, 0.1f, 1.2f, main);
            Box(-0.45f, -0.02f, s * 0.55f, 0.85f, 0.04f, 1.1f, wing);
            Pop();
            Pop();
        }
    }

    public static Vector3 DragonPetPos(Game g, int i, float t)
    {
        float ang = t * 0.3f + i * (MathF.PI * 2 / 6);
        float r = 5.2f + (i % 2) * 1.3f;
        return new Vector3(-2.8f + MathF.Cos(ang) * r, 5.0f + (i % 3) * 0.7f + MathF.Sin(t * 1.3f + i) * 0.4f, -1.5f + MathF.Sin(ang) * r * 0.45f);
    }

    public static void DrawPetDragons(Game g, float t)
    {
        for (int i = 0; i < g.DragonLevels.Length; i++)
        {
            int lv = g.DragonLevels[i];
            if (lv <= 0) continue;
            var p = DragonPetPos(g, i, t);
            var p2 = DragonPetPos(g, i, t + 0.05f);
            var d = p2 - p;
            float yaw = MathF.Atan2(-d.Z, d.X) * 180 / MathF.PI;
            float size = 0.3f + Math.Min(0.45f, (float)Math.Log2(lv + 1) * 0.06f);
            var col = Defs.Dragons[i].Color;
            float breath = g.DragonBreath[i] > 3.2f ? 1 : 0;
            P(p); RY(yaw); RZ(d.Y * 60); S(size); Rlgl.Translatef(0, -1.2f, 0);
            if (i == 5) SetGlow(0.3f);
            DrawDragon(col, Lerp(col, C(255, 240, 200), 0.5f), Mul(col, 0.65f), t * 1.2f + i, breath, 0);
            SetGlow(0);
            Pop();
        }
    }

    // ---------- enemies ----------
    public static void DrawEnemy(Game g, float t)
    {
        var e = g.Enemy;
        if (e.MaxHp.IsZero) return;
        if (!e.Alive && e.Dying <= 0) return;
        var p = Game.EnemyPos;
        float spawn = 1 - e.Spawn;
        float ease = spawn < 1 ? 1 - (1 - spawn) * (1 - spawn) * (1 + 1.5f * spawn) : 1;
        float dieK = e.Alive ? 1 : e.Dying;
        float sc = 1.35f * e.Scale * Math.Max(0.01f, ease) * (e.Alive ? 1 : 0.4f + 0.6f * dieK);
        float squash = 1 + (float)Math.Sin(e.Wobble * 20) * 0.12f * e.Wobble;

        Shadow(p.X, p.Z, 0.6f * sc * (e.IsDragon ? 2.2f : 1));
        P(p);
        if (!e.Alive) { Rlgl.Translatef(0, -(1 - dieK) * 0.8f, 0); RY((1 - dieK) * 360); }
        RY(e.IsDragon ? 215 : 235);
        Rlgl.Scalef(sc / squash, sc * squash, sc / squash);
        SetFlash(e.HitFlash > 0 ? 0.75f : (!e.Alive ? 0.5f : 0));
        var tint = e.Tint;
        float bob = (float)Math.Sin(t * 3) * 0.04f;
        float headTop = 1.8f;
        switch (e.IsDragon ? (EnemyKind)(-1) : e.Kind)
        {
            case (EnemyKind)(-1):
                P(-0.6f, 0, 0);
                DrawDragon(tint, Lerp(tint, C(255, 230, 190), 0.5f), Mul(tint, 0.6f), t, 0.5f + 0.5f * (float)Math.Sin(t * 2), 0);
                Pop();
                headTop = 2.9f;
                break;
            case EnemyKind.Slime:
            {
                float wob = (float)Math.Sin(t * 5) * 0.08f;
                Ellip(0, 0.45f, 0, 0.6f + wob, 0.45f - wob, 0.6f + wob, Lerp(tint, C(80, 220, 120), 0.5f));
                Sph(0.45f, 0.55f, 0.18f, 0.09f, Color.White); Sph(0.52f, 0.55f, 0.18f, 0.05f, Dark);
                Sph(0.45f, 0.55f, -0.18f, 0.09f, Color.White); Sph(0.52f, 0.55f, -0.18f, 0.05f, Dark);
                headTop = 1.0f;
                break;
            }
            case EnemyKind.Goblin:
            case EnemyKind.Imp:
            {
                bool imp = e.Kind == EnemyKind.Imp;
                var skin = imp ? C(200, 50, 40) : C(110, 170, 70);
                P(0, 0, 0); Rlgl.Scalef(0.8f, 0.75f, 0.8f);
                if (imp)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        P(-0.2f, 1.3f, s * 0.2f); RX(-s * (40 + (float)Math.Sin(t * 10) * 20));
                        Box(-0.1f, 0, s * 0.35f, 0.4f, 0.03f, 0.7f, C(120, 30, 30)); Pop();
                    }
                Humanoid(new Rig
                {
                    Body = imp ? C(120, 30, 30) : C(120, 90, 60), Legs = skin, Arms = skin, Head = skin, Bob = bob,
                    SwingR = 30 + (float)Math.Sin(t * 4) * 20, SwingL = 10,
                    RightHand = () => Box(0.3f, -0.58f, 0, 0.5f, 0.06f, 0.03f, imp ? C(255, 200, 60) : Steel),
                });
                if (imp)
                {
                    P(0, 1.75f, 0.12f); RX(-20); Cone(0, 0, 0, 0.06f, 0.25f, Dark); Pop();
                    P(0, 1.75f, -0.12f); RX(20); Cone(0, 0, 0, 0.06f, 0.25f, Dark); Pop();
                    Orb(0.22f, 1.6f, 0.085f, 0.04f, C(255, 220, 60), 4);
                    Orb(0.22f, 1.6f, -0.085f, 0.04f, C(255, 220, 60), 4);
                }
                else
                {
                    P(0, 1.6f, 0.22f); RX(-80); Cone(0, 0, 0, 0.07f, 0.3f, skin); Pop();
                    P(0, 1.6f, -0.22f); RX(80); Cone(0, 0, 0, 0.07f, 0.3f, skin); Pop();
                    Ellip(0.25f, 1.52f, 0, 0.08f, 0.06f, 0.06f, Mul(skin, 0.8f));
                }
                Pop();
                headTop = 1.5f;
                break;
            }
            case EnemyKind.Skeleton:
            {
                Humanoid(new Rig
                {
                    Body = Bone, Legs = Bone, Arms = Bone, Head = Bone, Bob = bob, Bulk = 0.7f,
                    SwingR = 30 + (float)Math.Sin(t * 3) * 25, SwingL = 10,
                    RightHand = () => Sword(C(150, 150, 160), false),
                });
                for (int k = 0; k < 3; k++) Box(0.14f, 0.9f + k * 0.15f, 0, 0.1f, 0.04f, 0.45f, Dark);
                Orb(0.23f, 1.6f, 0.085f, 0.05f, tint, 4);
                Orb(0.23f, 1.6f, -0.085f, 0.05f, tint, 4);
                break;
            }
            case EnemyKind.Mushroom:
            {
                Cyl(0, 0, 0, 0.3f, 0.9f, C(240, 230, 210));
                Ellip(0, 0.95f, 0, 0.75f, 0.45f, 0.75f, Lerp(C(220, 50, 50), tint, 0.3f));
                for (int k = 0; k < 6; k++)
                {
                    float ang = k * 1.047f + 0.3f;
                    Sph(MathF.Cos(ang) * 0.45f, 1.18f, MathF.Sin(ang) * 0.45f, 0.1f, Color.White);
                }
                Sph(0.28f, 0.6f, 0.1f, 0.06f, Dark); Sph(0.28f, 0.6f, -0.1f, 0.06f, Dark);
                Box(0.29f, 0.45f, 0, 0.02f, 0.04f, 0.14f, Dark);
                headTop = 1.5f;
                break;
            }
            case EnemyKind.Orc:
            {
                var skin = C(90, 140, 70);
                Humanoid(new Rig
                {
                    Body = C(100, 70, 50), Legs = C(70, 60, 50), Arms = skin, Head = skin, Bob = bob, Bulk = 1.5f,
                    SwingR = 40 + (float)Math.Sin(t * 2.5f) * 30, SwingL = 15,
                    RightHand = () =>
                    {
                        Box(0.35f, -0.58f, 0, 0.8f, 0.1f, 0.1f, Leather);
                        Ellip(0.75f, -0.58f, 0, 0.22f, 0.18f, 0.18f, C(90, 60, 40));
                    },
                });
                P(0.22f, 1.45f, 0.1f); Cone(0, 0, 0, 0.04f, 0.15f, Bone); Pop();
                P(0.22f, 1.45f, -0.1f); Cone(0, 0, 0, 0.04f, 0.15f, Bone); Pop();
                break;
            }
            case EnemyKind.Wraith:
            {
                float hover = 0.3f + (float)Math.Sin(t * 2) * 0.15f;
                P(0, hover, 0);
                P(0, 1.3f, 0); RX(180); Cone(0, 0, 0, 0.5f, 1.3f, C(40, 40, 60, 230)); Pop();
                Ellip(0, 1.55f, 0, 0.28f, 0.32f, 0.28f, C(30, 30, 45));
                Orb(0.22f, 1.55f, 0.09f, 0.05f, tint, 5);
                Orb(0.22f, 1.55f, -0.09f, 0.05f, tint, 5);
                for (int s = -1; s <= 1; s += 2)
                {
                    P(0, 1.25f, s * 0.35f); RZ(60 + (float)Math.Sin(t * 3 + s) * 20);
                    Box(0, -0.35f, 0, 0.12f, 0.7f, 0.12f, C(40, 40, 60));
                    Pop();
                }
                Pop();
                headTop = 2.1f;
                break;
            }
            case EnemyKind.Golem:
            {
                var rock = Lerp(C(130, 120, 110), tint, 0.2f);
                Box(0, 0.4f, 0.28f, 0.35f, 0.8f, 0.35f, rock);
                Box(0, 0.4f, -0.28f, 0.35f, 0.8f, 0.35f, rock);
                Box(0, 1.2f, 0, 0.8f, 0.85f, 1.0f, rock);
                Box(0.05f, 1.8f, 0, 0.45f, 0.4f, 0.45f, Mul(rock, 0.9f));
                Orb(0.41f, 1.2f, 0, 0.12f, tint, 3);
                Orb(0.28f, 1.85f, 0.1f, 0.045f, tint, 4);
                Orb(0.28f, 1.85f, -0.1f, 0.045f, tint, 4);
                for (int s = -1; s <= 1; s += 2)
                {
                    P(0, 1.45f, s * 0.65f); RZ(20 + (float)Math.Sin(t * 2) * 15);
                    Box(0, -0.4f, 0, 0.32f, 0.9f, 0.32f, Mul(rock, 0.85f));
                    Box(0, -0.9f, 0, 0.42f, 0.35f, 0.42f, rock);
                    Pop();
                }
                headTop = 2.2f;
                break;
            }
            case EnemyKind.Beholder:
            {
                float hover = 0.6f + (float)Math.Sin(t * 1.5f) * 0.15f;
                P(0, hover, 0);
                Sph(0, 0.8f, 0, 0.7f, Lerp(C(150, 80, 160), tint, 0.3f));
                Sph(0.5f, 0.85f, 0, 0.35f, Color.White);
                Sph(0.78f, 0.85f, 0, 0.14f, tint);
                Sph(0.86f, 0.85f, 0, 0.07f, Dark);
                Box(0.55f, 0.45f, 0, 0.1f, 0.08f, 0.6f, Dark);
                for (int k = 0; k < 6; k++)
                {
                    float ang = k * 1.047f;
                    P(-0.1f, 1.3f, 0); RX(MathF.Cos(ang) * 40); RZ(MathF.Sin(ang) * 30 + (float)Math.Sin(t * 3 + k) * 10);
                    Box(0, 0.35f, 0, 0.06f, 0.7f, 0.06f, C(130, 70, 140));
                    Sph(0, 0.75f, 0, 0.09f, Color.White);
                    Sph(0.07f, 0.75f, 0, 0.04f, Dark);
                    Pop();
                }
                Pop();
                headTop = 2.6f;
                break;
            }
            case EnemyKind.Treant:
            {
                var bark = C(110, 75, 45);
                Cyl(0, 0, 0, 0.42f, 1.6f, bark);
                for (int s = -1; s <= 1; s += 2)
                {
                    P(0, 1.3f, s * 0.35f); RX(-s * (50 + (float)Math.Sin(t * 1.5f) * 10));
                    Box(0, 0.35f, 0, 0.14f, 0.8f, 0.14f, bark);
                    Sph(0, 0.85f, 0, 0.25f, Lerp(C(60, 140, 60), tint, 0.3f));
                    Pop();
                }
                Sph(0, 1.9f, 0, 0.65f, Lerp(C(60, 140, 60), tint, 0.3f));
                Sph(0.3f, 2.2f, 0.3f, 0.4f, Lerp(C(70, 160, 70), tint, 0.3f));
                Sph(-0.2f, 2.25f, -0.3f, 0.45f, Lerp(C(50, 120, 50), tint, 0.3f));
                Orb(0.42f, 1.2f, 0.12f, 0.05f, C(255, 230, 80), 4);
                Orb(0.42f, 1.2f, -0.12f, 0.05f, C(255, 230, 80), 4);
                Box(0.42f, 0.95f, 0, 0.05f, 0.08f, 0.25f, Dark);
                headTop = 2.7f;
                break;
            }
        }
        SetFlash(0);
        if (e.IsBoss && !e.IsDragon)
        {
            P(0, headTop, 0); RY(t * 40);
            Cyl(0, 0, 0, 0.28f, 0.14f, Gold);
            for (int k = 0; k < 5; k++)
            {
                float ang = k * 72 * MathF.PI / 180;
                Cone(MathF.Cos(ang) * 0.22f, 0.12f, MathF.Sin(ang) * 0.22f, 0.07f, 0.2f, Gold);
            }
            Orb(0.28f, 0.07f, 0, 0.05f, C(255, 50, 80), 3);
            Pop();
        }
        Pop();
    }

    // ---------- world ----------
    record struct Prop(int Kind, Vector3 Pos, float Scale, float Rot);
    static int propBiome = -1;
    static readonly List<Prop> props = new();

    static void BuildProps(int biome)
    {
        propBiome = biome;
        props.Clear();
        var rng = new Random(biome * 7919 + 13);
        for (int k = 0; k < 46; k++)
        {
            float ang = (float)(rng.NextDouble() * Math.PI * 2);
            float r = 11 + (float)rng.NextDouble() * 16;
            var pos = new Vector3(-2 + MathF.Cos(ang) * r, 0, -4 + MathF.Sin(ang) * r * 0.8f);
            if (pos.Z > 4) pos.Z = -pos.Z * 0.7f - 6;
            props.Add(new Prop(rng.Next(3), pos, 0.7f + (float)rng.NextDouble() * 0.9f, (float)rng.NextDouble() * 360));
        }
    }

    public static void DrawWorld(int biome, int cycle, float t)
    {
        if (biome != propBiome) BuildProps(biome);
        var b = Defs.Biomes[biome];
        Cyl(-2, -0.3f, -4, 40, 0.3f, b.Ground);
        Cyl(-1.5f, -0.02f, 0.5f, 8.5f, 0.03f, Mul(b.Ground, 1.12f));
        for (int k = 0; k < 24; k++)
        {
            float ang = k * MathF.PI * 2 / 24;
            Box(-1.5f + MathF.Cos(ang) * 8.5f, 0.02f, 0.5f + MathF.Sin(ang) * 8.5f, 0.5f, 0.12f, 0.5f, Mul(b.Ground, 0.8f));
        }
        foreach (var pr in props)
        {
            P(pr.Pos); RY(pr.Rot); S(pr.Scale);
            DrawProp(biome, pr.Kind, t, b);
            Pop();
        }
        if (biome == 2)
            for (int k = 0; k < 5; k++)
            {
                SetGlow(1);
                Cyl(-9 + k * 4.5f, -0.01f, -8 - (k % 2) * 4, 1.5f + k % 2, 0.03f, C(255, 110, 20));
                SetGlow(0);
            }
    }

    static void DrawProp(int biome, int kind, float t, Biome b)
    {
        switch (biome)
        {
            case 0: // meadow: round trees, flowers, rocks
            case 1: // woods: pines
                if (kind < 2)
                {
                    Cyl(0, 0, 0, 0.25f, 1.4f, C(100, 70, 40));
                    if (biome == 0) { Sph(0, 2.0f, 0, 1.0f, b.Prop); Sph(0.4f, 2.5f, 0.2f, 0.7f, Mul(b.Prop, 1.15f)); }
                    else for (int k = 0; k < 3; k++) Cone(0, 1.0f + k * 0.8f, 0, 1.2f - k * 0.3f, 1.4f, Mul(b.Prop, 1 + k * 0.1f));
                }
                else Ellip(0, 0.2f, 0, 0.6f, 0.4f, 0.5f, C(130, 130, 125));
                break;
            case 2: // badlands: rocks and spires
                if (kind == 0) { Cone(0, 0, 0, 0.8f, 3.5f, b.Prop); }
                else Ellip(0, 0.3f, 0, 1.0f, 0.6f, 0.8f, Mul(b.Prop, 1.3f));
                if (kind == 1) Orb(0, 0.8f, 0, 0.12f, C(255, 120, 30), 5);
                break;
            case 3: // frost: ice spikes and snowy pines
                if (kind == 0) { SetGlow(0.25f); Cone(0, 0, 0, 0.6f, 3.0f, C(170, 220, 250)); SetGlow(0); }
                else if (kind == 1) { Cyl(0, 0, 0, 0.2f, 1f, C(90, 70, 50)); Cone(0, 0.8f, 0, 1.1f, 2.4f, C(60, 110, 90)); Cone(0, 2.0f, 0, 0.6f, 1.2f, C(245, 250, 255)); }
                else Ellip(0, 0.25f, 0, 0.9f, 0.5f, 0.7f, C(240, 245, 255));
                break;
            case 4: // crystals
                SetGlow(0.55f);
                P(0, 0, 0); RZ(15); Box(0, 1.2f, 0, 0.5f, 2.4f, 0.5f, kind == 0 ? b.Prop : b.Accent); Pop();
                P(0.4f, 0, 0.2f); RZ(-25); Box(0, 0.7f, 0, 0.35f, 1.4f, 0.35f, b.Prop); Pop();
                SetGlow(0);
                break;
            case 5: // void obelisks
                Box(0, 1.8f, 0, 0.7f, 3.6f, 0.7f, C(20, 15, 30));
                GlowBox(0, 1.8f + MathF.Sin(t + kind) * 1.2f, 0, 0.75f, 0.12f, 0.75f, b.Prop);
                if (kind == 1) Orb(0, 4.2f + MathF.Sin(t * 2) * 0.2f, 0, 0.25f, b.Accent, 3);
                break;
            default: // celestial pillars
                Cyl(0, 0, 0, 0.45f, 3.5f, b.Prop);
                Box(0, 3.6f, 0, 1.1f, 0.25f, 1.1f, b.Prop);
                if (kind == 0) Orb(0, 4.3f, 0, 0.3f, b.Accent, 3);
                break;
        }
    }

    /// <summary>Additive halos for every emissive orb drawn this frame.</summary>
    public static void DrawGlowPass()
    {
        Rlgl.DisableDepthMask();
        Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (var gl in Glows)
        {
            var c = gl.Color;
            Raylib.DrawSphereEx(gl.Pos, gl.Radius, 6, 8, new Color(c.R, c.G, c.B, (byte)40));
            Raylib.DrawSphereEx(gl.Pos, gl.Radius * 0.55f, 6, 8, new Color(c.R, c.G, c.B, (byte)55));
        }
        Raylib.EndBlendMode();
        Rlgl.EnableDepthMask();
    }

    public static void DrawHeroAuras(Game g, float t)
    {
        Rlgl.DisableDepthMask();
        Raylib.BeginBlendMode(BlendMode.Additive);
        for (int i = 0; i < g.HeroLevels.Length; i++)
        {
            int lv = g.HeroLevels[i];
            int tier = lv >= 1000 ? 4 : lv >= 500 ? 3 : lv >= 100 ? 2 : lv >= 25 ? 1 : 0;
            if (tier == 0) continue;
            var p = Game.HeroPos(i);
            var c = Defs.Heroes[i].Secondary;
            for (int k = 0; k < tier; k++)
            {
                float r = 0.55f + k * 0.18f + MathF.Sin(t * 3 + k) * 0.04f;
                Raylib.DrawCylinder(p + new Vector3(0, 0.03f + k * 0.01f, 0), r, r, 0.02f, 24, new Color(c.R, c.G, c.B, (byte)(50 + k * 15)));
            }
            if (tier >= 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    float y = (t * 0.8f + k / 3f) % 1f;
                    Raylib.DrawCylinderWires(p + new Vector3(0, y * 2.2f, 0), 0.6f * (1 - y * 0.5f), 0.6f * (1 - y * 0.5f), 0.01f, 16,
                        new Color(c.R, c.G, c.B, (byte)(120 * (1 - y))));
                }
            }
        }
        Raylib.EndBlendMode();
        Rlgl.EnableDepthMask();
    }
}
