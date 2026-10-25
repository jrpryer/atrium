using Atrium.Core.Content;
using Atrium.Core.Runtime;

namespace Atrium.Core.Story;

/// <summary>
/// Applies resolution and story payloads back into the minigames — lore to the Library, ingredients to the
/// Apothecary, minerals/auras to the configured pools container — by signal, never by direct reference.
/// </summary>
public sealed class SanctumRouter
{
    private readonly World _w;
    public SanctumRouter(World w)
    {
        _w = w;
        w.Bus.Subscribe<ObjectResolved>(e => Apply(w.Content.Objects[e.Object].Resolution.Payload));
    }

    public void Apply(Payload p)
    {
        if (p.IsEmpty) return;
        if (p.Tomes.Count > 0) _w.Bus.Publish(new TomesDelivered(p.Tomes));
        if (p.Pages.Count > 0) _w.Bus.Publish(new PagesDelivered(p.Pages));
        if (p.Ingredients.Count > 0) _w.Bus.Publish(new IngredientsDelivered(p.Ingredients));

        if (p.Minerals.Count > 0)
        {
            var dest = _w.Config.MineralDestination == "satchel" ? Locations.Satchel : Locations.DruidsBag;
            foreach (var m in p.Minerals) _w.Items.Create(m, dest);
            var names = string.Join(", ", p.Minerals.Select(_w.Content.ItemName));
            _w.Notify(NoticeKind.Info, "For the Spirit Pools", $"{names} — {_w.Items.DescribeLocation(dest)}.");
            _w.Log.Add(LogKind.Info, $"Delivered {names} {_w.Items.DescribeLocation(dest)}.", locale: "pools");
        }
        foreach (var g in p.Items)
        {
            _w.Items.Create(g.Item, g.Location, g.Quantity);
            _w.Notify(NoticeKind.Info, "Something new", $"{_w.Content.ItemName(g.Item)} — {_w.Items.DescribeLocation(g.Location)}.");
        }
        foreach (var f in p.Flags) _w.State.Flags.Add(f);
    }
}
