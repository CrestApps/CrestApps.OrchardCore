using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;

/// <summary>
/// Saves again, a page at a time, the contacts and leads saved before one of their index tables existed, so that table
/// gets the rows of every record. The phone number backfill and the email address step both page through here.
/// </summary>
/// <remarks>
/// A record is indexed again whenever it is saved, so only the records nothing has touched since need this. Each page
/// runs in a session of its own and is committed on its own, so a run that stops part way keeps what it did, and a
/// page read again later finds its records indexed and leaves them alone.
/// </remarks>
internal static class ContactIndexBackfill
{
    /// <summary>
    /// How many records a page reads.
    /// </summary>
    internal const int PageSize = 200;

    /// <summary>
    /// Indexes up to <paramref name="pages"/> pages of records after <paramref name="cursor"/>.
    /// </summary>
    /// <param name="store">The store the records are read from and saved to.</param>
    /// <param name="cursor">The document id of the last record already read.</param>
    /// <param name="pages">How many pages to read before returning.</param>
    /// <param name="needsRows">Whether a record has values the index table would hold a row for.</param>
    /// <param name="readIndexedDocumentIds">Reads which of the given document ids already have rows in the table.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The new cursor, whether every record has now been read, and how many records were saved again.</returns>
    internal static async Task<(long Cursor, bool Completed, int Reindexed)> RunAsync(
        IStore store,
        long cursor,
        int pages,
        Func<ContentItem, bool> needsRows,
        Func<ISession, long[], CancellationToken, Task<IEnumerable<long>>> readIndexedDocumentIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(needsRows);
        ArgumentNullException.ThrowIfNull(readIndexedDocumentIds);

        var reindexed = 0;

        for (var page = 0; page < pages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (pageCursor, pageReindexed, found) = await IndexPageAsync(store, cursor, needsRows, readIndexedDocumentIds, cancellationToken);

            if (!found)
            {
                return (cursor, true, reindexed);
            }

            cursor = pageCursor;
            reindexed += pageReindexed;
        }

        return (cursor, false, reindexed);
    }

    private static async Task<(long Cursor, int Reindexed, bool Found)> IndexPageAsync(
        IStore store,
        long cursor,
        Func<ContentItem, bool> needsRows,
        Func<ISession, long[], CancellationToken, Task<IEnumerable<long>>> readIndexedDocumentIds,
        CancellationToken cancellationToken)
    {
        long[] pending;
        var pageCursor = cursor;

        await using (var session = store.CreateSession())
        {
            // The contact index has one row for each published or latest version of a contact or lead, which are
            // exactly the versions the other contact index tables hold. The page is read from that index alone, so the
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

            // A record saved since the table was added already has its rows.
            var indexed = (await readIndexedDocumentIds(session, ids, cancellationToken)).ToHashSet();

            var toIndex = items
                .Where(item => !indexed.Contains(item.Id) && needsRows(item))
                .ToList();

            if (toIndex.Count == 0)
            {
                return (pageCursor, 0, true);
            }

            foreach (var item in toIndex)
            {
                // Saving the record unchanged writes its index rows again. The concurrency check keeps a version read
                // here from overwriting one saved by somebody else meanwhile, which would also undo a publish that
                // turned this version into an old one.
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
                // Whoever saved it meanwhile indexed it again, with all of its values.
            }
        }

        return (pageCursor, reindexed, true);
    }
}
