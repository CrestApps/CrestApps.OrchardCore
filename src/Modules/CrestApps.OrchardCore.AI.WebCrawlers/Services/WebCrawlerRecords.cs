using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers.Strategies;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Services;

/// <summary>
/// Decides which stored records belong on the Web Crawlers screens.
/// </summary>
/// <remarks>
/// A record's source is either a crawl strategy or an ingestion connector, and that is the only thing
/// separating the two kinds: a strategy-backed record crawls a website, while a connector-backed record
/// reads files and is managed on the File Sources screens. Without this the two bleed into each other --
/// a file source lists here, and its editor grows a second name and a target Web data source it does not
/// have.
/// <para>
/// An unregistered source belongs to neither screen and is shown on neither: whatever registered it is
/// gone, and the record cannot be run either way.
/// </para>
/// </remarks>
public static class WebCrawlerRecords
{
    /// <summary>
    /// Determines whether a source names a registered crawl strategy.
    /// </summary>
    /// <param name="source">The record's source.</param>
    /// <param name="strategies">The registered crawl strategies.</param>
    /// <returns><see langword="true"/> when the source is a registered crawl strategy.</returns>
    public static bool IsCrawlStrategy(string source, IReadOnlyList<WebCrawlerStrategyDescriptor> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        foreach (var strategy in strategies)
        {
            if (string.Equals(strategy.Strategy, source, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Keeps only the records the Web Crawlers screens manage.
    /// </summary>
    /// <param name="records">The records to filter.</param>
    /// <param name="strategies">The registered crawl strategies.</param>
    /// <returns>The records whose source is a registered crawl strategy.</returns>
    public static IReadOnlyList<WebCrawler> SelectCrawlers(IEnumerable<WebCrawler> records, IReadOnlyList<WebCrawlerStrategyDescriptor> strategies)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(strategies);

        return records
            .Where(record => IsCrawlStrategy(record.Source, strategies))
            .ToArray();
    }
}
