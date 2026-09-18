using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Services;

/// <summary>
/// Decides which stored records belong on the File Sources screens.
/// </summary>
/// <remarks>
/// A file source is not a new entity: it is a <c>WebCrawler</c> record whose source names a registered
/// ingestion connector rather than a registered crawl strategy. That is the only thing separating the two
/// kinds, so it is also the only thing the two screens may sort by.
/// <para>
/// An unregistered source belongs to neither screen and is shown on neither: whatever registered it is
/// gone, and the record cannot be run either way.
/// </para>
/// </remarks>
public static class FileSourceRecords
{
    /// <summary>
    /// Determines whether a source names a registered ingestion connector.
    /// </summary>
    /// <param name="source">The record's source.</param>
    /// <param name="connectors">The registered connectors.</param>
    /// <returns><see langword="true"/> when the source is a registered connector.</returns>
    public static bool IsConnector(string source, IReadOnlyList<IngestionConnectorDescriptor> connectors)
    {
        ArgumentNullException.ThrowIfNull(connectors);

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        foreach (var connector in connectors)
        {
            if (string.Equals(connector.Name, source, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Keeps only the records the File Sources screens manage.
    /// </summary>
    /// <param name="records">The records to filter.</param>
    /// <param name="connectors">The registered connectors.</param>
    /// <returns>The records whose source is a registered ingestion connector.</returns>
    public static IReadOnlyList<WebCrawler> SelectFileSources(IEnumerable<WebCrawler> records, IReadOnlyList<IngestionConnectorDescriptor> connectors)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(connectors);

        return records
            .Where(record => IsConnector(record.Source, connectors))
            .ToArray();
    }
}
