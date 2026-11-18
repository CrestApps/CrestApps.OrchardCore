using System.Linq.Expressions;
using System.Reflection;
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
/// Records are saved with YesSql's version check, so a record live traffic saved after the backfill read it is never
/// overwritten with the older copy; that save already mapped its index rows again, so the record is skipped.
/// </remarks>
public static class CatalogIndexBackfill
{
    /// <summary>
    /// The number of records saved again in each page.
    /// </summary>
    public const int DefaultPageSize = 200;

    private static readonly MethodInfo _isAfter = typeof(CatalogIndexBackfill).GetMethod(nameof(IsAfter), BindingFlags.NonPublic | BindingFlags.Static);

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

        // Paged by key (the item ID, which saving does not change): each page starts after the last record of the page
        // before. Paging with Skip would move a page forward whenever live traffic takes a record out of the filter (an
        // order paid meanwhile), and the first record that should have been read would never be indexed again.
        string after = null;

        while (true)
        {
            var (count, saved, last) = await ReindexPageAsync<TRecord, TIndex>(session.Store, collection, filter, after, pageSize, logger);

            total += saved;

            if (count < pageSize)
            {
                return total;
            }

            after = last;
        }
    }

    private static async Task<(int Count, int Saved, string Last)> ReindexPageAsync<TRecord, TIndex>(IStore store, string collection, Expression<Func<TIndex, bool>> filter, string after, int pageSize, ILogger logger)
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

            if (after is not null)
            {
                query = query.Where(ItemIdAfter<TIndex>(after));
            }

            var records = (await query
                .OrderBy(index => index.ItemId)
                .Take(pageSize)
                .ListAsync()).ToList();

            itemIds = records.ConvertAll(record => record.ItemId);

            try
            {
                // With the version check: a record saved by live traffic since this page read it (an order paid by a
                // webhook) is not overwritten with the copy read here. Its page is then saved one record at a time.
                foreach (var record in records)
                {
                    await page.SaveAsync(record, checkConcurrency: true, collection);
                }

                await page.SaveChangesAsync();

                return (records.Count, records.Count, itemIds.LastOrDefault());
            }
            catch (ConcurrencyException)
            {
                // YesSql cancels a session whose save failed, so the page is saved again one record at a time, each read
                // again.
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("A record of a page of {RecordType} records was saved meanwhile; its records are indexed again one at a time.", typeof(TRecord).Name);
                }
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

        return (itemIds.Count, saved, itemIds.LastOrDefault());
    }

    // index => index.ItemId > after, which C# can't write for strings: YesSql writes the comparison as SQL, so the database
    // compares the item IDs in the same order the pages are sorted in.
    private static Expression<Func<TIndex, bool>> ItemIdAfter<TIndex>(string after)
        where TIndex : CatalogItemIndex
    {
        var index = Expression.Parameter(typeof(TIndex), "index");
        var comparison = Expression.GreaterThan(
            Expression.Property(index, nameof(CatalogItemIndex.ItemId)),
            Expression.Constant(after, typeof(string)),
            liftToNull: false,
            _isAfter);

        return Expression.Lambda<Func<TIndex, bool>>(comparison, index);
    }

    // The method the comparison above names; YesSql translates the comparison to SQL and never calls it.
    private static bool IsAfter(string value, string after)
        => string.CompareOrdinal(value, after) > 0;

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

            await session.SaveAsync(record, checkConcurrency: true, collection);
            await session.SaveChangesAsync();

            return true;
        }
        catch (ConcurrencyException)
        {
            // Saved by live traffic since it was read here: that save mapped its index rows again, with the new columns.
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("The {RecordType} record {ItemId} was saved meanwhile, which indexed it again.", typeof(TRecord).Name, itemId);
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The {RecordType} record {ItemId} could not be indexed again and was skipped. It is indexed again when it is next saved.", typeof(TRecord).Name, itemId);

            return false;
        }
    }
}
