using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies;
using CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources.WebCrawlers;

/// <summary>
/// Checks which records the Web Crawlers screens claim.
/// </summary>
/// <remarks>
/// The mirror of the File Sources rule. Both screens read the same stored record type, so each has to
/// recognize only its own kind or the two bleed into each other.
/// </remarks>
public sealed class WebCrawlerRecordsTests
{
    private static readonly IReadOnlyList<WebCrawlerStrategyDescriptor> _strategies =
    [
        Descriptor(WebCrawlerConstants.Strategies.Sitemap),
    ];

    [Theory]
    [InlineData("Sitemap")]
    [InlineData("sitemap")]
    [InlineData("SITEMAP")]
    public void ARegisteredStrategy_IsACrawler(string source)
        => Assert.True(WebCrawlerRecords.IsCrawlStrategy(source, _strategies));

    [Theory]
    [InlineData("FileSystem")]
    [InlineData("Ftp")]
    [InlineData("Sftp")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AConnectorOrNothing_IsNotACrawler(string source)
        => Assert.False(WebCrawlerRecords.IsCrawlStrategy(source, _strategies));

    [Fact]
    public void SelectCrawlers_KeepsOnlyStrategyBackedRecords()
    {
        WebCrawler[] records =
        [
            new() { ItemId = "a", Source = "FileSystem" },
            new() { ItemId = "b", Source = "Sitemap" },
            new() { ItemId = "c", Source = "Ftp" },
        ];

        Assert.Equal(["b"], WebCrawlerRecords.SelectCrawlers(records, _strategies).Select(r => r.ItemId));
    }

    private static WebCrawlerStrategyDescriptor Descriptor(string strategy)
        => new()
        {
            Strategy = strategy,
            DisplayName = new LocalizedString(strategy, strategy),
            Description = new LocalizedString(strategy, strategy),
        };
}
