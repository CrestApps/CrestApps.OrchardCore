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
/// <remarks>
/// The leads are read once, in document order, and each is matched against every import, rather than once per import
/// with a query over owner and time windows: that query could not use an index, and on a large tenant it outlived
/// the command timeout during activation, which left every past import unrecorded. Work is done a step at a time from
/// a cursor, so a background task can spread it over several runs and pick up where it stopped.
/// </remarks>
internal static class LeadImportBackfill
{
    private const int PageSize = 500;

    /// <summary>
    /// Records the imports of the lead types on the leads they wrote, all in one go.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="leadTypes">The lead content types.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The number of leads an import was recorded on.</returns>
    public static async Task<int> RunAsync(IStore store, IReadOnlyCollection<string> leadTypes, ILogger logger)
    {
        var total = 0;
        var cursor = 0L;

        while (true)
        {
            var step = await RunStepAsync(store, leadTypes, cursor, maxPages: int.MaxValue, logger, CancellationToken.None);

            total += step.Recorded;
            cursor = step.Cursor;

            if (step.Completed)
            {
                return total;
            }
        }
    }

    /// <summary>
    /// Reads up to <paramref name="maxPages"/> pages of leads after <paramref name="cursor"/> and records on each the
    /// imports that wrote it.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="leadTypes">The lead content types.</param>
    /// <param name="cursor">The document id of the last lead already read, or zero to start from the beginning.</param>
    /// <param name="maxPages">The most pages of leads to read in this step.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Where the step stopped, how many leads it recorded an import on, and whether every lead was read.</returns>
    public static async Task<LeadImportBackfillStep> RunStepAsync(
        IStore store,
        IReadOnlyCollection<string> leadTypes,
        long cursor,
        int maxPages,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(leadTypes);

        if (leadTypes.Count == 0)
        {
            return new LeadImportBackfillStep(cursor, 0, Completed: true);
        }

        var types = leadTypes.ToArray();
        var imports = await GetImportsAsync(store, types, cancellationToken);

        if (imports.Count == 0)
        {
            return new LeadImportBackfillStep(cursor, 0, Completed: true);
        }

        var recorded = 0;

        for (var page = 0; page < maxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var session = store.CreateSession();

            var rows = (await session.QueryIndex<ContentItemIndex>(index =>
                    index.ContentType.IsIn(types) &&
                    (index.Latest || index.Published) &&
                    index.DocumentId > cursor)
                .OrderBy(index => index.DocumentId)
                .Take(PageSize)
                .ListAsync(cancellationToken))
                .ToList();

            if (rows.Count == 0)
            {
                return new LeadImportBackfillStep(cursor, recorded, Completed: true);
            }

            cursor = rows.Max(row => row.DocumentId);

            // Only the index rows are read for the whole page; a lead is loaded only when an import wrote it.
            var matches = rows
                .Select(row => (row.DocumentId, Imports: imports.Where(import => import.Wrote(row)).ToArray()))
                .Where(match => match.Imports.Length > 0)
                .ToDictionary(match => match.DocumentId, match => match.Imports);

            if (matches.Count == 0)
            {
                continue;
            }

            var leads = await session.GetAsync<ContentItem>(matches.Keys.ToArray(), cancellationToken: cancellationToken);

            foreach (var lead in leads)
            {
                if (lead is null ||
                    !matches.TryGetValue(lead.Id, out var leadImports) ||
                    !lead.TryGet<LeadPart>(out var part))
                {
                    continue;
                }

                var changed = false;

                foreach (var import in leadImports)
                {
                    changed |= LeadImports.Record(part, import.Entry);
                }

                if (!changed)
                {
                    continue;
                }

                lead.Apply(part);
                await session.SaveAsync(lead, cancellationToken: cancellationToken);
                recorded++;
            }

            await session.SaveChangesAsync(cancellationToken);
        }

        if (recorded > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Recorded past file imports on {LeadCount} leads; leads up to document {Cursor} have been read.", recorded, cursor);
        }

        return new LeadImportBackfillStep(cursor, recorded, Completed: false);
    }

    private static async Task<List<PastImport>> GetImportsAsync(IStore store, string[] types, CancellationToken cancellationToken)
    {
        await using var session = store.CreateSession();

        var entries = await session.Query<ContentTransferEntry, ContentTransferEntryIndex>(index => index.ContentType.IsIn(types))
            .OrderBy(index => index.CreatedUtc)
            .ListAsync(cancellationToken);

        return entries
            .Where(entry => entry.Direction == ContentTransferDirection.Import && !string.IsNullOrEmpty(entry.Owner))
            .Select(PastImport.Create)
            .Where(import => import is not null)
            .ToList();
    }

    private sealed class PastImport
    {
        private DateTime _from;
        private DateTime _until;

        public ContentTransferEntry Entry { get; private init; }

        public static PastImport Create(ContentTransferEntry entry)
        {
            var until = entry.CompletedUtc ?? entry.ProcessSaveUtc;

            // An import that never saved a batch wrote no leads.
            if (until is null || until < entry.CreatedUtc)
            {
                return null;
            }

            return new PastImport
            {
                Entry = entry,
                _from = entry.CreatedUtc,
                _until = until.Value,
            };
        }

        public bool Wrote(ContentItemIndex row)
            => string.Equals(row.ContentType, Entry.ContentType, StringComparison.Ordinal) &&
                string.Equals(row.Owner, Entry.Owner, StringComparison.Ordinal) &&
                (Within(row.CreatedUtc) || Within(row.ModifiedUtc));

        private bool Within(DateTime? value)
            => value is { } utc && utc >= _from && utc <= _until;
    }
}

/// <summary>
/// The outcome of one step of <see cref="LeadImportBackfill"/>.
/// </summary>
/// <param name="Cursor">The document id of the last lead read.</param>
/// <param name="Recorded">The number of leads an import was recorded on in this step.</param>
/// <param name="Completed">Whether every lead has been read.</param>
internal sealed record LeadImportBackfillStep(long Cursor, int Recorded, bool Completed);
