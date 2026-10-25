using Atrium.Core.Content;
using Atrium.Core.Mystery;
using Atrium.Core.Runtime;

namespace Atrium.Core.Locales;

/// <summary>Shared plumbing for locale modules. Locales know the World and the lens service — never each other.</summary>
public abstract class LocaleBase
{
    protected readonly World W;
    protected readonly ILensService Lens;

    protected LocaleBase(World world, ILensService lens) { W = world; Lens = lens; }

    /// <summary>The locale id this module implements (matches a <see cref="LocaleResource"/>).</summary>
    public abstract string Id { get; }

    public bool Unlocked => W.State.UnlockedLocales.Contains(Id);

    protected ContentDatabase C => W.Content;

    /// <summary>Generic "nothing new" text when an object reveals nothing: item response, then locale fallback.</summary>
    protected string Fallback(ItemInstance? item, string verb, string? tool = null)
    {
        if (item is not null)
        {
            var def = W.Items.Def(item);
            if (tool is not null && def.Responses.TryGetValue($"{Id}.{verb}:{tool}", out var r1)) return r1;
            if (def.Responses.TryGetValue($"{Id}.{verb}", out var r2)) return r2;
        }
        if (C.Locales.TryGetValue(Id, out var loc))
        {
            if (tool is not null && loc.Fallback.TryGetValue($"{verb}:{tool}", out var f1)) return f1;
            if (loc.Fallback.TryGetValue(verb, out var f2)) return f2;
        }
        return "Nothing about it surprises you.";
    }

    /// <summary>Runs the lens; if nothing matched, adds the fallback.</summary>
    protected ActionResult Observe(ActionResult result, LensEvent e, string? fallbackTool = null)
    {
        var lines = Lens.Observe(e);
        if (lines.Count == 0) result.Add(LineKind.Narration, Fallback(e.Item, e.Verb, fallbackTool ?? e.Tool));
        else result.AddRange(lines);
        return result;
    }

    protected ActionResult Done(ActionResult r)
    {
        W.Changed();
        return r;
    }

    protected ItemInstance? Carried(string instanceId)
    {
        var i = W.Items.Get(instanceId);
        return i is not null && (i.Location == Locations.Satchel || i.Location == Locations.SanctumCabinet && Id == "sanctum") ? i : null;
    }
}
