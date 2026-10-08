using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers.Strategies;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Services;

/// <summary>
/// Decides which stored records belong on the Web Crawlers screens.
/// </summary>
/// <remarks>
/// File sources have their own store now, so a crawler record is normally a crawler and this would have
/// nothing to do. It is still here for the records that predate that split, when both kinds shared this
/// store and were told apart only by whether their source named a crawl strategy or an ingestion
/// connector. Those move across on upgrade, but two cases leave one behind: a tenant whose upgrade has not
/// run yet, and a record whose connector module was disabled when it did -- the move only claims sources
/// it can see registered, so an FTP file source on a tenant with FTP switched off stays put.
/// <para>
/// Whatever the reason, the record is not a crawler and must not be treated as one: it would list here,
/// and its editor would grow a second name and a target Web data source it does not have.
/// </para>
/// <para>
/// A source that names neither a strategy nor anything else registered belongs to no screen and is shown
/// on none: whatever registered it is gone, and the record cannot be run either way.
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
