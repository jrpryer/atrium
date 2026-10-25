using Atrium.Core.Content;

namespace Atrium.Core.Tests;

/// <summary>Loads the shipped content once per test run.</summary>
public static class TestContent
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "content");

    private static readonly Lazy<ContentDatabase> _db = new(() => ContentLoader.LoadFromDirectory(Root));
    public static ContentDatabase Db => _db.Value;
}
