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
/// The Library: one bookcase per shelf along the back wall, a hanging plaque on each for its genre tag, the lamp at
/// the left end (shelf order runs from it), the reading table with the pile, and a returns box. Spines take their
/// colour from each book's cover description rather than its genre; how closely covers echo their genre is the
/// content's call.
/// Every rule is LibraryLocale's; this file only stages it.
/// </summary>
public sealed class LibraryScene : InteriorScene
{
    protected override string LocaleId => "library";

    // ───── layout (the back wall's face is z = -5.4; the left wall's x = -5.4) ─────
    private const float CaseZ = -5.0f, CaseDepth = 0.62f, BoardY = 1.25f, CaseH = 3.15f, BookDepth = 0.4f;
    private const float RowLeft = -4.55f, RowRight = 5.05f;
    private static readonly Vector3 LampPos = new(-5.08f, 2.35f, -4.85f);
    private static readonly Vector3 TablePos = new(0.4f, 0, 1.15f);
    private static readonly Vector3 PileAt = TablePos + new Vector3(-0.55f, 1.02f, 0.05f);
    private static readonly Vector3 CandleAt = TablePos + new Vector3(0.95f, 1.02f, -0.3f);
    private static readonly Vector3 ReturnsPos = new(4.1f, 0, 2.7f);

    private enum Card { None, Pile, Shelf }
    private Card _card;
    private float _cardAge, _shift;
    private int _shelfCard;
    private int _pileAt;

    private Part _case, _plaque, _table, _decor, _returns, _lampGlass;
    private Model _book, _band, _flat, _token;
    /// <summary>A kit's bookcase is made for <see cref="KitShelves"/> shelves; this stretches it to the content's count.</summary>
    private Matrix4x4 _caseFit = Matrix4x4.Identity;
    private const int KitShelves = 5;
    private readonly Dictionary<string, float> _arrive = new();        // tome id → slide-in progress (0 → 1)
    private readonly Dictionary<string, int> _onShelf = new();         // tome id → shelf index, last frame
    private readonly HashSet<string> _settled = new();
    private readonly Dictionary<int, float> _settleFx = new();         // shelf → glow (1 → 0)
    private readonly Dictionary<int, Spring> _plaqueSwing = new();
    private Spring _pileWobble = new(0, 160, 7);
    private float _dustT;
    private bool _primed;

    private sealed record Choice(string Label, Action Run);

    private float CaseW(int n) => (RowRight - RowLeft) / Math.Max(1, n) - 0.12f;
    private float CaseX(int i, int n) => RowLeft + (i + 0.5f) * (RowRight - RowLeft) / Math.Max(1, n);

    // ───────────────────────── building ─────────────────────────

    protected override void Build()
    {
        CamTarget = new Vector3(0.25f, 1.7f, -1.4f);
        CamYaw = 0.3f; CamPitch = 0.33f; CamDist = 13.2f; CamFov = 36;
        var toon = R.Shaders.Toon;
        int n = S.Library.Shelves().Count;
        _case = Prop("case", () => BuildCase(CaseW(n)));
        if (FromKit("case")) _caseFit = Matrix4x4.CreateScale(CaseW(n) / CaseW(KitShelves), 1, 1);
        _book = Own(new MeshBuilder().Box(new Vector3(0, 0.5f, 0), Vector3.One, Color.White).Model(toon));
        _band = Own(new MeshBuilder().Box(new Vector3(0, 0, 0), new Vector3(1.04f, 1, 1.02f), Color.White).Model(toon));
        _flat = Own(new MeshBuilder().Box(new Vector3(0, 0.05f, 0), new Vector3(0.52f, 0.1f, 0.36f), Color.White)
            .Box(new Vector3(0.012f, 0.05f, 0), new Vector3(0.5f, 0.08f, 0.37f), Palette.Hex(0xF2E8D2)).Model(toon));
        _plaque = Prop("plaque", () => new MeshBuilder().Box(new Vector3(0, -0.2f, 0), new Vector3(0.72f, 0.34f, 0.06f), Color.White)
            .Box(new Vector3(-0.22f, 0.02f, 0), new Vector3(0.02f, 0.14f, 0.02f), Palette.Hex(0x8E6E2E))
            .Box(new Vector3(0.22f, 0.02f, 0), new Vector3(0.02f, 0.14f, 0.02f), Palette.Hex(0x8E6E2E)));
        _table = Prop("table", BuildTable);
        _decor = Prop("decor", BuildDecor);
        _returns = Prop("returns", BuildReturns);
        _token = Own(new MeshBuilder().Blob(new Vector3(0, 0.06f, 0), new Vector3(0.11f, 0.06f, 0.09f), 3, 7, Color.White, 5, 0.2f).Model(toon));
        _lampGlass = Prop("lamp-glass", () => new MeshBuilder().Cylinder(Vector3.Zero, 0.16f, 0.12f, 0.36f, 8, Palette.LanternHot));

        for (int i = 0; i < n; i++)
        {
            float x = CaseX(i, n), w = CaseW(n);
            AddSpot("shelf:" + i, new Vector3(x - w / 2, 0, CaseZ - CaseDepth / 2), new Vector3(x + w / 2, CaseH - 0.45f, CaseZ + CaseDepth / 2 + 0.05f), "", "");
            AddSpot("tag:" + i, new Vector3(x - 0.42f, CaseH - 0.42f, CaseZ + 0.2f), new Vector3(x + 0.42f, CaseH, CaseZ + 0.5f), "", "");
            _plaqueSwing[i] = new Spring(0, 90, 5);
        }
        AddSpot("pile", PileAt + new Vector3(-0.6f, -0.05f, -0.65f), PileAt + new Vector3(0.75f, 0.95f, 0.6f), "The pile", "");
        AddSpot("returns", ReturnsPos + new Vector3(-0.5f, 0, -0.4f), ReturnsPos + new Vector3(0.5f, 0.85f, 0.4f), "The returns box", "");
        UpdateSpots();
    }

    private static MeshBuilder BuildCase(float w)
    {
        var m = new MeshBuilder();
        var wood = Palette.Hex(0x7A5236); var dark = Palette.Hex(0x5E3E29); var inner = Palette.Hex(0x4A3222);
        float d = CaseDepth;
        m.Box(new Vector3(0, CaseH / 2, -d / 2 + 0.03f), new Vector3(w, CaseH, 0.06f), inner);                      // back
        m.Box(new Vector3(-w / 2 + 0.05f, CaseH / 2, 0), new Vector3(0.1f, CaseH, d), wood);                          // sides
        m.Box(new Vector3(w / 2 - 0.05f, CaseH / 2, 0), new Vector3(0.1f, CaseH, d), wood);
        m.Box(new Vector3(0, CaseH + 0.06f, 0.02f), new Vector3(w + 0.16f, 0.14f, d + 0.1f), dark);                 // crown
        m.Box(new Vector3(0, 0.14f, 0.02f), new Vector3(w + 0.06f, 0.28f, d + 0.04f), dark);                        // plinth
        foreach (var y in new[] { BoardY, 2.25f })
            m.Box(new Vector3(0, y - 0.04f, 0), new Vector3(w - 0.12f, 0.08f, d - 0.04f), wood, Palette.Mul(wood, 1.1f));
        m.Box(new Vector3(0, BoardY - 0.12f, d / 2 - 0.02f), new Vector3(w - 0.12f, 0.06f, 0.03f), Palette.Hex(0xB08A4A)); // brass rail
        return m;
    }

    private static MeshBuilder BuildTable()
    {
        var m = new MeshBuilder();
        var wood = Palette.Hex(0x8A5E3C);
        m.Box(new Vector3(0, 0.96f, 0), new Vector3(2.9f, 0.12f, 1.5f), wood, Palette.Mul(wood, 1.08f));
        foreach (var sx in new[] { -1.3f, 1.3f })
            foreach (var sz in new[] { -0.62f, 0.62f })
                m.Box(new Vector3(sx, 0.45f, sz), new Vector3(0.12f, 0.9f, 0.12f), Palette.Mul(wood, 0.85f));
        m.Box(new Vector3(0, 0.3f, 0), new Vector3(2.5f, 0.06f, 1.1f), Palette.Mul(wood, 0.8f));
        // a candle in a brass dish, and an open ledger
        m.Cylinder(CandleAt - TablePos + new Vector3(0, -0.01f, 0), 0.16f, 0.16f, 0.03f, 10, Palette.Hex(0xC9A24A));
        m.Cylinder(CandleAt - TablePos + new Vector3(0, 0.02f, 0), 0.05f, 0.05f, 0.28f, 8, Palette.Hex(0xF1E6CC));
        m.Box(new Vector3(0.55f, 1.04f, 0.32f), new Vector3(0.62f, 0.04f, 0.44f), Palette.Hex(0xF2E8D2));
        m.Box(new Vector3(0.55f, 1.025f, 0.32f), new Vector3(0.66f, 0.03f, 0.48f), Palette.Hex(0x6B3A2E));
        return m.Transform(Matrix4x4.CreateTranslation(TablePos));
    }

    private MeshBuilder BuildDecor()
    {
        var m = new MeshBuilder();
        // the lamp the shelves are ordered from: a bracket and a brass housing on the left wall
        m.Box(LampPos + new Vector3(-0.2f, 0, 0), new Vector3(0.4f, 0.05f, 0.05f), Palette.Hex(0x3E3A3E));
        m.Cylinder(LampPos + new Vector3(0, 0.18f, 0), 0.2f, 0.06f, 0.16f, 8, Palette.Hex(0xC9A24A));
        m.Cylinder(LampPos + new Vector3(0, -0.24f, 0), 0.08f, 0.17f, 0.06f, 8, Palette.Hex(0xC9A24A));
        // a rolling ladder leaning on the cases
        var lw = Palette.Hex(0x9A6B44);
        var foot = new Vector3(2.2f, 0, -4.1f); var top = new Vector3(2.0f, CaseH - 0.2f, -4.62f);
        foreach (var off in new[] { -0.28f, 0.28f })
        {
            var a = foot + new Vector3(off, 0, 0); var b = top + new Vector3(off, 0, 0);
            var mid = (a + b) / 2; var len = (b - a).Length();
            var rot = Matrix4x4.CreateRotationX(-MathF.Atan2(b.Z - a.Z, b.Y - a.Y));
            m.Append(new MeshBuilder().Box(Vector3.Zero, new Vector3(0.06f, len, 0.06f), lw).Transform(rot * Matrix4x4.CreateTranslation(mid)));
        }
        for (int i = 1; i < 7; i++)
        {
            var p = Vector3.Lerp(foot, top, i / 7f);
            m.Box(p, new Vector3(0.6f, 0.04f, 0.05f), lw);
        }
        // a stool, a potted fern by the window, a little globe on the table's end
        m.Cylinder(new Vector3(-3.2f, 0.62f, 2.0f), 0.32f, 0.3f, 0.07f, 10, Palette.Hex(0x8A5E3C));
        foreach (var a in new[] { 0.4f, 2.5f, 4.6f })
            m.Box(new Vector3(-3.2f + MathF.Cos(a) * 0.2f, 0.3f, 2.0f + MathF.Sin(a) * 0.2f), new Vector3(0.06f, 0.62f, 0.06f), Palette.Hex(0x6E4A30));
        m.Cylinder(new Vector3(-4.7f, 0, 0.6f), 0.3f, 0.24f, 0.5f, 8, Palette.Hex(0xB86E4B));
        m.Blob(new Vector3(-4.7f, 0.75f, 0.6f), new Vector3(0.55f, 0.45f, 0.55f), 3, 7, Palette.Moss, 41, 0.25f);
        m.Cylinder(TablePos + new Vector3(-1.2f, 1.02f, -0.45f), 0.08f, 0.1f, 0.05f, 8, Palette.Hex(0xC9A24A));
        m.Blob(TablePos + new Vector3(-1.2f, 1.25f, -0.45f), new Vector3(0.18f, 0.18f, 0.18f), 4, 8, Palette.Hex(0x7FA3B8), 12, 0.04f);
        return m;
    }

    private static MeshBuilder BuildReturns()
    {
        var m = new MeshBuilder();
        var wood = Palette.Hex(0x7E5A3A);
        m.Box(ReturnsPos + new Vector3(0, 0.36f, 0), new Vector3(0.9f, 0.72f, 0.66f), wood, Palette.Mul(wood, 1.08f));
        m.Box(ReturnsPos + new Vector3(0, 0.74f, 0), new Vector3(0.96f, 0.05f, 0.72f), Palette.Mul(wood, 0.8f));
        m.Box(ReturnsPos + new Vector3(0, 0.77f, 0.05f), new Vector3(0.56f, 0.02f, 0.1f), Palette.Hex(0x2E2622));     // the slot
        m.Box(ReturnsPos + new Vector3(0, 0.42f, 0.335f), new Vector3(0.44f, 0.16f, 0.02f), Palette.Hex(0xE8DCC2));   // its label
        return m;
    }

    // ───────────────────────── colours ─────────────────────────

    private static float Hash01(string s, int salt = 0)
    {
        uint h = 2166136261u ^ (uint)salt;
        foreach (var ch in s) h = (h ^ ch) * 16777619;
        return (h % 10000) / 10000f;
    }

    /// <summary>A spine colour read from the cover's description ("smoke-blue cloth…"), not from its genre.</summary>
    private static Color CoverColor(TomeResource t)
    {
        var c = t.Cover.ToLowerInvariant();
        (string Word, uint Rgb)[] words =
        {
            ("oxblood", 0x7E2F2C), ("bottle-green", 0x2F5A3C), ("grass-green", 0x5E8F4E), ("moss", 0x5E7A45), ("green", 0x4E7A4A),
            ("river-blue", 0x4E78A0), ("smoke-blue", 0x5D7088), ("blue", 0x3D5AA8), ("ochre", 0xB9852E), ("mustard", 0xC09A3A),
            ("gold", 0xC9A24A), ("red", 0x9C3A2E), ("plum", 0x6E3E6A), ("violet", 0x6E4E8A), ("purple", 0x6E4E8A),
            ("grey", 0x7E7A78), ("gray", 0x7E7A78), ("black", 0x2E2A2E), ("white", 0xE8E0CE), ("cream", 0xE6DABC),
            ("vellum", 0xE3D6B4), ("parchment", 0xDCCBA4), ("tan", 0xB08A62), ("brown", 0x7A5236), ("leather", 0x8A5A3A),
        };
        foreach (var (w, rgb) in words)
            if (c.Contains(w)) return Palette.Mul(Palette.Hex(rgb), 0.9f + Hash01(t.Id, 3) * 0.2f);
        return Palette.Lerp(Palette.Hex(0x6E5A48), Palette.Hex(0xA88A62), Hash01(t.Id, 7));
    }

    private GenreResource? GenreOf(string? id) => id is not null && S.Content.Genres.TryGetValue(id, out var g) ? g : null;
    private Color GenreColor(string? id) => GenreOf(id) is { } g ? Palette.Genre(g.Color) : Palette.Hex(0xA0866A);

    // ───────────────────────── per frame ─────────────────────────

    protected override void Light()
    {
        base.Light();
        var l = R.Light;
        float f = R.ReduceMotion ? 1 : 0.92f + 0.08f * MathF.Sin(R.Time * 7.3f) * MathF.Sin(R.Time * 3.1f);
        l.Lamps.Add((LampPos + new Vector3(0, -0.2f, 0.3f), 6.5f, Palette.Lantern, 0.9f));
        l.Lamps.Add((CandleAt + new Vector3(0, 0.45f, 0), 4.2f, Palette.LanternHot, 0.75f * f));
    }

    protected override void Tick(float dt)
    {
        _cardAge += dt;
        _shift = Ease.Approach(_shift, _card == Card.None ? 0 : 1, 4, dt);
        var right = new Vector3(MathF.Cos(CamYaw), 0, -MathF.Sin(CamYaw));
        var focus = _card switch
        {
            Card.Shelf => new Vector3(CaseX(_shelfCard, S.Library.Shelves().Count), 1.6f, -3.6f),
            Card.Pile => PileAt + new Vector3(0, 0.3f, 0),
            _ => new Vector3(0.25f, 1.7f, -1.4f),
        };
        var goal = _card == Card.None ? focus : focus - right * 2.1f;
        CamTarget = Ease.Approach(CamTarget, goal, R.ReduceMotion ? 40 : 3.2f, dt);
        CamDist = Ease.Approach(CamDist, _card == Card.None ? 13.2f : 10.8f, R.ReduceMotion ? 40 : 3.2f, dt);

        Watch();
        foreach (var k in _arrive.Keys.ToList()) _arrive[k] = MathF.Min(1, _arrive[k] + dt / 0.55f);
        foreach (var k in _settleFx.Keys.ToList()) _settleFx[k] = MathF.Max(0, _settleFx[k] - dt / 2.2f);
        foreach (var k in _plaqueSwing.Keys.ToList()) { var s = _plaqueSwing[k]; s.Update(dt); _plaqueSwing[k] = s; }
        _pileWobble.Update(dt);
        UpdateSpots();
        Ambient(dt);
        if (_card != Card.None && Hovered is null && !Ui.WantsMouse && Input.Click) CloseCard();
    }

    /// <summary>Notices books that have just arrived on a shelf, and shelves that have just settled, for the juice.</summary>
    private void Watch()
    {
        var shelves = S.Library.Shelves();
        var now = new Dictionary<string, int>();
        foreach (var sh in shelves) foreach (var t in sh.Tomes) now[t.Id] = sh.Index;
        if (_primed)
        {
            foreach (var (id, idx) in now)
                if (!_onShelf.TryGetValue(id, out var was) || was != idx) _arrive[id] = R.ReduceMotion ? 1 : 0;
            foreach (var sh in shelves)
                if (sh.Settled && !_settled.Contains(sh.Id))
                {
                    _settleFx[sh.Index] = 1;
                    var c = GenreColor(sh.Genre);
                    float x = CaseX(sh.Index, shelves.Count), w = CaseW(shelves.Count);
                    for (int i = 0; i < 6; i++)
                        App.Particles.Burst(new Vector3(x - w / 2 + w * (i + 0.5f) / 6, BoardY + 0.4f, CaseZ + 0.3f), Palette.Lerp(c, Palette.LanternHot, 0.5f), R.ReduceMotion ? 2 : 6, 1.4f, 0.9f);
                    App.Audio.Play("chord", 0.6f);
                }
        }
        _onShelf.Clear();
        foreach (var kv in now) _onShelf[kv.Key] = kv.Value;
        _settled.Clear();
        foreach (var sh in shelves) if (sh.Settled) _settled.Add(sh.Id);
        _primed = true;
    }

    private void UpdateSpots()
    {
        var shelves = S.Library.Shelves();
        foreach (var sh in shelves)
        {
            var spot = SpotOf("shelf:" + sh.Index);
            spot.Name = Cap(sh.Label);
            spot.Hint = sh.Tomes.Count == 0 ? (sh.Genre is null ? "Bare wood and a little dust. Hang a tag on it first." : "Bare wood and a little dust.")
                : $"{sh.Tomes.Count} book{(sh.Tomes.Count == 1 ? "" : "s")}" + (sh.Settled ? ", settled. The shelf hums." : sh.InOrder ? ", in order." : ". Something about the run of years looks off.");
            var tag = SpotOf("tag:" + sh.Index);
            tag.Enabled = S.Library.Tagged;
            var g = GenreOf(sh.Genre);
            tag.Name = g is null ? "A blank tag hook" : $"The {g.Label} tag";
            tag.Hint = g is null ? "Click to hang a genre tag here." : $"{g.Sigil}. Click to change it.";
        }
        var pile = S.Library.Pile();
        var odd = S.Library.PileItems();
        var ps = SpotOf("pile");
        ps.Hint = pile.Count == 0 ? (odd.Count > 0 ? "No books left, but something odd is lying there." : "Empty. Every book is home.")
            : $"{pile.Count} book{(pile.Count == 1 ? "" : "s")} waiting to be shelved" + (odd.Count > 0 ? ", and something that isn't a book." : ".");
        var outs = S.Library.CheckedOut();
        SpotOf("returns").Hint = outs.Count == 0 ? "Nothing on loan to you." : "Return " + string.Join(", ", outs.Select(t => $"“{t.Title}”")) + ".";
    }

    private void Ambient(float dt)
    {
        if (R.ReduceMotion) return;
        _dustT -= dt;
        if (_dustT > 0) return;
        _dustT = 0.2f;
        var p = App.Particles;
        float s = p.R(0.4f, 4.4f);
        var at = new Vector3(-5.2f, 3.6f, -1.5f) + new Vector3(0.95f, -0.6f, 0.15f) * s + new Vector3(p.R(-0.4f, 0.4f), p.R(-0.5f, 0.5f), p.R(-0.6f, 0.6f));
        p.Emit(at, new Vector3(p.R(-0.04f, 0.04f), p.R(-0.01f, 0.05f), p.R(-0.04f, 0.04f)), p.R(5, 8), p.R(0.03f, 0.06f), Palette.Alpha(Palette.LanternHot, 0.55f), true, 0, 0.05f);
    }

    // ───────────────────────── drawing the room ─────────────────────────

    protected override void DrawRoom()
    {
        var shelves = S.Library.Shelves();
        int n = shelves.Count;
        var outIds = S.Library.CheckedOut().Select(t => t.Id).ToHashSet();
        R.Draw(_decor, Matrix4x4.Identity, Color.White, outlineWidth: 1.2f);
        R.Draw(_table, Matrix4x4.Identity, Color.White, outlineWidth: 1.4f);
        R.Draw(_returns, Matrix4x4.CreateTranslation(0, LiftOf("returns"), 0), Color.White, highlight: Rim("returns"), outlineWidth: 1.4f);
        R.Draw(_lampGlass, Matrix4x4.CreateTranslation(LampPos + new Vector3(0, -0.2f, 0)), Color.White, emissive: 1.4f, outline: false);

        foreach (var sh in shelves)
        {
            float x = CaseX(sh.Index, n), w = CaseW(n);
            var shelfId = "shelf:" + sh.Index;
            float lift = LiftOf(shelfId) * 0.3f;
            float fx = _settleFx.GetValueOrDefault(sh.Index);
            var gcol = GenreColor(sh.Genre);
            var rim = Rim(shelfId) ?? (fx > 0.01f ? Palette.Mul(gcol, fx * 0.8f) : null);
            R.Draw(_case, _caseFit * Matrix4x4.CreateTranslation(x, lift, CaseZ), Color.White, highlight: rim, outlineWidth: 1.4f);

            // the books, left (nearest the lamp) to right, scaled to fit the board
            var widths = sh.Tomes.Select(t => 0.085f + Hash01(t.Id, 1) * 0.06f).ToList();
            float inner = w - 0.26f, total = widths.Sum() + 0.012f * Math.Max(0, widths.Count - 1);
            float scale = total > inner ? inner / total : 1;
            float bx = x - w / 2 + 0.13f;
            for (int i = 0; i < sh.Tomes.Count; i++)
            {
                var t = sh.Tomes[i];
                float bw = widths[i] * scale, bh = 0.52f + Hash01(t.Id, 2) * 0.24f;
                float a = _arrive.GetValueOrDefault(t.Id, 1);
                float slide = (1 - Ease.OutCubic(a)) * 0.7f;
                float wob = a < 1 && !R.ReduceMotion ? MathF.Sin(a * 14) * (1 - a) * 0.18f : (Hash01(t.Id, 4) - 0.5f) * 0.03f;
                var col = CoverColor(t);
                if (outIds.Contains(t.Id)) { bx += bw + 0.012f * scale; continue; } // its gap waits for it
                var world = Matrix4x4.CreateScale(bw, bh, BookDepth) * Matrix4x4.CreateRotationZ(wob)
                            * Matrix4x4.CreateTranslation(bx + bw / 2, BoardY + lift, CaseZ + 0.04f + slide);
                R.Draw(_book, world, col, emissive: fx * 0.35f, outlineWidth: 0.9f);
                var band = Matrix4x4.CreateScale(bw, 0.035f, BookDepth) * Matrix4x4.CreateRotationZ(wob)
                           * Matrix4x4.CreateTranslation(bx + bw / 2, BoardY + lift + bh * 0.78f, CaseZ + 0.04f + slide);
                R.Draw(_band, band, Palette.Hex(0xD8B66A), outline: false);
                bx += bw + 0.012f * scale;
            }

            // the plaque: blank wood until tagged, then the genre's colour
            var tagId = "tag:" + sh.Index;
            float swing = _plaqueSwing.TryGetValue(sh.Index, out var sp) ? sp.Value : 0;
            var pw = Matrix4x4.CreateRotationX(swing * 0.25f) * Matrix4x4.CreateRotationZ(swing * 0.12f)
                     * Matrix4x4.CreateTranslation(x, CaseH - 0.02f + LiftOf(tagId) * 0.2f, CaseZ + CaseDepth / 2 + 0.06f);
            var pc = sh.Genre is null ? Palette.Hex(0xB59474) : gcol;
            R.Draw(_plaque, pw, pc, emissive: sh.Settled ? 0.25f : 0, highlight: Rim(tagId), outlineWidth: 1.2f);
            if (fx > 0.01f) R.Glow(new Vector3(x, BoardY + 0.4f, CaseZ + 0.4f), w * 1.3f * fx, Palette.Alpha(Palette.Lerp(gcol, Palette.LanternHot, 0.5f), 0.4f * fx));
        }

        // the pile: books lying flat, a little askew, and anything odd beside them
        var pile = S.Library.Pile();
        float pl = LiftOf("pile") * 0.3f;
        float wobble = _pileWobble.Value;
        // up to three leaning stacks; the book on top of the first stack is the one the card shows first
        const int perStack = 9;
        int show = Math.Min(pile.Count, perStack * 3);
        for (int i = 0; i < show; i++)
        {
            var t = pile[show - 1 - i];
            int stack = i / perStack, level = i % perStack;
            bool top = i == show - 1;
            float yaw = (Hash01(t.Id, 5) - 0.5f) * 0.5f + stack * 0.5f + (top ? wobble * 0.15f : 0);
            var at = PileAt + new Vector3(stack * 0.5f - (show > perStack ? 0.25f : 0), 0, stack == 1 ? -0.38f : stack == 2 ? 0.34f : 0);
            var world = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(at + new Vector3((Hash01(t.Id, 6) - 0.5f) * 0.05f, level * 0.1f + pl, 0));
            R.Draw(_flat, world, CoverColor(t), highlight: Rim("pile"), outlineWidth: 1.0f);
        }
        var odd = S.Library.PileItems();
        for (int i = 0; i < odd.Count; i++)
        {
            var def = S.World.Items.Def(odd[i]);
            var at = PileAt + new Vector3(0.9f + i * 0.28f, 0, 0.35f);
            float bob = R.ReduceMotion ? 0 : MathF.Sin(R.Time * 2 + i) * 0.02f;
            R.Draw(_token, Matrix4x4.CreateTranslation(at + new Vector3(0, bob, 0)), App.Art.ColorOf(def), emissive: odd[i].Essence is not null ? 0.25f : 0, outlineWidth: 0.9f);
            if (odd[i].Essence is not null) R.Glow(at + new Vector3(0, 0.2f, 0), 0.5f, Palette.Alpha(Palette.LanternHot, 0.35f + (R.ReduceMotion ? 0 : 0.15f * MathF.Sin(R.Time * 2.4f))));
        }
        // loaned books leave a card sticking out of the returns box
        if (S.Library.CheckedOut().Count > 0)
            R.Draw(_book, Matrix4x4.CreateScale(0.3f, 0.22f, 0.02f) * Matrix4x4.CreateTranslation(ReturnsPos + new Vector3(0, 0.72f + LiftOf("returns"), 0.05f)), Palette.Parchment, outlineWidth: 0.8f);

        // lamp and candle glows
        R.Glow(LampPos + new Vector3(0, -0.2f, 0.1f), 1.3f, Palette.Alpha(Palette.LanternHot, 0.6f));
        float f = R.ReduceMotion ? 1 : 0.85f + 0.15f * MathF.Sin(R.Time * 9.1f) * MathF.Sin(R.Time * 2.3f);
        R.Glow(CandleAt + new Vector3(0, 0.36f, 0), 0.32f * f, Palette.Alpha(Palette.LanternHot, 0.95f));
        R.Glow(CandleAt + new Vector3(0, 0.36f, 0), 0.9f * f, Palette.Alpha(Palette.Lantern, 0.35f));
    }

    private float LiftOf(string id) => HasSpot(id) ? SpotOf(id).Lift.Value : 0;

    // ───────────────────────── interaction ─────────────────────────

    protected override void OnSpot(string id)
    {
        if (id.StartsWith("shelf:", StringComparison.Ordinal))
        {
            int i = int.Parse(id[6..]);
            App.Audio.Play("slide", 0.35f, 1.2f);
            if (_card == Card.Shelf && _shelfCard == i) CloseCard();
            else { _shelfCard = i; OpenCard(Card.Shelf); }
            return;
        }
        if (id.StartsWith("tag:", StringComparison.Ordinal)) { ChooseTag(int.Parse(id[4..])); return; }
        switch (id)
        {
            case "pile":
                App.Audio.Play("paper", 0.5f);
                if (_card == Card.Pile) CloseCard(); else OpenCard(Card.Pile);
                break;
            case "returns": ChooseReturn(); break;
        }
    }

    private void OpenCard(Card c)
    {
        _card = c;
        _cardAge = 0;
        App.Audio.Play("page", 0.5f);
        Ui.ResetScroll("lib-shelf");
        Ui.ResetScroll("lib-pile");
    }

    private void CloseCard()
    {
        if (_card == Card.None) return;
        _card = Card.None;
        App.Audio.Play("slide", 0.35f);
    }

    public override bool Back()
    {
        if (_card != Card.None) { CloseCard(); return true; }
        return base.Back();
    }

    private void ChooseTag(int shelf)
    {
        if (!S.Library.Tagged) return;
        var shelves = S.Library.Shelves();
        var entries = S.Content.Config.Library.Tags.Where(S.Content.Genres.ContainsKey).Select(id =>
        {
            var g = S.Content.Genres[id];
            var on = shelves.FirstOrDefault(s => s.Genre == id);
            var sub = g.Sigil + (on is null ? "" : on.Index == shelf ? "  (already here)" : $"  (now on the {OrdinalShelf(on.Index)} shelf)");
            return new Picker<Choice>.Entry(new Choice(id, () => Tag(shelf, id)), g.Label, sub, Palette.Genre(g.Color));
        }).ToList();
        App.Open(new Picker<Choice>($"Hang which tag on the {OrdinalShelf(shelf)} shelf?", entries, c => c.Run(),
            "Moving a tag sends that shelf's books back to the pile."));
    }

    private void Tag(int shelf, string genre)
    {
        var r = S.Library.TagShelf(shelf, genre);
        if (_plaqueSwing.TryGetValue(shelf, out var s)) { s.Kick(R.ReduceMotion ? 0 : 4); _plaqueSwing[shelf] = s; }
        App.Audio.Play("clink", 0.6f);
        App.Audio.Play("pulse", 0.4f);
        App.Show(r);
    }

    private void Shelve(TomeResource tome)
    {
        var shelves = S.Library.Shelves();
        var entries = shelves.Select(sh => new Picker<Choice>.Entry(new Choice(sh.Id, () => ChooseSlot(tome, sh.Index)),
            $"The {OrdinalShelf(sh.Index)} shelf: {sh.Label}", $"{sh.Tomes.Count} book{(sh.Tomes.Count == 1 ? "" : "s")}", GenreColor(sh.Genre))).ToList();
        App.Open(new Picker<Choice>($"Shelve “{Short(tome)}” ({OrderKey(tome)}) where?", entries, c => c.Run()));
    }

    /// <summary>On a shelf of the right genre, choose where along it; the order is the puzzle.</summary>
    private void ChooseSlot(TomeResource tome, int shelf)
    {
        var sh = S.Library.Shelves()[shelf];
        if (sh.Genre != tome.Genre || sh.Tomes.Count == 0) { DoShelve(tome, shelf, null); return; }
        var entries = Enumerable.Range(0, sh.Tomes.Count + 1).Select(i =>
        {
            var label = i == 0 ? $"Nearest the lamp, before “{Short(sh.Tomes[0])}” ({OrderKey(sh.Tomes[0])})"
                : i == sh.Tomes.Count ? $"At the far end, after “{Short(sh.Tomes[i - 1])}” ({OrderKey(sh.Tomes[i - 1])})"
                : $"Between {OrderKey(sh.Tomes[i - 1])} and {OrderKey(sh.Tomes[i])}";
            return new Picker<Choice>.Entry(new Choice(label, () => DoShelve(tome, shelf, i)), label);
        }).ToList();
        App.Open(new Picker<Choice>($"Where along the shelf? (this one is {OrderKey(tome)})", entries, c => c.Run()));
    }

    private void DoShelve(TomeResource tome, int shelf, int? position)
    {
        var r = S.Library.Shelve(tome.Id, shelf, position);
        if (r.Ok)
        {
            App.Audio.Play("slide", 0.6f);
            App.Audio.Play("pulse", 0.45f);
            _settleFx[shelf] = MathF.Max(_settleFx.GetValueOrDefault(shelf), 0.35f);
            _pileAt = Math.Max(0, _pileAt - 1);
        }
        else _pileWobble.Kick(R.ReduceMotion ? 0 : 6);
        App.Show(r);
    }

    private void ChooseReturn()
    {
        var outs = S.Library.CheckedOut();
        if (outs.Count == 0) { App.Audio.Play("hm", 0.4f); App.Toasts.Add("", "Nothing on loan to you.", NoticeKind.Info, LineKind.Soft); return; }
        var entries = outs.Select(t => new Picker<Choice>.Entry(new Choice(t.Id, () =>
        {
            App.Audio.Play("slide", 0.5f);
            App.Show(S.Library.Return(t.Id));
        }), t.Title, $"{t.Author} · {OrderKey(t)}", CoverColor(t))).ToList();
        App.Open(new Picker<Choice>("Return which book?", entries, c => c.Run()));
    }

    /// <summary>Satchel objects: try them on a shelf, or hold them against a page.</summary>
    public override bool UseItem(ItemInstance item)
    {
        if (item.Essence is null && item.RemnantOf is null) return false;
        var entries = new List<Picker<Choice>.Entry>
        {
            new(new Choice("shelf", () => TryOnShelf(item)), "Try it on a shelf", "See whether a shelf will have it."),
            new(new Choice("page", () => HoldAgainst(item)), "Hold it against a page…", "Choose a book, then a page."),
        };
        App.Open(new Picker<Choice>(S.World.Items.Name(item), entries, c => c.Run(), S.World.Items.Description(item)));
        return true;
    }

    private void TryOnShelf(ItemInstance item)
    {
        var entries = S.Library.Shelves().Select(sh => new Picker<Choice>.Entry(new Choice(sh.Id, () =>
        {
            App.Audio.Play("slide", 0.45f, 0.9f);
            App.Show(S.Library.ShelveObject(item.Id, sh.Index), $"The {sh.Label} shelf");
        }), $"The {OrdinalShelf(sh.Index)} shelf: {sh.Label}", null, GenreColor(sh.Genre))).ToList();
        App.Open(new Picker<Choice>($"Try {S.World.Items.Name(item)} on which shelf?", entries, c => c.Run()));
    }

    private void HoldAgainst(ItemInstance item)
    {
        var books = S.Library.Shelves().SelectMany(s => s.Tomes).Concat(S.Library.Pile()).Concat(S.Library.CheckedOut())
            .DistinctBy(t => t.Id).OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList();
        var entries = books.Select(t => new Picker<Choice>.Entry(new Choice(t.Id, () => ChoosePage(item, t)), t.Title, $"{t.Author} · {OrderKey(t)}", CoverColor(t))).ToList();
        App.Open(new Picker<Choice>($"Hold {S.World.Items.Name(item)} against which book?", entries, c => c.Run()));
    }

    private void ChoosePage(ItemInstance item, TomeResource t)
    {
        var view = S.Library.Open(t.Id, record: false);
        if (view is null || view.Pages.Count == 0) return;
        var entries = view.Pages.Select(p => new Picker<Choice>.Entry(new Choice(p.Number.ToString(), () =>
        {
            App.Audio.Play("page", 0.6f);
            App.Show(S.Library.Compare(item.Id, t.Id, p.Number), t.Title);
        }), $"Page {p.Number}" + (p.Title is null ? "" : $": {p.Title}"), p.Text is null ? "faded ink" : p.Kind)).ToList();
        App.Open(new Picker<Choice>($"Which page of “{Short(t)}”?", entries, c => c.Run()));
    }

    // ───────────────────────── cards ─────────────────────────

    protected override void DrawRoomUI()
    {
        var name = App.Content.Locales.TryGetValue(LocaleId, out var l) ? l.Name : "The Library";
        var lib = S.Library;
        var settled = lib.Shelves().Count(s => s.Settled);
        var sub = $"{lib.TotalShelved} of {lib.TotalArrived} shelved  ·  {lib.Pile().Count} on the pile" + (settled > 0 ? $"  ·  {settled} {(settled == 1 ? "shelf" : "shelves")} settled" : "");
        var w = MathF.Max(Text.Measure(Ui.F.Hand, name, 44).X, Text.Measure(Ui.F.Italic, sub, 21).X) + 60;
        Raylib.DrawRectangleGradientH(0, 108, (int)w, 86, Palette.Alpha(Palette.Ink, 0.5f), Palette.Alpha(Palette.Ink, 0));
        Title(name, sub);

        if (_card == Card.None) { Tabs(); return; }
        float t = Ease.OutBack(MathF.Min(1, _cardAge / 0.35f), 1.3f);
        float a = MathF.Min(1, _cardAge / 0.2f);
        var r = new Rectangle(Desk.X - (1 - t) * 60, Desk.Y, 600, Desk.Height);
        if (_card == Card.Pile) PileCard(r, a);
        else if (_shelfCard < lib.Shelves().Count) ShelfCard(r, a, lib.Shelves()[_shelfCard]);
    }

    private void Tabs()
    {
        float x = 30, y = 200;
        float bw = Text.Measure(Ui.F.Body, "The pile", 21).X + 34;
        if (Ui.Button("lib-tab-pile", new Rectangle(x, y, bw, 38), "The pile", Accent, size: 21)) OpenCard(Card.Pile);
        x += bw + 10;
        foreach (var sh in S.Library.Shelves())
        {
            var label = GenreOf(sh.Genre)?.Label ?? $"Shelf {sh.Index + 1}";
            float w = Text.Measure(Ui.F.Body, label, 21).X + 34;
            if (x + w > 660) { x = 30; y += 46; }
            if (Ui.Button("lib-tab-" + sh.Index, new Rectangle(x, y, w, 38), label, Accent, size: 21)) { _shelfCard = sh.Index; OpenCard(Card.Shelf); }
            x += w + 10;
        }
    }

    private bool CloseButton(Rectangle r, string id)
    {
        bool c = Ui.Button(id, new Rectangle(r.X + r.Width - 58, r.Y + 18, 40, 38), "×", Accent, size: 30, tooltip: "Put the card aside (Esc)");
        if (c) CloseCard();
        return c;
    }

    private void PileCard(Rectangle r, float alpha)
    {
        var pile = S.Library.Pile();
        var odd = S.Library.PileItems();
        Ui.Panel(r, alpha, Accent);
        CloseButton(r, "lib-pile-close");
        float x = r.X + 32, y = r.Y + 22;
        Ui.Heading("The pile", new Vector2(x, y), 40);
        Ui.Rule(x, y + 56, 220, Accent);
        y += 72;
        Text.Draw(Ui.F.Italic, pile.Count == 0 ? "Empty. Every book is home." : $"{pile.Count} book{(pile.Count == 1 ? "" : "s")} waiting. The Quires keep the oldest nearest the lamp.",
            new Vector2(x, y), 19, Palette.InkSoft);
        y += 34;

        if (pile.Count > 0)
        {
            _pileAt = Math.Clamp(_pileAt, 0, pile.Count - 1);
            var t = pile[_pileAt];
            var cover = CoverColor(t);
            var book = new Rectangle(x, y, 74, 104);
            Raylib.DrawRectangleRounded(new Rectangle(book.X + 3, book.Y + 4, book.Width, book.Height), 0.1f, 4, Palette.Alpha(Palette.Ink, 0.25f));
            Raylib.DrawRectangleRounded(book, 0.1f, 4, cover);
            Raylib.DrawRectangle((int)book.X + 8, (int)book.Y + 6, 3, (int)book.Height - 12, Palette.Alpha(Palette.Parchment, 0.5f));
            Raylib.DrawRectangle((int)book.X + 18, (int)book.Y + 30, 46, 4, Palette.Hex(0xD8B66A));
            float tx = x + 94, tw = r.Width - 64 - 94;
            float ty = y - 4;
            foreach (var line in Text.Wrap(Ui.F.Hand, t.Title, 26, tw).Take(3)) { Text.Draw(Ui.F.Hand, line, new Vector2(tx, ty), 26, Palette.Ink); ty += 30; }
            Text.Draw(Ui.F.Italic, $"{t.Author}  ·  {OrderKey(t)}", new Vector2(tx, ty + 2), 19, Palette.InkSoft);
            y = MathF.Max(y + 112, ty + 30);
            foreach (var line in Text.Wrap(Ui.F.Italic, "Cover: " + t.Cover + ".", 18, r.Width - 64).Take(2)) { Text.Draw(Ui.F.Italic, line, new Vector2(x, y), 18, Palette.InkFaint); y += 22; }
            y += 4;
            foreach (var line in Text.Wrap(Ui.F.Body, t.Blurb, 20, r.Width - 64).Take(3)) { Text.Draw(Ui.F.Body, line, new Vector2(x, y), 20, Palette.InkSoft); y += 25; }
            y += 10;
            if (Ui.Button("lib-shelve", new Rectangle(x, y, 170, 44), "Shelve it…", Accent, size: 22)) Shelve(t);
            if (Ui.Button("lib-read", new Rectangle(x + 180, y, 130, 44), "Read it", size: 22)) App.Open(new ReaderOverlay(t.Id));
            if (Ui.Button("lib-prev", new Rectangle(r.X + r.Width - 172, y, 44, 44), "‹", size: 28, enabled: pile.Count > 1)) { _pileAt = (_pileAt - 1 + pile.Count) % pile.Count; App.Audio.Play("paper", 0.4f); }
            Text.DrawCentered(Ui.F.Italic, $"{_pileAt + 1} of {pile.Count}", new Vector2(r.X + r.Width - 98, y + 22), 18, Palette.InkFaint);
            if (Ui.Button("lib-next", new Rectangle(r.X + r.Width - 76, y, 44, 44), "›", size: 28, enabled: pile.Count > 1)) { _pileAt = (_pileAt + 1) % pile.Count; App.Audio.Play("paper", 0.4f); }
            y += 60;
        }

        if (odd.Count > 0)
        {
            Text.Draw(Ui.F.Hand, "And something that isn't a book", new Vector2(x, y), 25, Palette.Ink);
            y += 36;
            foreach (var it in odd)
            {
                var row = new Rectangle(x - 6, y, r.Width - 52, 52);
                Raylib.DrawRectangleRounded(row, 0.2f, 6, Palette.Alpha(Palette.Lantern, 0.14f));
                App.Art.Draw(S.World.Items.Def(it), new Vector2(row.X + 26, row.Y + 26), 40, it.Inert, Ui.Time);
                Text.Draw(Ui.F.Body, S.World.Items.Name(it), new Vector2(row.X + 54, row.Y + 14), 22, Palette.Ink);
                if (Ui.Button("lib-odd-try-" + it.Id, new Rectangle(row.X + row.Width - 252, row.Y + 8, 130, 36), "Try a shelf…", size: 19)) TryOnShelf(it);
                if (Ui.Button("lib-odd-take-" + it.Id, new Rectangle(row.X + row.Width - 114, row.Y + 8, 106, 36), "Take it", Accent, size: 19))
                {
                    App.Audio.Play("paper", 0.6f);
                    App.Show(S.Library.TakeFromPile(it.Id));
                }
                if (Ui.Hover(row)) Ui.Tooltip(S.World.Items.Description(it));
                y += 60;
            }
        }
    }

    private void ShelfCard(Rectangle r, float alpha, ShelfView sh)
    {
        var g = GenreOf(sh.Genre);
        Ui.Panel(r, alpha, sh.Genre is null ? Accent : GenreColor(sh.Genre));
        CloseButton(r, "lib-shelf-close");
        float x = r.X + 32, y = r.Y + 22;
        Ui.Heading(Cap(sh.Label), new Vector2(x, y), 38);
        Ui.Rule(x, y + 54, 220, GenreColor(sh.Genre));
        y += 68;
        var status = sh.Tomes.Count == 0 ? "Bare wood and a little dust."
            : sh.Settled ? "The shelf hums, settled. Every book where it belongs."
            : sh.InOrder ? "In order, oldest nearest the lamp." : "Something about the run of years looks off.";
        Text.Draw(Ui.F.Italic, (g is null ? "" : g.Sigil + ".  ") + status, new Vector2(x, y), 19, sh.Settled ? Palette.Moss : sh.InOrder ? Palette.InkSoft : Palette.Rose);
        if (S.Library.Tagged && Ui.Button("lib-tag", new Rectangle(r.X + r.Width - 210, r.Y + 22, 140, 38), g is null ? "Hang a tag…" : "Change tag…", Accent, size: 19))
            ChooseTag(sh.Index);
        y += 34;
        Text.Draw(Ui.F.Italic, "the lamp end", new Vector2(x, y), 17, Palette.InkFaint);
        y += 22;

        var outIds = S.Library.CheckedOut().Select(t => t.Id).ToHashSet();
        var view = new Rectangle(x - 6, y, r.Width - 52, r.Y + r.Height - y - 24);
        const float rowH = 50;
        float off = Ui.BeginScroll("lib-shelf", view, Math.Max(1, sh.Tomes.Count) * rowH);
        float yy = view.Y - off;
        if (sh.Tomes.Count == 0) Text.Draw(Ui.F.Italic, "Nothing here yet. Books come from the pile.", new Vector2(view.X + 8, yy + 10), 19, Palette.InkFaint);
        for (int i = 0; i < sh.Tomes.Count; i++)
        {
            var t = sh.Tomes[i];
            int at = i;
            var row = new Rectangle(view.X, yy, view.Width - 14, rowH - 6);
            bool loaned = outIds.Contains(t.Id);
            var sub = $"{OrderKey(t)}  ·  {t.Author}" + (loaned ? "  ·  on loan to you" : "");
            float btn = 4 * 46 + 92;
            if (Ui.Row("lib-row-" + t.Id, new Rectangle(row.X, row.Y, row.Width - btn, row.Height), Short(t), sub, CoverColor(t), size: 20)) App.Open(new ReaderOverlay(t.Id));
            float bx = row.X + row.Width - btn + 6;
            if (Ui.Button("lib-left-" + t.Id, new Rectangle(bx, row.Y + 5, 40, 34), "‹", size: 24, enabled: at > 0, tooltip: "Move it toward the lamp")) Reorder(sh.Index, at, at - 1);
            bx += 46;
            if (Ui.Button("lib-right-" + t.Id, new Rectangle(bx, row.Y + 5, 40, 34), "›", size: 24, enabled: at < sh.Tomes.Count - 1, tooltip: "Move it away from the lamp")) Reorder(sh.Index, at, at + 1);
            bx += 46;
            if (Ui.Button("lib-down-" + t.Id, new Rectangle(bx, row.Y + 5, 86, 34), "Take down", size: 17, tooltip: "Back onto the pile"))
            {
                App.Audio.Play("slide", 0.5f, 0.9f);
                App.Show(S.Library.TakeDown(sh.Index, at));
            }
            bx += 92;
            if (Ui.Button("lib-lend-" + t.Id, new Rectangle(bx, row.Y + 5, 86, 34), loaned ? "Return" : "Borrow", size: 17, tooltip: loaned ? "Slip it back into its gap" : "Carry it in your satchel"))
            {
                App.Audio.Play("paper", 0.6f);
                App.Show(loaned ? S.Library.Return(t.Id) : S.Library.CheckOut(t.Id));
            }
            yy += rowH;
        }
        Ui.EndScroll();
    }

    private void Reorder(int shelf, int from, int to)
    {
        var t = S.Library.Shelves()[shelf].Tomes[from];
        var r = S.Library.Reorder(shelf, from, to);
        _arrive[t.Id] = R.ReduceMotion ? 1 : 0.55f;
        App.Audio.Play("slide", 0.45f, 1.15f);
        App.Show(r);
    }

    // ───────────────────────── words ─────────────────────────

    /// <summary>What the shelf's order is judged by (the content decides: year, title or author).</summary>
    private string OrderKey(TomeResource t) => S.Content.Config.Library.OrderBy switch
    {
        "title" => $"“{Short(t)}”",
        "author" => t.Author,
        _ => $"SR {t.Year}",
    };

    private static string Short(TomeResource t)
    {
        var colon = t.Title.IndexOf(':');
        var s = colon > 0 ? t.Title[..colon] : t.Title;
        return s.Length > 38 ? s[..37] + "…" : s;
    }

    private static readonly string[] Ordinals = { "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth" };
    private static string OrdinalShelf(int i) => i < Ordinals.Length ? Ordinals[i] : $"{i + 1}th";

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
