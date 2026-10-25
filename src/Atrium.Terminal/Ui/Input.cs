namespace Atrium.Terminal.Ui;

public enum KeyKind { Char, Enter, Escape, Up, Down, Left, Right, Backspace, Timeout }

public readonly record struct Key(KeyKind Kind, char Char = '\0')
{
    public bool Is(char c) => Kind == KeyKind.Char && char.ToLowerInvariant(Char) == char.ToLowerInvariant(c);
    public static Key Of(char c) => new(KeyKind.Char, c);
}

public interface IInput
{
    /// <summary>Waits for a key. With a positive timeout, returns <see cref="KeyKind.Timeout"/> if none arrives.</summary>
    Key Read(int timeoutMs = 0);
    string ReadLine();
    bool Scripted { get; }
}

/// <summary>Thrown when a scripted session runs out of input.</summary>
public sealed class ScriptEndedException : Exception { }

public sealed class ConsoleInput : IInput
{
    public bool Scripted => false;

    public Key Read(int timeoutMs = 0)
    {
        if (timeoutMs > 0)
        {
            var waited = 0;
            while (!Console.KeyAvailable)
            {
                Thread.Sleep(50);
                waited += 50;
                if (waited >= timeoutMs) return new Key(KeyKind.Timeout);
            }
        }
        var k = Console.ReadKey(intercept: true);
        return k.Key switch
        {
            ConsoleKey.Enter => new Key(KeyKind.Enter),
            ConsoleKey.Escape => new Key(KeyKind.Escape),
            ConsoleKey.UpArrow => new Key(KeyKind.Up),
            ConsoleKey.DownArrow => new Key(KeyKind.Down),
            ConsoleKey.LeftArrow => new Key(KeyKind.Left),
            ConsoleKey.RightArrow => new Key(KeyKind.Right),
            ConsoleKey.Backspace => new Key(KeyKind.Backspace),
            _ => k.KeyChar == '\0' ? new Key(KeyKind.Timeout) : Key.Of(k.KeyChar),
        };
    }

    public string ReadLine()
    {
        Screen.ShowCursor(true);
        try { return Console.ReadLine() ?? ""; }
        finally { Screen.ShowCursor(false); }
    }
}

/// <summary>
/// Plays keys from a script (a file, or redirected stdin). Each line is a run of single-key presses, or one of
/// the words <c>enter</c>, <c>esc</c>, <c>up</c>, <c>down</c>, <c>left</c>, <c>right</c>. A text prompt consumes
/// the next whole line. Lines starting with <c>#</c> are comments.
/// </summary>
public sealed class ScriptInput : IInput
{
    private readonly Queue<string> _lines;
    private string _current = "";
    public bool Scripted => true;

    public ScriptInput(IEnumerable<string> lines) =>
        _lines = new Queue<string>(lines.Where(l => !l.TrimStart().StartsWith('#')));

    public Key Read(int timeoutMs = 0)
    {
        while (_current.Length == 0)
        {
            if (_lines.Count == 0) throw new ScriptEndedException();
            var line = _lines.Dequeue().Trim();
            switch (line.ToLowerInvariant())
            {
                case "enter": return new Key(KeyKind.Enter);
                case "esc": return new Key(KeyKind.Escape);
                case "up": return new Key(KeyKind.Up);
                case "down": return new Key(KeyKind.Down);
                case "left": return new Key(KeyKind.Left);
                case "right": return new Key(KeyKind.Right);
            }
            _current = line;
        }
        var c = _current[0];
        _current = _current[1..];
        return Key.Of(c);
    }

    public string ReadLine()
    {
        if (_current.Length > 0) { var rest = _current; _current = ""; return rest; }
        if (_lines.Count == 0) throw new ScriptEndedException();
        return _lines.Dequeue();
    }
}
