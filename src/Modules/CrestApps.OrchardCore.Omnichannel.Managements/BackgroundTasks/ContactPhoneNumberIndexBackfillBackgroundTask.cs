using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Documents;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;

/// <summary>
/// Writes the phone number index rows of the contacts and leads saved before that index existed, so they can be found
/// by any of their numbers, up to 2,000 records a minute until every one has been read.
/// </summary>
/// <remarks>
/// A record is indexed again whenever it is saved, so only the records nothing has touched since need this. It runs
/// here rather than in the migration that creates the table, because re-saving every contact during activation would
/// hold up the tenant on a large list. Until it has finished, <see cref="Services.OmnichannelContactPhoneLookup"/>
/// also reads the cell and home columns of the contact index, so no record that used to be found goes missing.
/// </remarks>
[BackgroundTask(
    Title = "Omnichannel Contact Phone Number Backfill",
    Schedule = "* * * * *",
    Description = "Indexes every phone number of the contacts and leads saved before each number was indexed.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class ContactPhoneNumberIndexBackfillBackgroundTask : IBackgroundTask
{
    internal const string BackfillName = "ContactPhoneNumbers";

    internal const int PageSize = 200;

    private const int PagesPerRun = 10;

    private bool _completed;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (_completed)
        {
            return;
        }

        var stateManager = serviceProvider.GetRequiredService<IDocumentManager<OmnichannelBackfillState>>();
        var state = await stateManager.GetOrCreateMutableAsync();

        if (state.Completed.Contains(BackfillName))
        {
            _completed = true;

            return;
        }

        var step = await RunStepAsync(
            serviceProvider.GetRequiredService<IStore>(),
            serviceProvider.GetRequiredService<IContactMethodsAccessor>(),
            state.Cursors.GetValueOrDefault(BackfillName),
            PagesPerRun,
            serviceProvider.GetRequiredService<ILogger<ContactPhoneNumberIndexBackfillBackgroundTask>>(),
            cancellationToken);

        if (step.Completed)
        {
            state.Completed.Add(BackfillName);
            state.Cursors.Remove(BackfillName);
            _completed = true;
        }
        else
        {
            state.Cursors[BackfillName] = step.Cursor;
        }

        await stateManager.UpdateAsync(state);
    }

    /// <summary>
    /// Indexes up to <paramref name="pages"/> pages of records after <paramref name="cursor"/>.
    /// </summary>
    /// <param name="store">The store the records are read from and saved to.</param>
    /// <param name="contactMethods">Reads the phone numbers of a record.</param>
    /// <param name="cursor">The document id of the last record already read.</param>
    /// <param name="pages">How many pages to read before returning.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The new cursor, and whether every record has now been read.</returns>
    internal static async Task<(long Cursor, bool Completed)> RunStepAsync(
        IStore store,
        IContactMethodsAccessor contactMethods,
        long cursor,
        int pages,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var reindexed = 0;

        for (var page = 0; page < pages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (pageCursor, pageReindexed, found) = await IndexPageAsync(store, contactMethods, cursor, cancellationToken);

            if (!found)
            {
                Log(logger, reindexed);

                return (cursor, true);
            }

            cursor = pageCursor;
            reindexed += pageReindexed;
        }

        Log(logger, reindexed);

        return (cursor, false);
    }

    private static async Task<(long Cursor, int Reindexed, bool Found)> IndexPageAsync(
        IStore store,
        IContactMethodsAccessor contactMethods,
        long cursor,
        CancellationToken cancellationToken)
    {
        long[] pending;
        var pageCursor = cursor;

        await using (var session = store.CreateSession())
        {
            // The contact index has one row for each published or latest version of a contact or lead, which are
            // exactly the versions the phone number index holds. The page is read from that index alone, so the
            // database seeks to the cursor and stops after one page. Joining the content item index as well made
            // SQL Server group and sort every row after the cursor to find the next page, so each page read almost
            // the whole table and the backfill kept the database at full load for as long as it ran.
            var ids = (await session
                .QueryIndex<OmnichannelContactIndex>(index => index.DocumentId > cursor)
                .OrderBy(index => index.DocumentId)
                .Take(PageSize)
                .ListAsync(cancellationToken))
                .Select(index => index.DocumentId)
                .Distinct()
                .ToArray();

            if (ids.Length == 0)
            {
                return (cursor, 0, false);
            }

            // The cursor moves past every id of the page, also past a record deleted since its index row was read.
            pageCursor = ids.Max();

            var items = (await session.GetAsync<ContentItem>(ids, cancellationToken: cancellationToken)).ToList();

            // A record saved since the index was added already has its rows.
            var indexed = (await session
                .QueryIndex<OmnichannelContactPhoneNumberIndex>(index => index.DocumentId.IsIn(ids))
                .ListAsync(cancellationToken))
                .Select(index => index.DocumentId)
                .ToHashSet();

            var toIndex = items
                .Where(item => !indexed.Contains(item.Id) && contactMethods.Get(item).PhoneNumbers.Count > 0)
                .ToList();

            if (toIndex.Count == 0)
            {
                return (pageCursor, 0, true);
            }

            foreach (var item in toIndex)
            {
                // Saving the record unchanged writes its index rows again, now with a row for each of its numbers. The
                // concurrency check keeps a version read here from overwriting one saved by somebody else meanwhile,
                // which would also undo a publish that turned this version into an old one.
                await session.SaveAsync(item, checkConcurrency: true, cancellationToken: cancellationToken);
            }

            try
            {
                await session.SaveChangesAsync(cancellationToken);

                return (pageCursor, toIndex.Count, true);
            }
            catch (ConcurrencyException)
            {
                // One record of the page was saved by somebody else meanwhile, which undid the whole page; the
                // records are indexed one at a time instead, so only that one is skipped.
                pending = toIndex.Select(item => item.Id).ToArray();
            }
        }

        var reindexed = 0;

        foreach (var documentId in pending)
        {
            await using var session = store.CreateSession();

            var item = await session
                .Query<ContentItem, ContentItemIndex>(index => index.DocumentId == documentId)
                .With<OmnichannelContactIndex>()
                .FirstOrDefaultAsync(cancellationToken);

            if (item is null)
            {
                continue;
            }

            await session.SaveAsync(item, checkConcurrency: true, cancellationToken: cancellationToken);

            try
            {
                await session.SaveChangesAsync(cancellationToken);
                reindexed++;
            }
            catch (ConcurrencyException)
            {
                // Whoever saved it meanwhile indexed it again, with all of its numbers.
            }
        }

        return (pageCursor, reindexed, true);
    }

    private static void Log(ILogger logger, int reindexed)
    {
        if (reindexed > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Indexed every phone number of {RecordCount} contacts and leads saved before each number was indexed.", reindexed);
        }
    }
}
