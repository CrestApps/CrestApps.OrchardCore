using CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

namespace CrestApps.OrchardCore.Tests.Agent.BrowserAutomation;

public sealed class BrowserNavigationPathParserTests
{
    [Theory]
    [InlineData("Search >> Indexes", "Search|Indexes")]
    [InlineData("Search > Indexes > Default", "Search|Indexes|Default")]
    [InlineData("Search/Indexes", "Search|Indexes")]
    [InlineData(@"Search\Indexes", "Search|Indexes")]
    [InlineData("Search » Indexes", "Search|Indexes")]
    [InlineData(" Content   Definitions ", "Content Definitions")]
    public void Split_ShouldReturnNormalizedSegments(string path, string expectedSegments)
    {
        var actual = BrowserNavigationPathParser.Split(path);

        Assert.Equal(expectedSegments.Split('|', StringSplitOptions.RemoveEmptyEntries), actual);
    }

    [Fact]
    public void Split_WhenPathIsBlank_ShouldReturnEmptyArray()
    {
        var actual = BrowserNavigationPathParser.Split("   ");

        Assert.Empty(actual);
    }
}
