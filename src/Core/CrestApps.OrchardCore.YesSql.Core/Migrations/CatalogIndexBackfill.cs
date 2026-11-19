using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.Migrations;

/// <summary>
/// Maps the index rows of every catalog record again, a page at a time, so the columns a later migration added to an
/// index table are filled in for the records saved before it.
/// </summary>
/// <remarks>
/// Use a deferred task (<c>ShellScope.AddDeferredTask</c>) to commit one page at a time after activation.
/// Use <see cref="ReindexInTransactionAsync{TRecord, TIndex}"/> when the new index data must commit with the migration version. Both methods save
/// records through the session so store handlers do not run.
/// </remarks>
public static class CatalogIndexBackfill
{
    /// <summary>
    /// The number of records saved again in each page.
    /// </summary>
    public const int DefaultPageSize = 200;

    /// <summary>
    /// Saves every record of a catalog again through the session, which maps its index rows again.
    /// </summary>
    /// <typeparam name="TRecord">The type of record.</typeparam>
    /// <typeparam name="TIndex">An index of the record that has a row for every record.</typeparam>
    /// <param name="session">The session.</param>
    /// <param name="collection">The collection the records are stored in.</param>
    /// <param name="pageSize">The number of records saved again in each page.</param>
    /// <returns>The number of records saved again.</returns>
    public static Task<int> ReindexAsync<TRecord, TIndex>(ISession session, string collection, int pageSize = DefaultPageSize)
        where TRecord : CatalogItem
        where TIndex : CatalogItemIndex
        => ReindexAsync<TRecord, TIndex>(session, collection, pageSize, commitEachPage: true);

    /// <summary>
    /// Maps existing records in the caller's transaction and checks their versions so concurrent changes roll back the migration.
    /// </summary>
    /// <typeparam name="TRecord">The catalog record to map.</typeparam>
    /// <typeparam name="TIndex">An index with a row for each record.</typeparam>
    /// <param name="session">The session that owns the migration transaction.</param>
    /// <param name="collection">The collection that stores the records.</param>
    /// <returns>The number of records mapped.</returns>
    public static Task<int> ReindexInTransactionAsync<TRecord, TIndex>(ISession session, string collection)
        where TRecord : CatalogItem
        where TIndex : CatalogItemIndex
        => ReindexAsync<TRecord, TIndex>(session, collection, DefaultPageSize, commitEachPage: false);

    private static async Task<int> ReindexAsync<TRecord, TIndex>(ISession session, string collection, int pageSize, bool commitEachPage)
        where TRecord : CatalogItem
        where TIndex : CatalogItemIndex
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var total = 0;

        // Paged by item ID, which saving does not change, so a page never moves while its rows are mapped again.
        for (var skip = 0; ; skip += pageSize)
        {
            var records = await session
                .Query<TRecord, TIndex>(collection: collection)
                .OrderBy(index => index.ItemId)
                .Skip(skip)
                .Take(pageSize)
                .ListAsync();

            var count = 0;

            foreach (var record in records)
            {
                await session.SaveAsync(record, checkConcurrency: !commitEachPage, collection);
                count++;
            }

            if (commitEachPage)
            {
                await session.SaveChangesAsync();
            }
            else
            {
                await session.FlushAsync();
                session.Detach(records, collection);
            }

            total += count;

            if (count < pageSize)
            {
                return total;
            }
        }
    }
}
