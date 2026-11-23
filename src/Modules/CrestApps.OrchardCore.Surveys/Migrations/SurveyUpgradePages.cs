using System.Globalization;
using System.Linq.Expressions;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Walks the records of Surveys catalogs one page per run of a resumable upgrade step (<see cref="IResumableStep"/>),
/// keeping where it got to in the step's cursor, so a large table is upgraded in the background, never during the
/// tenant's activation.
/// </summary>
internal static class SurveyUpgradePages
{
    /// <summary>
    /// The number of records read in one run.
    /// </summary>
    public const int PageSize = 200;

    private const char PhaseSeparator = '|';
    private const char RangeSeparator = ':';

    /// <summary>
    /// Runs the next page of a step made of walks done one after another, such as one walk per catalog. The step's
    /// cursor keeps which walk it is on and where that walk got to.
    /// </summary>
    /// <param name="context">The step's context.</param>
    /// <param name="walks">The walks, in order; each takes its cursor (<see langword="null"/> on its first page) and
    /// returns the cursor of its next page, or <see langword="null"/> when it is done.</param>
    /// <returns><see langword="true"/> when every walk is done.</returns>
    public static async Task<bool> RunAsync(ResumableStepContext context, params Func<string, Task<string>>[] walks)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(walks);

        var (phase, cursor) = ReadPhase(context.Cursor);

        if (phase < walks.Length)
        {
            cursor = await walks[phase](cursor);

            if (cursor is null)
            {
                phase++;
            }
        }

        if (phase >= walks.Length)
        {
            return true;
        }

        context.Cursor = phase.ToString(CultureInfo.InvariantCulture) + PhaseSeparator + cursor;

        return false;
    }

    /// <summary>
    /// Visits the next page of a catalog's records, oldest index row first, and saves those the visit changed.
    /// </summary>
    /// <typeparam name="T">The records.</typeparam>
    /// <typeparam name="TIndex">Their index, with a row per record.</typeparam>
    /// <param name="session">The YesSql session.</param>
    /// <param name="catalog">The catalog, which loads and saves the records.</param>
    /// <param name="collection">The collection of the records.</param>
    /// <param name="cursor">Where the walk got to, or <see langword="null"/> on its first page.</param>
    /// <param name="visit">The visit; returns <see langword="true"/> when it changed the record.</param>
    /// <param name="filter">Which index rows to visit, or <see langword="null"/> for all of them.</param>
    /// <param name="pageSize">The number of records read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cursor of the next page, or <see langword="null"/> when the walk is done.</returns>
    public static async Task<string> VisitPageAsync<T, TIndex>(
        ISession session,
        DocumentCatalog<T, TIndex> catalog,
        string collection,
        string cursor,
        Func<T, bool> visit,
        Expression<Func<TIndex, bool>> filter = null,
        int pageSize = PageSize,
        CancellationToken cancellationToken = default)
        where T : CatalogItem
        where TIndex : CatalogItemIndex
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var (afterId, upToId) = ReadRange(cursor);

        if (upToId == 0)
        {
            // Saving a record can write its index row again under a new id, so the walk stops at the last row that was
            // there when it began: a saved record is never visited twice.
            var last = await Filtered(session.QueryIndex<TIndex>(collection: collection), filter)
                .OrderByDescending(index => index.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (last is null)
            {
                return null;
            }

            upToId = last.Id;
        }

        var rows = (await Filtered(session.QueryIndex<TIndex>(index => index.Id > afterId && index.Id <= upToId, collection: collection), filter)
            .OrderBy(index => index.Id)
            .Take(pageSize)
            .ListAsync(cancellationToken)).ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        var ids = rows.Select(row => row.ItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        foreach (var record in await catalog.GetAsync(ids, cancellationToken))
        {
            if (visit(record))
            {
                await catalog.UpdateAsync(record, cancellationToken);
            }
        }

        afterId = rows[^1].Id;

        return rows.Count < pageSize || afterId >= upToId
            ? null
            : afterId.ToString(CultureInfo.InvariantCulture) + RangeSeparator + upToId.ToString(CultureInfo.InvariantCulture);
    }

    private static IQueryIndex<TIndex> Filtered<TIndex>(IQueryIndex<TIndex> query, Expression<Func<TIndex, bool>> filter)
        where TIndex : CatalogItemIndex
        => filter is null ? query : query.Where(filter);

    private static (int Phase, string Cursor) ReadPhase(string cursor)
    {
        var separator = string.IsNullOrEmpty(cursor) ? -1 : cursor.IndexOf(PhaseSeparator, StringComparison.Ordinal);

        if (separator <= 0 || !int.TryParse(cursor.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var phase))
        {
            return (0, null);
        }

        var rest = cursor[(separator + 1)..];

        return (phase, rest.Length == 0 ? null : rest);
    }

    private static (long AfterId, long UpToId) ReadRange(string cursor)
    {
        var separator = string.IsNullOrEmpty(cursor) ? -1 : cursor.IndexOf(RangeSeparator, StringComparison.Ordinal);

        if (separator <= 0
            || !long.TryParse(cursor.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var afterId)
            || !long.TryParse(cursor.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var upToId))
        {
            return (0, 0);
        }

        return (afterId, upToId);
    }
}
