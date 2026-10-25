namespace Atrium.Core.Content;

// Designer-authored content. These are plain data classes so they can be loaded from JSON/Markdown here and
// mirrored as Godot [GlobalClass] Resources (or ScriptableObjects) by a future engine adapter without changes.

// ───────────────────────────── Taxonomy (shared ontology, invariant 11) ─────────────────────────────

public sealed class TaxonomyResource
{
    /// <summary>Designation facets on the Sanctum's designation card ("What it is", "Where it came from", …).</summary>
    public List<FacetResource> Facets { get; set; } = new();
    /// <summary>Earnable designation terms, each scoped to one facet.</summary>
    public List<TermResource> Terms { get; set; } = new();
    /// <summary>Library classifications (genres / shelving sigils).</summary>
    public List<GenreResource> Genres { get; set; } = new();
    /// <summary>Item traits shared by every locale (recipes, seals, pool transforms, derivations read them).</summary>
    public List<TraitResource> Traits { get; set; } = new();
    /// <summary>Energy axes. Exactly two are used by the Pools: <c>light</c> and <c>heat</c>.</summary>
    public List<AxisResource> Axes { get; set; } = new();
    /// <summary>Capabilities that tools grant, with a player-facing description.</summary>
    public List<CapabilityResource> Capabilities { get; set; } = new();
}

public sealed class FacetResource
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Prompt { get; set; } = "";
}

public sealed class TermResource
{
    public string Id { get; set; } = "";
    public string Facet { get; set; } = "";
    public string Label { get; set; } = "";
    public string Gloss { get; set; } = "";
}

public sealed class GenreResource
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Sigil { get; set; } = "";
    /// <summary>Presentation hint only (e.g. "green"); the engine never reads it.</summary>
    public string Color { get; set; } = "";
}

public sealed class TraitResource
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Gloss { get; set; } = "";
}

public sealed class AxisResource
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string NegativeLabel { get; set; } = "";
    public string PositiveLabel { get; set; } = "";
    public int Min { get; set; } = -5;
    public int Max { get; set; } = 5;
}

public sealed class CapabilityResource
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
}

// ───────────────────────────── Items ─────────────────────────────

public enum ItemKind { Ingredient, Food, Mineral, Aura, Reagent, ClueForm, Remnant, Tool, Misc }

/// <summary>
/// Any thing that can exist in the world: Apothecary elements (this is the <c>IngredientResource</c> of the
/// brief, unified so elements can also be bottled and carried), foods, minerals/auras, clue-object forms,
/// remnants and assembled tools.
/// </summary>
public sealed class ItemResource
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ItemKind Kind { get; set; } = ItemKind.Misc;
    public List<string> Traits { get; set; } = new();
    /// <summary>What this contributes to a pool's energy while it sits in the water.</summary>
    public Energy? PoolEnergy { get; set; }
    /// <summary>Hunger relief when fed to pool lifeforms; 0 = not food.</summary>
    public int FoodValue { get; set; }
    /// <summary>Elements must be portable to be bottled into the satchel.</summary>
    public bool Portable { get; set; } = true;
    public string? InertName { get; set; }
    public string? InertDescription { get; set; }
    /// <summary>Optional flavour responses keyed <c>locale.verb</c> or <c>locale.verb:tool</c>, used when no reading matches.</summary>
    public Dictionary<string, string> Responses { get; set; } = new();

    public bool IsUnique => Kind is ItemKind.ClueForm or ItemKind.Remnant or ItemKind.Tool;
}

// ───────────────────────────── Library ─────────────────────────────

public sealed class TomeResource
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int Year { get; set; }
    public string Genre { get; set; } = "";
    public string Cover { get; set; } = "";
    public string Blurb { get; set; } = "";
    public string? Hint { get; set; }
    public Condition Available { get; set; } = Condition.True;
    public List<PageResource> Pages { get; set; } = new();
    public string SourceFile { get; set; } = "";
}

public sealed class PageResource
{
    /// <summary>1-based page number in file order.</summary>
    public int Number { get; set; }
    public string Kind { get; set; } = "prose";
    public string? Title { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Only readable once the Library's faded-page condition holds (e.g. by gilded lamplight).</summary>
    public bool Faded { get; set; }
    /// <summary>Not part of the tome until delivered by a payload (<c>tome#page</c>).</summary>
    public bool Loose { get; set; }

    public bool Verbatim => Kind is "poetry" or "art" or "table" or "list" or "recipe";
}

public sealed class LibraryConfig
{
    /// <summary><c>tagged</c>: empty shelves the player tags with genres. <c>prelabeled</c>: shelves arrive labelled.</summary>
    public string ShelvingModel { get; set; } = "tagged";
    public int ShelfCount { get; set; } = 5;
    public List<ShelfResource> Shelves { get; set; } = new();
    /// <summary>Genre tags available to place (tagged model).</summary>
    public List<string> Tags { get; set; } = new();
    public int ShelfCapacity { get; set; } = 14;
    /// <summary><c>year</c>, <c>title</c>, <c>author</c> or <c>none</c>.</summary>
    public string OrderBy { get; set; } = "year";
    public Condition FadedRevealWhen { get; set; } = new() { Always = false };
    public int CheckoutLimit { get; set; } = 1;
    public int MinPages { get; set; } = 1;
    public int MaxPages { get; set; } = 3;
    public string Intro { get; set; } = "";
}

public sealed class ShelfResource
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Genre { get; set; }
}

// ───────────────────────────── Apothecary ─────────────────────────────

/// <summary>An element-to-element discovery: an order-independent multiset of known elements, plus an optional tool.</summary>
public sealed class RecipeResource
{
    public string Id { get; set; } = "";
    public List<string> Inputs { get; set; } = new();
    public string? Tool { get; set; }
    public string Output { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class ApothecaryConfig
{
    public List<string> StartingIngredients { get; set; } = new();
    public int MaxInputs { get; set; } = 3;
    /// <summary>Insight gained per new (untried) failed mix.</summary>
    public int InsightPerTry { get; set; } = 1;
    /// <summary>Insight needed to reveal one hidden ingredient on the Discovery Map.</summary>
    public int InsightToReveal { get; set; } = 3;
    public string Intro { get; set; } = "";
}

/// <summary>A riddling entry in the Apothecary's Old Formulary (or any locale's reference book).</summary>
public sealed class FormularyEntry
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public Condition AvailableWhen { get; set; } = Condition.True;
}

// ───────────────────────────── Tools ─────────────────────────────

public enum ToolKind { Fixed, Assembled }

/// <summary>
/// A locale tool. <b>Fixed</b> tools are fixtures of a locale (the mortar, the rake) available while
/// <see cref="AvailableWhen"/> holds. <b>Assembled</b> tools are built at the Sanctum from components and
/// exist as items; if a component carries an essence, the tool contains it (and goes inert on reconstitution).
/// </summary>
public sealed class ToolResource
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>The locale the tool is used in (fixed) or assembled at (assembled: always the Sanctum).</summary>
    public string Locale { get; set; } = "";
    public ToolKind Kind { get; set; } = ToolKind.Fixed;
    /// <summary>Short verb shown in menus ("grind", "weigh", "warm").</summary>
    public string Verb { get; set; } = "";
    public Condition AvailableWhen { get; set; } = Condition.True;
    public List<ComponentSlot> Components { get; set; } = new();
    public List<string> Grants { get; set; } = new();
    public List<string> Traits { get; set; } = new();
    public Energy? PoolEnergy { get; set; }
    public string AssembleText { get; set; } = "";
    public string? InertName { get; set; }
    public string? InertDescription { get; set; }
}

public sealed class ComponentSlot
{
    /// <summary>A specific item id…</summary>
    public string? Item { get; set; }
    /// <summary>…or any item carrying this trait.</summary>
    public string? Trait { get; set; }
    public string Label { get; set; } = "";
}

// ───────────────────────────── Essence / derivations (invariant 2) ─────────────────────────────

/// <summary>
/// A sanctioned transformation of an essence-carrying form into a new form. Every derivation is a
/// <b>containment</b> edge: the essence moves into <see cref="Produces"/>, and <see cref="Remnant"/> (if any)
/// is left behind for the Reconstitution Circle. Catalysis edges are recorded at runtime and declared in
/// <see cref="CatalysisResource"/> for documentation and validation.
/// </summary>
public sealed class DerivationResource
{
    public string Id { get; set; } = "";
    public string Locale { get; set; } = "";
    public string? Tool { get; set; }
    /// <summary>The specific input form…</summary>
    public string? Input { get; set; }
    /// <summary>…or any essence-carrying form with this trait.</summary>
    public string? InputTrait { get; set; }
    /// <summary>Elements used alongside (exact multiset).</summary>
    public List<string> With { get; set; } = new();
    public string Produces { get; set; } = "";
    public string? Remnant { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Shown before the player commits; should say what will be left behind.</summary>
    public string Confirm { get; set; } = "";
    public FormularyEntry? Formulary { get; set; }
}

public sealed class CatalysisResource
{
    public string From { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string Note { get; set; } = "";
}

// ───────────────────────────── Spirit Pools ─────────────────────────────

public sealed class PoolResource
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Energy BaseEnergy { get; set; }
    public int Capacity { get; set; } = 8;
    public int StartingClarity { get; set; } = 60;
    public bool SpawningPond { get; set; }
    public List<StockEntry> StartingStock { get; set; } = new();
    /// <summary>Things a cleanse can turn up, once each, when the cleanser has the required capability.</summary>
    public List<SiltFind> SiltFinds { get; set; } = new();
}

public sealed class StockEntry
{
    public string Lifeform { get; set; } = "";
    /// <summary><c>female</c>, <c>male</c> or empty for unsexed species.</summary>
    public string? Sex { get; set; }
    public int Count { get; set; } = 1;
}

public sealed class SiltFind
{
    public string Item { get; set; } = "";
    public string? Capability { get; set; }
    public int MinCleanses { get; set; } = 1;
    public string Text { get; set; } = "";
}

public enum LifeformKind { Fish, Snail, Plant, Mote }

public sealed class LifeformResource
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public LifeformKind Kind { get; set; } = LifeformKind.Fish;
    /// <summary>True when individuals are female/male; false for hermaphrodites, plants and motes.</summary>
    public bool Sexed { get; set; } = true;
    public Band Survive { get; set; } = new();
    public Band Thrive { get; set; } = new();
    public Energy Emits { get; set; }
    /// <summary>Hunger gained per tick (0 = never needs feeding).</summary>
    public double HungerPerTick { get; set; }
    /// <summary>Clarity lost per tick; negative values clean the water.</summary>
    public double WastePerTick { get; set; }
    /// <summary>Whether same-species pairs breed true by default.</summary>
    public bool Breeds { get; set; } = true;
    public int GestationTicks { get; set; } = 120;
    public int CooldownTicks { get; set; } = 240;
    public List<string> Traits { get; set; } = new();
}

/// <summary>
/// A breeding rule. <see cref="Ordered"/> pairs need <see cref="ParentA"/> as the mother and
/// <see cref="ParentB"/> as the father; unordered pairs match either way round (open question 13 stays open in data).
/// </summary>
public sealed class BreedingResource
{
    public string Id { get; set; } = "";
    public string ParentA { get; set; } = "";
    public string ParentB { get; set; } = "";
    public bool Ordered { get; set; }
    public PoolRequirement? Requires { get; set; }
    public List<OffspringEntry> Offspring { get; set; } = new();
    /// <summary>The first brood of a never-seen offspring species is that species (so discoveries never hinge on luck).</summary>
    public bool GuaranteeNewFirst { get; set; } = true;
    public string Text { get; set; } = "";
}

public sealed class OffspringEntry
{
    public string Lifeform { get; set; } = "";
    public int Weight { get; set; } = 1;
}

public sealed class PoolRequirement
{
    public IntRange Light { get; set; } = IntRange.Everything;
    public IntRange Heat { get; set; } = IntRange.Everything;
    public int ClarityMin { get; set; }
    /// <summary>Items with these traits (non-inert) must be in the pool.</summary>
    public List<string> ReagentTraits { get; set; } = new();
    /// <summary>These specific items must be in the pool.</summary>
    public List<string> Reagents { get; set; } = new();
    public List<LifeformCount> Lifeforms { get; set; } = new();
}

public sealed class LifeformCount
{
    public string Lifeform { get; set; } = "";
    public int Min { get; set; } = 1;
}

/// <summary>A timed transformation applied to an item soaking in a pool that meets <see cref="Requires"/>.</summary>
public sealed class PoolTransformResource
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>The item form that is transformed.</summary>
    public string Target { get; set; } = "";
    public PoolRequirement Requires { get; set; } = new();
    /// <summary>Base tick cost (data, never a constant in code — open question 5).</summary>
    public int Ticks { get; set; } = 300;
    public List<TickModifier> Modifiers { get; set; } = new();
    /// <summary>Form the target becomes (same essence; no remnant). Null = reading only.</summary>
    public string? TransformInto { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Shown while the soak is in progress.</summary>
    public string ProgressText { get; set; } = "";
}

public sealed class TickModifier
{
    public string Lifeform { get; set; } = "";
    public int Min { get; set; } = 1;
    public int DeltaTicks { get; set; }
}

/// <summary>A mineral or aura laid in a spawning pond spawns motes over time (open question 3: semantics kept minimal).</summary>
public sealed class SpawnRuleResource
{
    public string Id { get; set; } = "";
    public string Mineral { get; set; } = "";
    public string Spawns { get; set; } = "";
    public int EveryTicks { get; set; } = 90;
    public int MaxPresent { get; set; } = 3;
    public string Text { get; set; } = "";
}

public sealed class PoolsConfig
{
    public int CleanseAmount { get; set; } = 35;
    public string BasicFoodName { get; set; } = "pond flakes";
    public int BasicFoodValue { get; set; } = 45;
    public int ThriveHungerMax { get; set; } = 55;
    public int ContentHungerMax { get; set; } = 85;
    public int ThriveClarityMin { get; set; } = 50;
    public int ContentClarityMin { get; set; } = 20;
    public string HoldingName { get; set; } = "the holding crock";
    public List<string> Names { get; set; } = new();
    public string Intro { get; set; } = "";
}

// ───────────────────────────── Sanctum ─────────────────────────────

/// <summary>A lock that opens to the right object (e.g. the Stillkeepers' Balance). Offering an object never consumes it.</summary>
public sealed class SealResource
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Locale { get; set; } = "sanctum";
    public string Inscription { get; set; } = "";
    public Condition AvailableWhen { get; set; } = Condition.True;
    public List<string> RequiresTraits { get; set; } = new();
    public string? RequiresItem { get; set; }
    public string SolvedText { get; set; } = "";
    public string RefuseText { get; set; } = "";
    public Payload Payload { get; set; } = new();
}

// ───────────────────────────── Mystery spine ─────────────────────────────

public sealed class MysteryObjectResource
{
    /// <summary>Also the id of the object's essence.</summary>
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>The original form (item id) the Circle restores.</summary>
    public string BaseForm { get; set; } = "";
    public string Summary { get; set; } = "";
    public ArrivalResource Arrival { get; set; } = new();
    /// <summary>Facets used on this object's designation card (default: all taxonomy facets).</summary>
    public List<string> Facets { get; set; } = new();
    public List<ReadingResource> Readings { get; set; } = new();
    public List<ClaimResource> Claims { get; set; } = new();
    public ResolutionResource Resolution { get; set; } = new();
    /// <summary>Growing/breeding objects are exempt from the one-instance assertion.</summary>
    public bool ExemptFromOneInstance { get; set; }
    public List<string> ExtraRelevantTerms { get; set; } = new();
    /// <summary>Items (forms, remnants) owned by this object, authored alongside it for convenience.</summary>
    public List<ItemResource> Forms { get; set; } = new();
    public List<DerivationResource> Derivations { get; set; } = new();
    public List<CatalysisResource> Catalysis { get; set; } = new();
}

public sealed class ArrivalResource
{
    public Condition When { get; set; } = Condition.True;
    public string Location { get; set; } = Locations.Satchel;
    public string Text { get; set; } = "";
    /// <summary>A reading surfaced the moment the object arrives (where it was found is evidence too).</summary>
    public string? Reading { get; set; }
}

/// <summary>One piece of evidence an object yields through a locale's own mechanics.</summary>
public sealed class ReadingResource
{
    public string Id { get; set; } = "";
    public string Locale { get; set; } = "";
    public LensTrigger Trigger { get; set; } = new();
    /// <summary>Restrict to one form of the essence (item id); null = any form.</summary>
    public string? Form { get; set; }
    public Condition? Requires { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Taxonomy terms earned for the designation card.</summary>
    public List<string> Grants { get; set; } = new();
    /// <summary>Used by the unresolved-object funnel at "strong" strength.</summary>
    public string FunnelHint { get; set; } = "";
}

/// <summary>
/// Matches a lens event. <see cref="Verb"/> must match. <see cref="Tool"/> and <see cref="With"/> are exact
/// (null/empty means "none"; tool "*" means any). The other fields are wildcards when unset.
/// </summary>
public sealed class LensTrigger
{
    public string Verb { get; set; } = "";
    public string? Tool { get; set; }
    public List<string> With { get; set; } = new();
    public string? Shelf { get; set; }
    public string? Tome { get; set; }
    public int? Page { get; set; }
    public string? Pool { get; set; }
    public IntRange? Light { get; set; }
    public IntRange? Heat { get; set; }
    public string? Seal { get; set; }
    public string? Transform { get; set; }
    public string? Derivation { get; set; }
}

public enum ClaimKind { True, PlausibleWrong }

/// <summary>A synthesis target: a designation (one term per facet) and the evidence that permits attempting it.</summary>
public sealed class ClaimResource
{
    public string Id { get; set; } = "";
    public ClaimKind Kind { get; set; } = ClaimKind.PlausibleWrong;
    public Dictionary<string, string> Designation { get; set; } = new();
    public Condition Requires { get; set; } = Condition.True;
    /// <summary>The unique, diegetic discrepancy for a plausible-wrong claim (given once).</summary>
    public string NearMiss { get; set; } = "";
}

public sealed class ResolutionResource
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public Payload Payload { get; set; } = new();
}

// ───────────────────────────── Story, locales, hub ─────────────────────────────

public sealed class StoryBeatResource
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public Condition When { get; set; } = new() { Always = false };
    public Payload Payload { get; set; } = new();
    public int Order { get; set; }
}

public sealed class LocaleResource
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string LockedDescription { get; set; } = "";
    public Condition UnlockWhen { get; set; } = Condition.True;
    public string UnlockText { get; set; } = "";
    public string Intro { get; set; } = "";
    /// <summary>Generic responses when an object reveals nothing, keyed by verb (or <c>verb:tool</c>).</summary>
    public Dictionary<string, string> Fallback { get; set; } = new();
    public int Order { get; set; }
}

/// <summary>
/// The courtyard as the hub shows it: one fixed scene. Old Stoic is a big old tree, serene and unchanging, beside a
/// lazy waterfall in a quiet corner of the city. It is deliberately not a progress display.
/// </summary>
public sealed class HubResource
{
    public string Caption { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Art { get; set; } = new();
}

public sealed class GameConfig
{
    public string Title { get; set; } = "Atrium";
    public string Prologue { get; set; } = "";
    /// <summary>Real seconds per world-clock tick (open question 5).</summary>
    public double SecondsPerTick { get; set; } = 1.0;
    /// <summary>Most ticks one real-time step may add (a long idle never floods the pools).</summary>
    public int MaxTicksPerStep { get; set; } = 600;
    /// <summary>Ticks passed by "sit a while".</summary>
    public int SitTicks { get; set; } = 60;
    /// <summary><c>none</c> or <c>capped</c>.</summary>
    public string OfflineProgress { get; set; } = "none";
    public int OfflineCapTicks { get; set; } = 1800;
    /// <summary><c>numeric</c> or <c>diegetic</c>.</summary>
    public string TickDisplay { get; set; } = "diegetic";
    /// <summary><c>global</c>, <c>perObject</c> or <c>globalFiltered</c> (open question 4).</summary>
    public string LexiconMode { get; set; } = "global";
    /// <summary><c>off</c>, <c>gentle</c> or <c>strong</c> (open question 2).</summary>
    public string Funnel { get; set; } = "gentle";
    public int NearMissMinLocales { get; set; } = 2;
    /// <summary><c>druids-bag</c> or <c>satchel</c> (open question 11).</summary>
    public string MineralDestination { get; set; } = "druids-bag";
    public ulong Seed { get; set; } = 0x5EED_A7_12;
    public SynthesisText Synthesis { get; set; } = new();
    public LibraryConfig Library { get; set; } = new();
    public ApothecaryConfig Apothecary { get; set; } = new();
    public PoolsConfig Pools { get; set; } = new();
}

public sealed class SynthesisText
{
    public List<string> Invalid { get; set; } = new() { "The thought doesn't sit with your notes." };
    public List<string> NotYet { get; set; } = new() { "The thought won't hold together yet." };
    public string AlreadyConsidered { get; set; } = "You've already worked through why that doesn't hold. It's in your journal.";
    public string AlreadyResolved { get; set; } = "You already know what this is.";
}
