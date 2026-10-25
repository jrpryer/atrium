using Atrium.Core.Content;
using Xunit;
using Xunit.Abstractions;

namespace Atrium.Core.Tests;

public class ContentTests
{
    private readonly ITestOutputHelper _out;
    public ContentTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void ShippedContentLoadsAndValidates()
    {
        var (db, report) = ContentLoader.TryLoad(new DirectoryContentSource(TestContent.Root));
        foreach (var w in report.Warnings) _out.WriteLine("warning: " + w);
        foreach (var e in report.Errors) _out.WriteLine("ERROR: " + e);
        Assert.Empty(report.Errors);
        Assert.NotNull(db);
        Assert.NotEmpty(db!.Hub.Art);
    }
}
