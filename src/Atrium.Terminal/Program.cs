using Atrium.Core;
using Atrium.Core.Content;
using Atrium.Core.Runtime;
using Atrium.Terminal.Ui;
using Atrium.Terminal.Views;

namespace Atrium.Terminal;

public static class Program
{
    private const string Usage = """
        Atrium — a cozy puzzle game about tending a forgotten courtyard.

          Atrium                     play
          Atrium --validate          check the content folder and report problems
          Atrium --content <dir>     use a different content folder
          Atrium --saves <dir>       keep saves somewhere else
          Atrium --slot <name>       use a named save slot (default: keeper)
          Atrium --plain             no colour
          Atrium --script <file>     play keys from a file (for testing; see docs)
        """;

    public static int Main(string[] args)
    {
        var opts = Options.Parse(args);
        if (opts.Help) { Console.WriteLine(Usage); return 0; }

        var contentDir = opts.Content ?? FindContent();
        if (contentDir is null) { Console.Error.WriteLine("Couldn't find the content folder. Pass --content <dir>."); return 2; }

        if (opts.Validate) return Validate(contentDir);

        ContentDatabase content;
        try { content = ContentLoader.LoadFromDirectory(contentDir); }
        catch (ContentLoadException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("\nRun with --validate for the full report.");
            return 2;
        }

        IInput input = opts.Script is not null ? new ScriptInput(File.ReadAllLines(opts.Script))
            : Console.IsInputRedirected ? new ScriptInput(ReadAllStdin())
            : new ConsoleInput();
        var interactive = !input.Scripted && !Console.IsOutputRedirected;
        Ansi.Color = !opts.Plain && interactive;

        var savesDir = opts.Saves ?? (input.Scripted
            ? Path.Combine(Path.GetTempPath(), "atrium-script-saves")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Atrium", "saves"));
        var store = new SaveStore(savesDir);

        Screen.Begin(interactive);
        try
        {
            var session = TitleScreen(content, store, input, opts);
            if (session is null) return 0;
            var app = new App(session, store, input, opts.Slot);
            app.Run(new HubView());
            if (!input.Scripted) app.Save(quiet: true);
        }
        catch (ScriptEndedException)
        {
            // A script ran out of keys; that's the end of the test.
        }
        finally
        {
            Screen.End();
        }
        if (interactive) Console.WriteLine("The falls go on falling. Come back any time.");
        return 0;
    }

    private static GameSession? TitleScreen(ContentDatabase content, SaveStore store, IInput input, Options opts)
    {
        while (true)
        {
            var f = new Frame(Screen.Width);
            f.Add(); f.Add();
            foreach (var l in Logo) f.Add("    " + Ansi.BoldFg(Palette.Gold, l));
            f.Add();
            f.Add("    " + Ansi.Italic(Palette.Dim, "a cozy puzzle game about tending a forgotten courtyard"));
            f.Add();
            var summary = store.Summary(opts.Slot);
            f.Add("    " + Ansi.BoldFg(Palette.Moss, "[N]") + " Begin a new Atrium");
            if (summary is not null) f.Add("    " + Ansi.BoldFg(Palette.Moss, "[C]") + " Continue " + Ansi.Fg(Palette.Dim, $"({summary})"));
            f.Add("    " + Ansi.BoldFg(Palette.Moss, "[Q]") + " Quit");
            Screen.Draw(f);

            var k = input.Read();
            if (k.Is('q') || k.Kind == KeyKind.Escape) return null;
            if (k.Is('c') && summary is not null)
            {
                try { return store.Load(opts.Slot, content); }
                catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or InvariantViolationException)
                {
                    Message(input, "That save couldn't be read: " + ex.Message);
                }
            }
            if (k.Is('n'))
            {
                if (summary is not null && !ConfirmOverwrite(input)) continue;
                var s = GameSession.NewGame(content, opts.Seed);
                Prologue(content, input);
                return s;
            }
        }
    }

    private static readonly string[] Logo =
    {
        "    _  _____ ___ ___ _   _ __  __ ",
        "   /_\\|_   _| _ \\_ _| | | |  \\/  |",
        "  / _ \\ | | |   /| || |_| | |\\/| |",
        " /_/ \\_\\|_| |_|_\\___|\\___/|_|  |_|",
    };

    private static void Prologue(ContentDatabase content, IInput input)
    {
        var f = new Frame(Screen.Width);
        f.Add(); f.Add();
        foreach (var l in Wrap.Lines(content.Config.Prologue, Math.Min(Screen.Width - 10, 80), "     ")) f.Add(Ansi.Fg(Palette.Text, l));
        f.Add();
        f.Add("     " + Ansi.Fg(Palette.Faint, "(any key)"));
        Screen.Draw(f);
        input.Read();
    }

    private static bool ConfirmOverwrite(IInput input)
    {
        var f = new Frame(Screen.Width);
        f.Add(); f.Add();
        f.Text("There's already an Atrium in this save slot. Starting again will replace it. (Use --slot <name> to keep both.)", Palette.Text, "     ");
        f.Add();
        f.Add("     " + Ansi.BoldFg(Palette.Moss, "[Y]") + " Start again     " + Ansi.BoldFg(Palette.Rose, "[N]") + " Go back");
        Screen.Draw(f);
        while (true)
        {
            var k = input.Read();
            if (k.Is('y')) return true;
            if (k.Is('n') || k.Kind == KeyKind.Escape) return false;
        }
    }

    private static void Message(IInput input, string text)
    {
        var f = new Frame(Screen.Width);
        f.Add(); f.Add();
        f.Text(text, Palette.Rose, "     ");
        f.Add("     " + Ansi.Fg(Palette.Faint, "(any key)"));
        Screen.Draw(f);
        input.Read();
    }

    private static int Validate(string contentDir)
    {
        var (db, report) = ContentLoader.TryLoad(new DirectoryContentSource(contentDir));
        Console.WriteLine($"Content: {Path.GetFullPath(contentDir)}");
        if (db is not null)
        {
            Console.WriteLine($"  {db.Tomes.Count} tomes · {db.Items.Count} items · {db.Recipes.Count} recipes · {db.Tools.Count} tools · " +
                              $"{db.Lifeforms.Count} lifeforms · {db.Breeding.Count} breeding rules · {db.Pools.Count} pools · " +
                              $"{db.Objects.Count} clue-objects ({db.Readings.Count} readings, {db.Objects.Values.Sum(o => o.Claims.Count)} claims) · " +
                              $"{db.Beats.Count} story beats · {db.Taxonomy.Terms.Count} terms");
        }
        foreach (var w in report.Warnings) Console.WriteLine("  warning: " + w);
        foreach (var e in report.Errors) Console.WriteLine("  ERROR:   " + e);
        Console.WriteLine(report.Ok ? $"OK ({report.Warnings.Count} warning{(report.Warnings.Count == 1 ? "" : "s")})." : $"{report.Errors.Count} error(s).");
        return report.Ok ? 0 : 1;
    }

    /// <summary>The live content folder of a checkout (so edits show without a rebuild), else the copy beside the game.</summary>
    private static string? FindContent()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "content");
            if (File.Exists(Path.Combine(c, "config.json"))) return c;
            dir = dir.Parent;
        }
        var beside = Path.Combine(AppContext.BaseDirectory, "content");
        return File.Exists(Path.Combine(beside, "config.json")) ? beside : null;
    }

    private static IEnumerable<string> ReadAllStdin()
    {
        string? line;
        var lines = new List<string>();
        while ((line = Console.ReadLine()) is not null) lines.Add(line);
        return lines;
    }

    private sealed class Options
    {
        public string? Content, Saves, Script;
        public string Slot = "keeper";
        public bool Validate, Plain, Help;
        public ulong? Seed;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
                switch (args[i])
                {
                    case "--content": o.Content = Next(); break;
                    case "--saves": o.Saves = Next(); break;
                    case "--slot": o.Slot = Next(); break;
                    case "--script": o.Script = Next(); break;
                    case "--seed": o.Seed = ulong.Parse(Next()); break;
                    case "--validate": o.Validate = true; break;
                    case "--plain": o.Plain = true; break;
                    case "-h": case "--help": o.Help = true; break;
                }
            }
            return o;
        }
    }
}
