using System.Linq.Expressions;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.Migrations;

/// <summary>
/// Maps the index rows of every catalog record again, a page at a time, so the columns a later migration added to an
/// index table are filled in for the records saved before it.
/// </summary>
/// <remarks>
/// Run it from a deferred task (<c>ShellScope.AddDeferredTask</c>) scheduled by the migration that adds the columns,
/// not inside the migration: mapping every record again during activation would hold up the tenant. Records are saved
/// straight through a session of the store rather than through their store, so no store handler runs. Each page is saved
/// in a session of its own: a record that fails to be mapped again cancels only its page's session, and that page is then
/// saved again one record at a time, so the failing record is logged and skipped and every other record is still indexed.
/// </remarks>
public static class CatalogIndexBackfill
{
    /// <summary>
    /// The number of records saved again in each page.
    /// </summary>
    public const int DefaultPageSize = 200;

    /// <summary>
    /// Saves every record of a catalog again, which maps its index rows again. A record that fails to be mapped again is
    /// logged and skipped; it keeps its old index rows until it is next saved.
    /// </summary>
    /// <typeparam name="TRecord">The type of record.</typeparam>
    /// <typeparam name="TIndex">An index of the record that has a row for every record.</typeparam>
    /// <param name="session">A session of the store the records are in; each page is saved in a new session of it.</param>
    /// <param name="collection">The collection the records are stored in.</param>
    /// <param name="pageSize">The number of records saved again in each page.</param>
    /// <param name="logger">The logger a record that fails to be mapped again is logged to.</param>
    /// <param name="filter">The records to save again, by their index row, or <see langword="null"/> for every record.</param>
    /// <returns>The number of records saved again.</returns>
    public static async Task<int> ReindexAsync<TRecord, TIndex>(ISession session, string collection, int pageSize = DefaultPageSize, ILogger logger = null, Expression<Func<TIndex, bool>> filter = null)
        where TRecord : CatalogItem
        where TIndex : CatalogItemIndex
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        logger ??= NullLogger.Instance;

        var total = 0;

        // Paged by item ID, which saving does not change, so a page never moves while its rows are mapped again. A skipped
        // record keeps its place, so the pages after it don't move either.
        for (var skip = 0; ; skip += pageSize)
        {
            var (count, saved) = await ReindexPageAsync<TRecord, TIndex>(session.Store, collection, filter, skip, pageSize, logger);

            total += saved;

            if (count < pageSize)
            {
                return total;
            }
        }
    }

    private static async Task<(int Count, int Saved)> ReindexPageAsync<TRecord, TIndex>(IStore store, string collection, Expression<Func<TIndex, bool>> filter, int skip, int pageSize, ILogger logger)
        where TRecord : CatalogItem
        where TIndex : CatalogItemIndex
    {
        List<string> itemIds;

        await using (var page = store.CreateSession())
        {
            var query = page.Query<TRecord, TIndex>(collection: collection);

            if (filter is not null)
            {
                query = query.Where(filter);
            }

            var records = (await query
                .OrderBy(index => index.ItemId)
                .Skip(skip)
                .Take(pageSize)
                .ListAsync()).ToList();

            itemIds = records.ConvertAll(record => record.ItemId);

            try
            {
                foreach (var record in records)
                {
                    await page.SaveAsync(record, checkConcurrency: false, collection);
                }

                await page.SaveChangesAsync();

                return (records.Count, records.Count);
            }
            catch (Exception ex)
            {
                // YesSql cancels a session whose save failed, so the page is saved again one record at a time.
                logger.LogWarning(ex, "A page of {RecordType} records could not be indexed again; its records are indexed again one at a time.", typeof(TRecord).Name);
            }
        }

        var saved = 0;

        foreach (var itemId in itemIds)
        {
            if (await ReindexOneAsync<TRecord, TIndex>(store, collection, itemId, logger))
            {
                saved++;
            }
        }

        return (itemIds.Count, saved);
    }

    private static async Task<bool> ReindexOneAsync<TRecord, TIndex>(IStore store, string collection, string itemId, ILogger logger)
        where TRecord : CatalogItem
        where TIndex : CatalogItemIndex
    {
        await using var session = store.CreateSession();

        try
        {
            if (await session.Query<TRecord, TIndex>(index => index.ItemId == itemId, collection: collection).FirstOrDefaultAsync() is not { } record)
            {
                return false;
            }

            await session.SaveAsync(record, checkConcurrency: false, collection);
            await session.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The {RecordType} record {ItemId} could not be indexed again and was skipped. It is indexed again when it is next saved.", typeof(TRecord).Name, itemId);

            return false;
        }
    }
}
