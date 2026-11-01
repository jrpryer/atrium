using System.Numerics;
using Atrium.Diorama.Render;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// The forgotten corner's dressing, from the corner kit (art/models/corner-kit.glb, built by
/// tools/blender/assets/corner_kit.py): crumbling facades on the pocket's walls, the cracked brick arch the falls burst
/// from, flagstones heaved by Old Stoic's roots, rubble and fallen coping, ivy hanging from the rims, moss, ferns and
/// grass at the feet of the walls and along their tops, puddles, and rivulets trickling down the stone. Everything is
/// baked into a few meshes, like the city.
/// </summary>
internal static class HubCorner
{
    public sealed class Parts
    {
        /// <summary>Weathered stone (the corner's wall texture), the root-heaved flagstones (its path texture), plain colours.</summary>
        public readonly MeshBuilder Stone = new(), Flags = new(), Upper = new();
        /// <summary>Standing water (drawn as water) and water running down the walls (drawn as falling water).</summary>
        public readonly MeshBuilder Puddles = new(), Rivulets = new();
    }

    /// <param name="blocked">Ground the dressing keeps off: the houses, the stream, the pool, the trunk, the lamps.</param>
    public static Parts Build(KitAsset kit, Func<Vector2, bool> blocked)
    {
        var p = new Parts();
        var rng = new Random(57);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float Facing(Vector3 n) => MathF.Atan2(n.X, n.Z);
        bool Free(Vector3 at) => !blocked(new Vector2(at.X, at.Z)) && !HubCity.OnStairs(new Vector2(at.X, at.Z));

        void Put(string name, Vector3 at, float yaw, Vector3? scale = null)
        {
            if (kit[name] is not { } piece) return;
            var m = Matrix4x4.CreateTranslation(-piece.Origin) * Matrix4x4.CreateScale(scale ?? Vector3.One)
                  * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(at);
            foreach (var (role, mesh, colour) in piece.Meshes)
                switch (role)
                {
                    case "stone" when name.StartsWith("flags-"): p.Flags.AppendMesh(mesh, m, HubCity.StoneTint); break;
                    case "stone": p.Stone.AppendMesh(mesh, m, HubCity.StoneTint); break;
                    case "water": p.Puddles.AppendMesh(mesh, m, Color.White); break;
                    case "trickle": p.Rivulets.AppendMesh(mesh, m, Color.White, q => new Vector2(0.5f, 0.3f + Math.Clamp((4 - q.Y) / 16, 0, 0.3f))); break;
                    default: p.Upper.AppendMesh(mesh, m, colour); break;
                }
        }

        // the pocket's walls, faced with crumbling sections: (from, to, outward normal)
        var runs = new (Vector3 A, Vector3 B, Vector3 N)[]
        {
            (new(HubCity.West, 0, HubCity.CornerC.Y), new(HubCity.West, 0, HubCity.Front), Vector3.UnitX),
            (new(HubCity.CornerC.X, 0, HubCity.North), new(-2.6f, 0, HubCity.North), Vector3.UnitZ),
            (new(6.4f, 0, HubCity.North), new(HubCity.East, 0, HubCity.North), Vector3.UnitZ),
            (new(HubCity.East, 0, HubCity.GateZ + 1.7f), new(HubCity.East, 0, HubCity.Front), -Vector3.UnitX),
        };
        int k = 0;
        foreach (var (a, b, n) in runs)
        {
            float len = Vector3.Distance(a, b);
            int count = Math.Max(1, (int)MathF.Round(len / 4));
            for (int i = 0; i < count; i++)
                Put(k++ % 2 == 0 ? "wall-crumble-a" : "wall-crumble-b", Vector3.Lerp(a, b, (i + 0.5f) / count), Facing(n), new Vector3(len / count / 4, 1, 1));
        }

        // the cracked arch the falls burst from (its hole sits where the falls begin), in the baths' back wall
        Put("brick-arch", new Vector3(HubWorld.FallsBase.X, 2.7f, HubCity.North), 0);

        // flagstones heaved by Old Stoic's roots, the roots running out from the trunk
        foreach (float deg in new[] { 0f, -75f, 75f })
        {
            float t = deg * MathF.PI / 180;
            var at = HubWorld.TreePos + new Vector3(MathF.Sin(t), 0, MathF.Cos(t)) * 4.6f;   // over the paving ring, which they heave
            if (Free(at)) Put(deg == 0 ? "flags-roots-b" : "flags-roots-a", at, t);
        }

        // rubble and fallen coping at the feet of the walls
        foreach (var (name, at, n) in new (string, Vector3, Vector3)[]
                 {
                     ("rubble-a", new(-10.0f, 0, -2.2f), Vector3.UnitX), ("rubble-a", new(12.1f, 0, 3.4f), -Vector3.UnitX),
                     ("coping-fallen", new(-9.9f, 0, 2.6f), Vector3.UnitX), ("coping-fallen", new(6.0f, 0, -8.8f), Vector3.UnitZ),
                 })
            if (Free(at)) Put(name, at, Facing(n) + R(-0.4f, 0.4f));

        // ivy hanging from the rims, the gallery, the baths' back wall and the foot of the city wall
        foreach (var (name, at, n) in new (string, Vector3, Vector3)[]
                 {
                     ("ivy-curtain-a", new(HubCity.West, HubCity.Tier1 + 0.1f, -3.0f), Vector3.UnitX),
                     ("ivy-curtain-b", new(HubCity.West, HubCity.Tier1 + 0.1f, 2.2f), Vector3.UnitX),
                     ("ivy-curtain-a", new(-4.0f, HubCity.Tier1 + 0.1f, HubCity.North), Vector3.UnitZ),
                     ("ivy-curtain-b", new(-1.6f, 11.6f, HubCity.North), Vector3.UnitZ),
                     ("ivy-curtain-a", new(4.9f, 8.8f, HubCity.North), Vector3.UnitZ),
                     ("ivy-curtain-b", new(9.0f, HubCity.Tier1 + 0.1f, HubCity.North), Vector3.UnitZ),
                     ("ivy-curtain-a", new(HubCity.East, HubCity.Tier1 + 0.1f, 1.2f), -Vector3.UnitX),
                     ("ivy-curtain-b", new(11.1f, 3.9f, -6.6f), -Vector3.UnitX),
                     ("ivy-curtain-a", new(HubCity.East, 8.7f, HubCity.WaterGateZ - 1.4f), -Vector3.UnitX),
                 })
            Put(name, at, Facing(n));

        // rivulets down the stone, each ending in a little puddle
        foreach (var (at, n, h) in new (Vector3, Vector3, float)[]
                 {
                     (new(9.2f, 0, HubCity.North), Vector3.UnitZ, 1), (new(5.0f, 0, HubCity.North), Vector3.UnitZ, 2.1f),
                     (new(HubCity.West, 0, -0.5f), Vector3.UnitX, 1), (new(HubCity.East, 0, 2.6f), -Vector3.UnitX, 1),
                     (new(11.6f, 0, HubCity.North), Vector3.UnitZ, 1),
                 })
            Put("rivulet-a", at, Facing(n), new Vector3(1, h, 1));

        // puddles where the paving has sunk
        foreach (var at in new Vector3[] { new(-3.5f, 0, 8.6f), new(2.4f, 0, 9.6f), new(-8.0f, 0, 6.8f), new(-2.6f, 0, -6.6f), new(9.0f, 0, 8.2f), new(-6.6f, 0, 10.6f) })
            if (Free(at)) Put(rng.NextDouble() < 0.5 ? "puddle-a" : "puddle-b", at, R(0, MathF.Tau));

        // moss, ferns and grass: along the feet of the walls, on the rims above, round the trunk and the pool
        string Green() => rng.NextDouble() switch { < 0.35 => "moss-a", < 0.6 => "moss-b", < 0.8 => "fern-a", _ => "tuft-a" };
        foreach (var (a, b, n) in runs.Append((new Vector3(HubCity.East, 0, HubCity.North), new Vector3(HubCity.East, 0, HubCity.GateZ - 1.7f), -Vector3.UnitX)))
        {
            float len = Vector3.Distance(a, b);
            for (float s = R(0.2f, 0.8f); s < len; s += R(0.6f, 1.2f))
            {
                var at = Vector3.Lerp(a, b, s / len) + n * R(0.25f, 0.75f);
                if (Free(at)) Put(Green(), at, R(0, MathF.Tau), new Vector3(R(0.8f, 1.2f)));
            }
            for (float s = R(0.5f, 1.5f); s < len; s += R(1.2f, 2.2f))   // on the rim's top, back from the edge
                Put(rng.NextDouble() < 0.6 ? "tuft-a" : "moss-a", Vector3.Lerp(a, b, s / len) - n * R(0.3f, 0.9f) + new Vector3(0, HubCity.Tier1 + 0.2f, 0), R(0, MathF.Tau));
        }
        for (int i = 0; i < 12; i++)
        {
            float t = MathF.Tau * i / 12 + R(-0.2f, 0.2f);
            var at = HubWorld.TreePos + new Vector3(MathF.Cos(t), 0, MathF.Sin(t)) * R(1.5f, 2.4f);
            if (Free(at)) Put(rng.NextDouble() < 0.5 ? "fern-a" : "moss-b", at, R(0, MathF.Tau));
        }
        for (int i = 0; i < 10; i++)
        {
            float t = MathF.Tau * i / 10 + R(-0.2f, 0.2f);
            var at = new Vector3(1.8f + MathF.Cos(t) * 3.4f, 0, -6.1f + MathF.Sin(t) * 2.7f);
            if (Free(at)) Put(Green(), at, R(0, MathF.Tau));
        }
        return p;
    }
}
