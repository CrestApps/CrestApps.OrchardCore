using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Visits the records of a catalog a page at a time inside a migration. The host runs a feature's migration steps on
/// one transaction, so a step can't commit; each page is flushed and let go of instead, so a large table is never held
/// in memory or in pending writes all at once.
/// </summary>
internal static class SurveyMigrationPages
{
    /// <summary>
    /// The number of records read at a time.
    /// </summary>
    public const int PageSize = 200;

    /// <summary>
    /// Visits every record of a catalog, oldest first, and saves those the visit changed.
    /// </summary>
    /// <typeparam name="T">The records.</typeparam>
    /// <typeparam name="TIndex">Their index.</typeparam>
    /// <param name="session">The YesSql session.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="collection">The collection of the records.</param>
    /// <param name="visit">The visit; returns <see langword="true"/> when it changed the record.</param>
    /// <param name="pageSize">The number of records read at a time.</param>
    /// <returns>How many records were saved.</returns>
    public static async Task<int> ForEachAsync<T, TIndex>(ISession session, DocumentCatalog<T, TIndex> catalog, string collection, Func<T, bool> visit, int pageSize = PageSize)
        where T : CatalogItem
        where TIndex : CatalogItemIndex
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(visit);

        // Saving a record can write its index row again under a new id, so the walk stops at the last row that was there
        // when it began: a saved record is never visited twice.
        var last = await session.QueryIndex<TIndex>(collection: collection).OrderByDescending(index => index.Id).FirstOrDefaultAsync();

        if (last is null)
        {
            return 0;
        }

        var upToId = last.Id;
        var afterId = 0L;
        var saved = 0;

        while (afterId < upToId)
        {
            var rows = (await session.QueryIndex<TIndex>(index => index.Id > afterId && index.Id <= upToId, collection: collection)
                .OrderBy(index => index.Id)
                .Take(pageSize)
                .ListAsync()).ToList();

            if (rows.Count == 0)
            {
                return saved;
            }

            afterId = rows[^1].Id;

            var records = await catalog.GetAsync(rows.Select(row => row.ItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray());

            foreach (var record in records)
            {
                if (visit(record))
                {
                    await catalog.UpdateAsync(record);
                    saved++;
                }
            }

            await session.FlushAsync();

            foreach (var record in records)
            {
                session.Detach(record, collection);
            }

            if (rows.Count < pageSize)
            {
                return saved;
            }
        }

        return saved;
    }
}
