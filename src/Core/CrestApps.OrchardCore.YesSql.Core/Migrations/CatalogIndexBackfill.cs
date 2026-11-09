using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.Migrations;

/// <summary>
/// Maps the index rows of every catalog record again, a page at a time, so the columns a later migration added to an
/// index table are filled in for the records saved before it.
/// </summary>
/// <remarks>
/// Run it from a deferred task (<c>ShellScope.AddDeferredTask</c>) scheduled by the migration that adds the columns,
/// not inside the migration: mapping every record again during activation would hold up the tenant. Records are saved
/// straight through the session rather than through their store, so no store handler runs.
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
    public static async Task<int> ReindexAsync<TRecord, TIndex>(ISession session, string collection, int pageSize = DefaultPageSize)
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
                await session.SaveAsync(record, checkConcurrency: false, collection);
                count++;
            }

            await session.SaveChangesAsync();
            total += count;

            if (count < pageSize)
            {
                return total;
            }
        }
    }
}
