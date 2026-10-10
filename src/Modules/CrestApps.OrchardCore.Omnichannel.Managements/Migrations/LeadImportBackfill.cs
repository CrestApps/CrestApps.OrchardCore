using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Records the file imports on the leads imported before leads recorded them. An import does not keep the items it
/// wrote, so a lead is attributed to an import when the import set it as the lead's owner and created or last saved
/// it while the import ran: every row an import writes is owned by the user who uploaded the file. A lead the same
/// user saved by hand while their import ran is attributed to it too, and a lead an import updated that was saved
/// again later is not.
/// </summary>
internal static class LeadImportBackfill
{
    private const int BatchSize = 200;

    /// <summary>
    /// Records the imports of the lead types on the leads they wrote.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="leadTypes">The lead content types.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The number of leads an import was recorded on.</returns>
    public static async Task<int> RunAsync(IStore store, IReadOnlyCollection<string> leadTypes, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(leadTypes);

        if (leadTypes.Count == 0)
        {
            return 0;
        }

        var types = leadTypes.ToArray();
        List<ContentTransferEntry> entries;

        await using (var session = store.CreateSession())
        {
            entries = (await session.Query<ContentTransferEntry, ContentTransferEntryIndex>(index => index.ContentType.IsIn(types))
                    .OrderBy(index => index.CreatedUtc)
                    .ListAsync())
                .Where(entry => entry.Direction == ContentTransferDirection.Import && !string.IsNullOrEmpty(entry.Owner))
                .ToList();
        }

        var total = 0;

        foreach (var entry in entries)
        {
            var recorded = await RecordAsync(store, entry);

            total += recorded;

            if (recorded > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Recorded the import '{EntryId}' on {LeadCount} leads it wrote before leads recorded their imports.",
                    entry.EntryId,
                    recorded);
            }
        }

        return total;
    }

    private static async Task<int> RecordAsync(IStore store, ContentTransferEntry entry)
    {
        var from = entry.CreatedUtc;
        var to = entry.CompletedUtc ?? entry.ProcessSaveUtc;

        // An import that never saved a batch wrote no leads.
        if (to is null || to < from)
        {
            return 0;
        }

        var until = to.Value;
        var contentType = entry.ContentType;
        var owner = entry.Owner;
        var documentId = 0L;
        var recorded = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            await using var session = store.CreateSession();

            var batch = (await session.Query<ContentItem, ContentItemIndex>(index =>
                    index.ContentType == contentType &&
                    index.Owner == owner &&
                    (index.Latest || index.Published) &&
                    ((index.CreatedUtc >= from && index.CreatedUtc <= until) ||
                        (index.ModifiedUtc >= from && index.ModifiedUtc <= until)) &&
                    index.DocumentId > documentId)
                .OrderBy(index => index.DocumentId)
                .Take(BatchSize)
                .ListAsync())
                .ToList();

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var contentItem in batch)
            {
                documentId = Math.Max(documentId, contentItem.Id);

                if (!contentItem.TryGet<LeadPart>(out var part) || !LeadImports.Record(part, entry))
                {
                    continue;
                }

                contentItem.Apply(part);
                await session.SaveAsync(contentItem);
                recorded.Add(contentItem.ContentItemId);
            }

            await session.SaveChangesAsync();
        }

        return recorded.Count;
    }
}
