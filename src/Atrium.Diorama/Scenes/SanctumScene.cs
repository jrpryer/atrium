using System.Numerics;
using Atrium.Core.Content;
using Atrium.Core.Locales;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Overlays;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// The Sanctum: a candlelit round study, the linchpin. Six stations, each a hotspot that opens a parchment working
/// card on the desk side of the screen: the designation desk (with the journal and the specimen board), the
/// magnifying glass on its stand, the assembly bench, the Reconstitution Circle chalked on the floor, the Balance over
/// its reliquary (one per seal) and the cedar cabinet. Every rule is Core's (<see cref="SanctumLocale"/>); this file
/// only stages it. The cards live in SanctumCards.cs and the furniture meshes in SanctumProps.cs.
/// </summary>
public sealed partial class SanctumScene : InteriorScene
{
    protected override string LocaleId => "sanctum";
    public override Color Backdrop => Palette.Hex(0x2E2622);

    // ───── where things stand (floor y = 0; the back wall's face is z = -5.4, the left wall's x = -5.4) ─────
    private static readonly Vector3 DeskPos = new(-2.75f, 0, -4.15f);
    private static readonly Vector3 GlassPos = new(-4.35f, 0, -1.25f);
    private static readonly Vector3 BenchPos = new(1.55f, 0, -4.85f);
    private static readonly Vector3 CabinetPos = new(4.4f, 0, -4.9f);
    private static readonly Vector3 CirclePos = new(-0.2f, 0, 0.6f);
    private static readonly Vector3 BalancePos = new(3.85f, 0, -1.7f);
    private const float CircleR = 1.75f;
    /// <summary>Where the journal lies open on the desk (its top surface).</summary>
    private static readonly Vector3 JournalAt = DeskPos + new Vector3(-0.05f, 1.06f, 0.12f);
    private static readonly Vector3 LensPivot = GlassPos + new Vector3(0, 1.52f, -0.02f);

    private static Vector3 SealPos(int i) => BalancePos + new Vector3(0, 0, 2.5f * i);
    private static Vector3 BeamPivot(int i) => SealPos(i) + new Vector3(0, 2.46f, 0);
    private const float BeamHalf = 0.84f;
    /// <summary>The beam rests tilted (the counterweight side down) while its seal is shut, and level once it opens.</summary>
    private const float ShutTilt = -0.17f;

    private static readonly Color Brass = Palette.Hex(0xC9A24A), BrassDark = Palette.Hex(0x8E6E2E), Chalk = Palette.Hex(0xEEE8D8),
        WaxRed = Palette.Hex(0x9C3A2E), CandleWax = Palette.Hex(0xF1E6CC), Walnut = Palette.Hex(0x6E4B33), Cedar = Palette.Hex(0x8A4E2E),
        Iron = Palette.Hex(0x4F4A52), StoneCol = Palette.Hex(0xA79A86), Blueprint = Palette.Hex(0xCFDCE6);

    // ───── models ─────
    private Part _decor, _oculusGlass, _desk, _journal, _board, _glassTable, _lensRing, _lensGlass, _bench, _sheet;
    private Part _cabinet, _plinth, _relicDoor, _stand, _beam, _pan, _circle, _circleCandles;
    private Model _tag, _swatch, _wax, _weight, _orb, _rock, _crystal, _jar, _box;

    // ───── animation state ─────
    private float _t;
    private Vector3 _home;
    private float _homeDist;
    private Spring _lens = new(0, 70, 4.5f);
    private float _gust;            // a not-yet breath across the desk candles (1 → 0)
    private float _flutter = -1;    // near-miss: the journal's pages lift
    private float _sealT = -1;      // a resolution stamps wax on the journal
    private float _circleFx = -1;   // the Circle drawing itself in light (0 → 1)
    private string? _restoredDef;   // what rose out of the Circle, for a moment
    private float _restoredT = -1;
    private float _benchFx = -1;
    private string? _glassItem;     // last thing set under the glass
    private float _dustTimer, _smokeTimer, _moteTimer;
    private readonly Dictionary<string, SealAnim> _seals = new();
    private readonly List<(float At, Action Act)> _later = new();

    private sealed class SealAnim
    {
        public Spring Beam = new(ShutTilt, 30, 2.4f);
        public Spring Door = new(0, 55, 8);
        public string? PanDef;
        public bool PanInert;
        public float PanT = -1;
    }

    private sealed record Choice(Action Do);

    // ───────────────────────── building ─────────────────────────

    protected override void Build()
    {
        Model Mk(MeshBuilder b) => Own(b.Model(R.Shaders.Toon));
        _decor = Prop("decor", BuildDecor);
        _oculusGlass = Prop("oculus-glass", BuildOculusGlass);
        _desk = Prop("desk", BuildDesk);
        _journal = Prop("journal", BuildJournal);
        _board = Prop("board", BuildBoard);
        _tag = Mk(new MeshBuilder().Box(Vector3.Zero, new Vector3(0.36f, 0.24f, 0.02f), Palette.Parchment)
            .Box(new Vector3(-0.04f, -0.04f, 0.012f), new Vector3(0.2f, 0.012f, 0.006f), Palette.InkSoft)
            .Box(new Vector3(-0.06f, -0.08f, 0.012f), new Vector3(0.16f, 0.012f, 0.006f), Palette.InkSoft)
            .Box(new Vector3(0, 0.09f, 0.02f), new Vector3(0.035f, 0.035f, 0.03f), Brass));
        _swatch = Mk(new MeshBuilder().Box(Vector3.Zero, new Vector3(0.09f, 0.09f, 0.014f), Color.White));
        _wax = Mk(BuildWax());
        _glassTable = Prop("glass-table", BuildGlassTable);
        _lensRing = Prop("lens-ring", BuildLensRing);
        _lensGlass = Prop("lens-glass", BuildLensGlass);
        _bench = Prop("bench", BuildBench);
        _sheet = Prop("sheet", BuildSheet);
        _cabinet = Prop("cabinet", BuildCabinet);
        _plinth = Prop("plinth", BuildPlinth);
        _relicDoor = Prop("relic-door", BuildRelicDoor);
        _stand = Prop("stand", BuildBalanceStand);
        _beam = Prop("beam", BuildBeam);
        _pan = Prop("pan", BuildPan);
        _weight = Mk(new MeshBuilder().Cylinder(Vector3.Zero, 0.09f, 0.07f, 0.12f, 8, BrassDark, Brass).Blob(new Vector3(0, 0.15f, 0), new Vector3(0.035f, 0.035f, 0.035f), 2, 6, Brass));
        _circle = Prop("circle", BuildCircle);
        _circleCandles = Prop("circle-candles", BuildCircleCandles);
        _orb = Mk(new MeshBuilder().Blob(new Vector3(0, 0.1f, 0), new Vector3(0.1f, 0.095f, 0.1f), 4, 7, Color.White, 3, 0.08f));
        _rock = Mk(new MeshBuilder().Blob(new Vector3(0, 0.07f, 0), new Vector3(0.13f, 0.075f, 0.1f), 3, 6, Color.White, 9, 0.3f));
        _crystal = Mk(new MeshBuilder().Cone(new Vector3(0, 0.13f, 0), 0.08f, 0.16f, 5, Color.White)
            .Append(new MeshBuilder().Cone(Vector3.Zero, 0.08f, 0.13f, 5, Color.White).Transform(Matrix4x4.CreateRotationX(MathF.PI) * Matrix4x4.CreateTranslation(0, 0.13f, 0))));
        _jar = Mk(new MeshBuilder().Cylinder(Vector3.Zero, 0.075f, 0.07f, 0.17f, 8, Color.White).Cylinder(new Vector3(0, 0.17f, 0), 0.045f, 0.05f, 0.045f, 6, Palette.Mul(Color.White, 0.8f)));
        _box = Mk(new MeshBuilder().Box(new Vector3(0, 0.07f, 0), new Vector3(0.2f, 0.14f, 0.15f), Color.White).Box(new Vector3(0, 0.16f, 0), new Vector3(0.08f, 0.05f, 0.08f), Palette.Mul(Color.White, 0.85f)));

        _home = new Vector3(0.25f, 1.45f, -1.45f);
        _homeDist = 13.6f;
        CamTarget = _home; CamDist = _homeDist; CamYaw = 0.3f; CamPitch = 0.4f; CamFov = 36;

        AddSpot("desk", DeskPos + new Vector3(-1.45f, 0, -1.25f), DeskPos + new Vector3(1.45f, 3.25f, 0.68f), "The designation desk", "");
        AddSpot("glass", GlassPos + new Vector3(-0.5f, 0, -0.5f), GlassPos + new Vector3(0.5f, 1.8f, 0.55f), "The magnifying glass", "");
        AddSpot("bench", BenchPos + new Vector3(-1.5f, 0, -0.5f), BenchPos + new Vector3(1.5f, 3.35f, 0.55f), "The assembly bench", "");
        AddSpot("cabinet", CabinetPos + new Vector3(-0.9f, 0, -0.45f), CabinetPos + new Vector3(0.9f, 3.35f, 0.5f), "The cabinet", "");
        AddSpot("circle", CirclePos + new Vector3(-CircleR, 0, -CircleR), CirclePos + new Vector3(CircleR, 0.12f, CircleR), "A chalk circle", "");
        foreach (var s in S.Sanctum.Seals()) AnimFor(s.Id);
        UpdateSpots();
    }

    private SealAnim AnimFor(string sealId)
    {
        if (_seals.TryGetValue(sealId, out var a)) return a;
        a = new SealAnim();
        if (S.Sanctum.Solved(sealId)) { a.Beam = new Spring(0, 30, 2.4f); a.Door = new Spring(1, 55, 8); }
        _seals[sealId] = a;
        return a;
    }

    // ───────────────────────── per frame ─────────────────────────

    protected override void Tick(float dt)
    {
        _t += dt;
        _cardAge += dt;
        for (int i = 0; i < _later.Count; i++)
            if (_later[i].At <= _t) { var act = _later[i].Act; _later.RemoveAt(i--); act(); }

        UpdateSpots();
        FocusCamera(dt);

        bool still = R.ReduceMotion;
        var seals = S.Sanctum.Seals();
        for (int i = 0; i < seals.Count; i++)
        {
            var a = AnimFor(seals[i].Id);
            bool solved = S.Sanctum.Solved(seals[i].Id);
            a.Beam.Target = solved ? 0 : ShutTilt;
            a.Door.Target = solved ? 1 : 0;
            if (still) { a.Beam.Value = a.Beam.Target; a.Beam.Velocity = 0; }
            a.Beam.Update(dt);
            a.Door.Update(dt);
            if (a.PanT >= 0) { a.PanT += dt; if (a.PanT > 4.2f) { a.PanT = -1; a.PanDef = null; } }
        }
        _lens.Update(dt);
        _gust = MathF.Max(0, _gust - dt * 0.8f);
        if (_flutter >= 0) { _flutter += dt; if (_flutter > 1.6f) _flutter = -1; }
        if (_sealT >= 0) _sealT += dt;
        if (_benchFx >= 0) { _benchFx += dt; if (_benchFx > 1.2f) _benchFx = -1; }
        if (_circleFx >= 0)
        {
            _circleFx += dt / (still ? 0.6f : 1.6f);
            if (_circleFx >= 1)
            {
                _circleFx = -1;
                Burst(CirclePos + new Vector3(0, 0.4f, 0), Palette.Lerp(Accent, Palette.LanternHot, 0.6f), 40, 2.6f);
                _restoredT = 0;
            }
        }
        if (_restoredT >= 0) { _restoredT += dt; if (_restoredT > 3.2f) { _restoredT = -1; _restoredDef = null; } }
        if (_glassItem is not null && !S.Sanctum.AtHand.Any(i => i.Id == _glassItem)) _glassItem = null;

        if (!still) Ambient(dt);
    }

    /// <summary>Dust in the window's light, a thread of candle smoke, and motes over a waking circle.</summary>
    private void Ambient(float dt)
    {
        var p = App.Particles;
        _dustTimer -= dt;
        if (_dustTimer <= 0)
        {
            _dustTimer = 0.22f;
            // the window is on the left wall; its light falls toward +x and down
            float s = p.R(0.6f, 4.2f);
            var at = new Vector3(-5.2f, 3.5f, -1.5f) + new Vector3(0.9f, -0.62f, -0.15f) * s + new Vector3(p.R(-0.5f, 0.5f), p.R(-0.6f, 0.6f), p.R(-0.7f, 0.7f));
            p.Emit(at, new Vector3(p.R(-0.05f, 0.05f), p.R(-0.02f, 0.05f), p.R(-0.05f, 0.05f)), p.R(5, 8), p.R(0.03f, 0.06f), Palette.Alpha(Palette.LanternHot, 0.55f), true, 0, 0.05f);
        }
        _smokeTimer -= dt;
        if (_smokeTimer <= 0)
        {
            _smokeTimer = p.R(1.6f, 3.2f);
            var wick = DeskCandles[(int)p.R(0, DeskCandles.Length - 0.01f)];
            p.Emit(wick + new Vector3(0, 0.12f, 0), new Vector3(p.R(-0.04f, 0.04f), p.R(0.25f, 0.4f), 0), p.R(2, 3), 0.08f, Palette.Alpha(Palette.Hex(0xD8D0C4), 0.22f), false, 0.12f, 0.3f);
        }
        if (S.Sanctum.CircleAvailable)
        {
            _moteTimer -= dt;
            if (_moteTimer <= 0)
            {
                _moteTimer = 0.45f;
                float a = p.R(0, MathF.Tau);
                var at = CirclePos + new Vector3(MathF.Cos(a) * (CircleR - 0.05f), 0.04f, MathF.Sin(a) * (CircleR - 0.05f));
                p.Emit(at, new Vector3(0, p.R(0.12f, 0.3f), 0), p.R(1.6f, 2.6f), p.R(0.05f, 0.09f), Palette.Alpha(Palette.Lerp(Accent, Palette.LanternHot, 0.5f), 0.6f), true, -0.01f, 0.2f);
            }
        }
    }

    private void FocusCamera(float dt)
    {
        Vector3? focus = _card switch
        {
            Card.Desk or Card.Designation => DeskPos + new Vector3(0, 1.5f, 0),
            Card.Glass => GlassPos + new Vector3(0.2f, 1.1f, 0),
            Card.Bench => BenchPos + new Vector3(0, 1.7f, 0),
            Card.Cabinet => CabinetPos + new Vector3(-0.2f, 1.7f, 0),
            Card.Circle => CirclePos + new Vector3(0, 0.4f, -0.4f),
            Card.Seal => SealPos(SealIndex(_seal)) + new Vector3(0, 1.5f, 0),
            _ => null,
        };
        var wantT = _home;
        float wantD = _homeDist;
        if (focus is { } f)
        {
            // lean in, and keep the station clear of the working card on the left
            var right = new Vector3(MathF.Cos(CamYaw), 0, -MathF.Sin(CamYaw));
            wantT = f - right * 2.35f;
            wantD = 11.2f;
        }
        float rate = R.ReduceMotion ? 40 : 3.2f;
        CamTarget = Ease.Approach(CamTarget, wantT, rate, dt);
        CamDist = Ease.Approach(CamDist, wantD, rate, dt);
    }

    private int SealIndex(string? id)
    {
        var seals = S.Sanctum.Seals();
        for (int i = 0; i < seals.Count; i++) if (seals[i].Id == id) return i;
        return 0;
    }

    /// <summary>Names and hints follow the state; seals come and go with their conditions.</summary>
    private void UpdateSpots()
    {
        var san = S.Sanctum;
        int open = S.Unresolved.Open.Count();
        int ready = S.Unresolved.Open.Count(o => S.Gate.AnyAttemptable(o));
        var desk = SpotOf("desk");
        desk.Hint = open == 0 ? "The journal lies open. Nothing unexplained, for now."
            : $"The journal lies open. {Count(open, "thing")} still unexplained" + (ready > 0 ? $"; your notes feel full enough to try {(ready == 1 ? "one" : "some")}." : ".");

        var lens = LensTools().FirstOrDefault();
        var glass = SpotOf("glass");
        glass.Enabled = lens is not null;
        if (lens is not null) { glass.Name = lens.Name; glass.Hint = lens.Description; }

        var bps = san.Blueprints();
        var bench = SpotOf("bench");
        bench.Hint = bps.Count == 0 ? "Tools, twine and a vice. You don't know how to make anything here yet."
            : $"{Count(bps.Count, "sketch", "sketches")} pinned above it" + (bps.Any(b => b.CanAssemble) ? "; you have what one of them needs." : ".");

        var cab = san.Cabinet.Count();
        SpotOf("cabinet").Hint = cab == 0 ? "A tall cedar cabinet. Empty, and smelling of cedar." : $"A tall cedar cabinet. {Count(cab, "thing")} put away inside.";

        var circle = SpotOf("circle");
        var ct = CircleTool();
        circle.Name = san.CircleAvailable && ct is not null ? ct.Name : "A chalk circle";
        circle.Hint = san.CircleAvailable ? "Glowing faintly, like a hearth after the fire's gone out." : "Just chalk, as far as you can tell.";

        var seals = san.Seals();
        var live = new HashSet<string>();
        for (int i = 0; i < seals.Count; i++)
        {
            var s = seals[i];
            var id = "seal:" + s.Id;
            live.Add(id);
            if (!HasSpot(id)) AddSpot(id, SealPos(i) + new Vector3(-0.62f, 0, -0.6f), SealPos(i) + new Vector3(0.62f, 2.75f, 0.62f), Cap(s.Name), "");
            var spot = SpotOf(id);
            spot.Hint = san.Solved(s.Id) ? "It stands open above an empty reliquary." : s.Inscription;
        }
        foreach (var gone in _seals.Keys.Where(k => !live.Contains("seal:" + k)).ToList())
        {
            RemoveSpot("seal:" + gone);
            _seals.Remove(gone);
        }
    }

    // ───────────────────────── light ─────────────────────────

    private static readonly Vector3[] DeskCandles = { DeskPos + new Vector3(-1.12f, 1.42f, -0.34f), DeskPos + new Vector3(1.16f, 1.34f, -0.38f) };
    private static readonly Vector3 GlassCandle = GlassPos + new Vector3(-0.26f, 1.2f, 0.22f);
    private static readonly Vector3 BenchLamp = BenchPos + new Vector3(-1.1f, 1.32f, -0.18f);
    private static readonly Vector3 Sconce = new(3.2f, 2.78f, -5.12f);
    private static readonly Vector3 StandCandle = BalancePos + new Vector3(-0.85f, 1.78f, 0.62f);

    private float Flicker(int i) => R.ReduceMotion ? 1
        : 1 + 0.07f * MathF.Sin(_t * 11.3f + i * 1.7f) + 0.05f * MathF.Sin(_t * 6.1f + i * 3.1f) + 0.03f * MathF.Sin(_t * 23f + i);

    protected override void Light()
    {
        base.Light();
        var l = R.Light;
        // a dimmer, warmer room than the others: candles do most of the work here
        l.Sun = Palette.Mul(l.Sun, 0.78f);
        l.Sky = Palette.Mul(Palette.Hex(0xCDB494), 0.92f);
        l.Ground = Palette.Hex(0x6A5244);
        var warm = Palette.LanternHot;
        float g = 1 - _gust * 0.45f;
        l.Lamps.Add((DeskPos + new Vector3(0, 1.75f, -0.2f), 5.2f, warm, 1.0f * Flicker(0) * g));
        l.Lamps.Add((GlassCandle + new Vector3(0, 0.2f, 0), 3.6f, warm, 0.75f * Flicker(1)));
        l.Lamps.Add((BenchLamp + new Vector3(0, 0.25f, 0.2f), 4.4f, warm, 0.85f * Flicker(2)));
        l.Lamps.Add((Sconce + new Vector3(0, 0, 0.4f), 4.2f, warm, 0.75f * Flicker(3)));
        l.Lamps.Add((StandCandle + new Vector3(0, 0.15f, 0), 4.4f, warm, 0.85f * Flicker(4)));
        if (S.Sanctum.CircleAvailable || _circleFx >= 0)
        {
            float pulse = R.ReduceMotion ? 0.5f : 0.5f + 0.5f * MathF.Sin(_t * 1.1f);
            float fx = _circleFx >= 0 ? 1.4f * MathF.Sin(MathF.PI * _circleFx) : 0;
            l.Lamps.Add((CirclePos + new Vector3(0, 0.6f, 0), 3.8f, Palette.Lerp(Accent, warm, 0.55f), 0.45f + 0.25f * pulse + fx));
        }
        float relic = _seals.Values.Select(a => Math.Clamp(a.Door.Value, 0, 1)).DefaultIfEmpty(0).Max();
        if (relic > 0.05f) l.Lamps.Add((BalancePos + new Vector3(0, 0.6f, 0.75f), 2.2f, warm, 0.6f * relic));
    }

    // ───────────────────────── drawing ─────────────────────────

    private static Matrix4x4 At(Vector3 p) => Matrix4x4.CreateTranslation(p);
    private float LiftOf(string id) => HasSpot(id) ? SpotOf(id).Lift.Value : 0;

    protected override void DrawRoom()
    {
        var san = S.Sanctum;
        bool still = R.ReduceMotion;
        R.Draw(_decor, Matrix4x4.Identity, Color.White, outlineWidth: 1.3f);
        R.Draw(_oculusGlass, Matrix4x4.Identity, Color.White, emissive: 0.55f, outline: false);

        // the designation desk, the journal and the specimen board
        float dl = LiftOf("desk");
        var dh = Rim("desk");
        R.Draw(_desk, At(new Vector3(0, dl, 0)), Color.White, highlight: dh, outlineWidth: 1.5f);
        R.Draw(_board, Matrix4x4.Identity, Color.White, highlight: dh, outlineWidth: 1.3f);
        bool ready = S.Unresolved.Open.Any(o => S.Gate.AnyAttemptable(o));
        float jlift = dl + (_flutter >= 0 ? 0.05f * MathF.Sin(MathF.PI * MathF.Min(1, _flutter / 0.6f)) : 0);
        float jglow = ready ? (still ? 0.12f : 0.1f + 0.06f * MathF.Sin(_t * 1.6f)) : 0;
        R.Draw(_journal, At(new Vector3(0, jlift, 0)), Color.White, emissive: jglow, highlight: dh, outlineWidth: 1.2f);
        DrawJournalSeal(dl);
        DrawBoardTags();

        // the magnifying glass and whatever is under it
        float gl = LiftOf("glass");
        var gh = Rim("glass");
        R.Draw(_glassTable, At(new Vector3(0, gl, 0)), Color.White, highlight: gh, outlineWidth: 1.4f);
        if (_glassItem is not null && S.World.Items.Get(_glassItem) is { } gi)
            DrawToken(gi.Def, gi.Inert, GlassPos + new Vector3(0, 0.9f + gl, 0.08f), 1.1f, gi.Essence is not null);
        var lensWorld = Matrix4x4.CreateRotationX(_lens.Value) * At(LensPivot + new Vector3(0, gl, 0));
        R.Draw(_lensRing, lensWorld, Color.White, highlight: gh, outlineWidth: 1.4f);

        // the assembly bench and its pinned sketches
        float bl = LiftOf("bench");
        var bh = Rim("bench");
        R.Draw(_bench, At(new Vector3(0, bl, 0)), Color.White, highlight: bh, outlineWidth: 1.4f);
        var bps = san.Blueprints();
        for (int i = 0; i < Math.Min(bps.Count, 4); i++)
        {
            var at = BenchPos + new Vector3(-1.02f + i * 0.68f, 2.9f, -0.5f);
            var rot = Matrix4x4.CreateRotationZ((i % 2 == 0 ? 1 : -1) * 0.045f);
            float glow = bps[i].CanAssemble ? (still ? 0.25f : 0.18f + 0.1f * MathF.Sin(_t * 2 + i)) : 0;
            R.Draw(_sheet, rot * At(at), Blueprint, emissive: glow, highlight: bh, outlineWidth: 1.1f);
        }
        if (_benchFx >= 0) R.Glow(BenchPos + new Vector3(0, 1.1f, 0.1f), 1.4f * (1 - _benchFx / 1.2f), Palette.Alpha(Palette.LanternHot, 0.8f * (1 - _benchFx / 1.2f)));

        // the cabinet and what's been put away in it
        float cl = LiftOf("cabinet");
        R.Draw(_cabinet, At(new Vector3(0, cl, 0)), Color.White, highlight: Rim("cabinet"), outlineWidth: 1.5f);
        DrawCabinetItems(cl);

        // the Balance (one for each seal)
        var seals = san.Seals();
        for (int i = 0; i < seals.Count; i++) DrawBalance(i, seals[i]);

        // the circle
        DrawCircle();

        // translucent and glowing things last
        R.Draw(_lensGlass, lensWorld, Palette.Alpha(Color.White, 0.42f), emissive: 0.35f, outline: false);
        DrawFlames();
    }

    private void DrawJournalSeal(float lift)
    {
        bool any = S.State.Evidence.Resolved.Count > 0;
        if (!any && _sealT < 0) return;
        var at = JournalAt + new Vector3(0.36f, 0.075f + lift, 0.16f);
        float s = 1, drop = 0;
        if (_sealT >= 0 && !R.ReduceMotion)
        {
            s = Ease.OutElastic(MathF.Min(1, _sealT / 0.8f));
            drop = (1 - Ease.OutCubic(MathF.Min(1, _sealT / 0.3f))) * 0.7f;
        }
        if (s < 0.02f) return;
        R.Draw(_wax, Matrix4x4.CreateScale(s * 0.85f) * Matrix4x4.CreateRotationY(0.4f) * At(at + new Vector3(0, drop, 0)), Color.White, emissive: 0.05f, outlineWidth: 1.1f);
    }

    /// <summary>One parchment tag per thing that has turned up; settled ones carry a wax dot.</summary>
    private void DrawBoardTags()
    {
        var objs = S.Sanctum.Objects();
        var face = Matrix4x4.CreateRotationX(MathF.PI / 2);
        for (int i = 0; i < Math.Min(objs.Count, 8); i++)
        {
            var o = objs[i];
            int col = i % 4, row = i / 4;
            var at = new Vector3(DeskPos.X - 0.78f + col * 0.52f, 2.86f - row * 0.42f, -5.29f);
            var tilt = Matrix4x4.CreateRotationZ(((i * 37) % 7 - 3) * 0.025f);
            R.Draw(_tag, tilt * At(at), Color.White, outlineWidth: 1);
            var col3 = S.Content.Items.TryGetValue(o.BaseForm, out var def) ? App.Art.ColorOf(def) : Palette.Stone;
            R.Draw(_swatch, tilt * At(at + new Vector3(0.11f, -0.03f, 0.015f)), col3, outline: false);
            if (S.Ledger.IsResolved(o.Id))
                R.Draw(_wax, Matrix4x4.CreateScale(0.42f) * face * tilt * At(at + new Vector3(0.12f, -0.08f, 0.02f)), Color.White, outlineWidth: 0.8f);
        }
    }

    private void DrawCabinetItems(float lift)
    {
        var items = S.Sanctum.Cabinet.ToList();
        for (int i = 0; i < Math.Min(items.Count, 12); i++)
        {
            int shelf = i / 4, slot = i % 4;
            var at = CabinetPos + new Vector3(-0.54f + slot * 0.36f, 2.58f - shelf * 0.56f + lift, 0.04f);
            DrawToken(items[i].Def, items[i].Inert, at, 1.05f, items[i].Essence is not null);
        }
    }

    private void DrawBalance(int i, SealResource seal)
    {
        var a = AnimFor(seal.Id);
        var id = "seal:" + seal.Id;
        float l = LiftOf(id);
        var h = Rim(id);
        var basePos = SealPos(i) + new Vector3(0, l, 0);
        R.Draw(_plinth, At(basePos), Color.White, highlight: h, outlineWidth: 1.5f);
        float open = Math.Clamp(a.Door.Value, -0.1f, 1.15f);
        R.Draw(_relicDoor, Matrix4x4.CreateRotationY(-open * 1.75f) * At(basePos + new Vector3(-0.3f, 0.56f, 0.475f)), Color.White, highlight: h, outlineWidth: 1.2f);
        if (open > 0.05f) R.Glow(basePos + new Vector3(0, 0.56f, 0.38f), 0.9f * open, Palette.Alpha(Palette.LanternHot, 0.55f * open));
        R.Draw(_stand, At(basePos), Color.White, highlight: h, outlineWidth: 1.3f);

        float th = a.Beam.Value + (R.ReduceMotion ? 0 : 0.012f * MathF.Sin(_t * 0.9f + i));
        var pivot = BeamPivot(i) + new Vector3(0, l, 0);
        R.Draw(_beam, Matrix4x4.CreateRotationZ(th) * At(pivot), Color.White, highlight: h, outlineWidth: 1.3f);
        var left = pivot + new Vector3(-BeamHalf * MathF.Cos(th), -BeamHalf * MathF.Sin(th), 0);
        var right = pivot + new Vector3(BeamHalf * MathF.Cos(th), BeamHalf * MathF.Sin(th), 0);
        R.Draw(_pan, At(left), Color.White, highlight: h, outlineWidth: 1.2f);
        R.Draw(_pan, At(right), Color.White, highlight: h, outlineWidth: 1.2f);
        R.Draw(_weight, At(right + new Vector3(0.05f, -0.565f, 0.02f)), Color.White, outlineWidth: 1);
        if (a.PanDef is not null && a.PanT >= 0)
        {
            float drop = R.ReduceMotion ? 0 : (1 - Ease.OutCubic(MathF.Min(1, a.PanT / 0.35f))) * 0.6f;
            float fade = a.PanT > 3.4f ? 1 - (a.PanT - 3.4f) / 0.8f : 1;
            DrawToken(a.PanDef, a.PanInert, left + new Vector3(0, -0.565f + drop + (1 - fade) * 0.3f, 0), 1.15f * MathF.Max(0.05f, fade), false);
        }
    }

    private void DrawCircle()
    {
        bool lit = S.Sanctum.CircleAvailable;
        float pulse = R.ReduceMotion ? 0.5f : 0.5f + 0.5f * MathF.Sin(_t * 1.1f);
        float fx = _circleFx >= 0 ? MathF.Sin(MathF.PI * _circleFx) : 0;
        float hov = HasSpot("circle") ? SpotOf("circle").Glow : 0;
        float em = lit ? 0.12f + 0.16f * pulse + 1.2f * fx + hov * 0.25f : hov * 0.15f;
        var tint = lit ? Palette.Lerp(Color.White, Palette.LanternHot, 0.25f + 0.25f * fx) : Palette.Hex(0xCFC8B8);
        R.Draw(_circle, At(CirclePos + new Vector3(0, 0.006f, 0)), tint, emissive: em, outline: false);
        R.Draw(_circleCandles, Matrix4x4.Identity, Color.White, highlight: Rim("circle"), outlineWidth: 1.1f);

        var glowCol = Palette.Lerp(Accent, Palette.LanternHot, 0.55f);
        if (lit)
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * MathF.Tau + MathF.PI / 2;
                var p = CirclePos + new Vector3(MathF.Cos(a) * (CircleR - 0.38f), 0.05f, MathF.Sin(a) * (CircleR - 0.38f));
                float s = R.ReduceMotion ? 0.5f : 0.5f + 0.5f * MathF.Sin(_t * 1.1f + k);
                R.Glow(p, 0.45f + 0.15f * s, Palette.Alpha(glowCol, 0.18f + 0.12f * s));
            }
        if (_circleFx >= 0 && !R.ReduceMotion)
        {
            // a spark runs the chalk, drawing the circle in light
            float head = _circleFx * 1.15f;
            for (int k = 0; k < 18; k++)
            {
                float tt = head - k * 0.018f;
                if (tt < 0) break;
                float a = MathF.Min(1, tt) * MathF.Tau - MathF.PI / 2;
                var p = CirclePos + new Vector3(MathF.Cos(a) * (CircleR - 0.03f), 0.06f, MathF.Sin(a) * (CircleR - 0.03f));
                R.Glow(p, 0.5f - k * 0.02f, Palette.Alpha(Palette.LanternHot, 0.9f - k * 0.045f));
            }
            if (_circleFx > 0.6f)
            {
                float c = (_circleFx - 0.6f) / 0.4f;
                for (int k = 0; k < 6; k++) R.Glow(CirclePos + new Vector3(0, 0.2f + k * 0.35f * c, 0), 1.1f - k * 0.12f, Palette.Alpha(glowCol, 0.35f * c));
            }
        }
        if (_restoredDef is not null && _restoredT >= 0)
        {
            float rise = R.ReduceMotion ? 0.6f : Ease.OutCubic(MathF.Min(1, _restoredT / 0.9f)) * 0.6f;
            float fade = _restoredT > 2.4f ? 1 - (_restoredT - 2.4f) / 0.8f : 1;
            float pop = R.ReduceMotion ? 1 : Ease.OutBack(MathF.Min(1, _restoredT / 0.5f));
            DrawToken(_restoredDef, false, CirclePos + new Vector3(0, 0.05f + rise, 0), 2.2f * pop * MathF.Max(0.05f, fade), true);
            R.Glow(CirclePos + new Vector3(0, 0.25f + rise, 0), 1.2f * fade, Palette.Alpha(Palette.LanternHot, 0.35f * fade));
        }
    }

    private void DrawFlames()
    {
        float g = 1 - _gust * 0.55f;
        int i = 0;
        foreach (var c in DeskCandles) Flame(c + new Vector3(_gust * 0.03f, 0, 0), 1.0f * g, i++);
        Flame(GlassCandle + new Vector3(0, LiftOf("glass"), 0), 0.9f, i++);
        Flame(BenchLamp + new Vector3(0, LiftOf("bench"), 0), 1.0f, i++);
        Flame(Sconce, 1.0f, i++);
        Flame(StandCandle, 1.0f, i++);
        if (S.Sanctum.CircleAvailable)
            foreach (var c in CircleCandleTops) Flame(c, 0.8f, i++);
    }

    private void Flame(Vector3 p, float size, int i)
    {
        float f = Flicker(i + 3);
        R.Glow(p, 0.75f * size * f, Palette.Alpha(Palette.Lantern, 0.38f));
        R.Glow(p + new Vector3(0, 0.02f, 0), 0.24f * size * f, Palette.Alpha(Palette.LanternHot, 0.95f));
        R.Glow(p + new Vector3(0, 0.01f, 0), 0.09f * size, Palette.Alpha(Color.White, 0.9f));
    }

    /// <summary>A small stand-in for a carried thing, in its icon colour: orb, crystal, rock, jar or box by kind.</summary>
    private void DrawToken(string defId, bool inert, Vector3 at, float scale, bool essence)
    {
        if (!S.Content.Items.TryGetValue(defId, out var def)) return;
        var model = def.Kind switch
        {
            ItemKind.Remnant => _rock,
            ItemKind.Tool => _box,
            ItemKind.Mineral or ItemKind.Aura => _crystal,
            ItemKind.ClueForm or ItemKind.Food => _orb,
            _ => _jar,
        };
        var col = App.Art.ColorOf(def);
        if (inert) col = Palette.Lerp(col, Palette.InkFaint, 0.7f);
        R.Draw(model, Matrix4x4.CreateScale(scale) * At(at), col, emissive: inert ? 0 : 0.12f, outlineWidth: 1.1f);
        if (essence && !inert)
            R.Glow(at + new Vector3(0, 0.12f * scale, 0), 0.32f * scale, Palette.Alpha(Palette.LanternHot, R.ReduceMotion ? 0.3f : 0.24f + 0.08f * MathF.Sin(_t * 2.2f + at.X)));
    }

    // ───────────────────────── juice helpers ─────────────────────────

    private void Burst(Vector3 at, Color c, int n = 24, float speed = 2.2f)
    {
        if (R.ReduceMotion) { App.Particles.Emit(at, Vector3.Zero, 0.8f, 0.9f, Palette.Alpha(c, 0.5f), true); return; }
        App.Particles.Burst(at, c, n, speed);
    }

    private void Motes(Vector3 at, Color c, int n)
    {
        if (R.ReduceMotion) return;
        var p = App.Particles;
        for (int i = 0; i < n; i++)
            p.Emit(at + new Vector3(p.R(-0.3f, 0.3f), 0, p.R(-0.2f, 0.2f)), new Vector3(p.R(-0.1f, 0.1f), p.R(0.4f, 0.8f), p.R(-0.1f, 0.1f)), p.R(1.2f, 2f), p.R(0.06f, 0.11f), Palette.Alpha(c, 0.75f), true, -0.02f, 0.6f);
    }

    private void Hop(string spot, float k = 1.6f)
    {
        if (HasSpot(spot) && !R.ReduceMotion) SpotOf(spot).Lift.Kick(k);
    }

    /// <summary>Runs on the next frame's update, never mid-draw (so a fresh overlay can't catch this frame's click).</summary>
    private void Do(Action a) => _later.Add((_t, a));
    private void After(float seconds, Action a) => _later.Add((_t + seconds, a));

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    private static string Count(int n, string one, string? many = null) => $"{n} {(n == 1 ? one : many ?? one + "s")}";
}
