using System.Globalization;
using System.Linq.Expressions;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;
using YesSql.Services;

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
    /// <param name="idempotent">Whether a second visit of a record the visit changed changes nothing more. Such a walk
    /// has no upper bound, so it also reaches the records saved while it runs (whose index rows are written again under
    /// new ids); a record it saved is read once more and left as it is. Otherwise the walk stops at the last index row
    /// there when it began, which is safe only when a normal save applies the same change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cursor of the next page, or <see langword="null"/> when the walk is done.</returns>
    /// <remarks>
    /// A record the visit changed is saved only when nobody saved it after the walk read its index row
    /// (<see cref="SaveUnchangedAsync"/>), so the walk never overwrites an author's edit, a send or a delete with the
    /// copy it loaded.
    /// </remarks>
    public static async Task<string> VisitPageAsync<T, TIndex>(
        ISession session,
        DocumentCatalog<T, TIndex> catalog,
        string collection,
        string cursor,
        Func<T, bool> visit,
        Expression<Func<TIndex, bool>> filter = null,
        int pageSize = PageSize,
        bool idempotent = false,
        CancellationToken cancellationToken = default)
        where T : CatalogItem
        where TIndex : CatalogItemIndex
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var (afterId, upToId) = ReadRange(cursor);

        if (idempotent)
        {
            upToId = long.MaxValue;
        }
        else if (upToId == 0)
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
        var changed = new List<T>();

        foreach (var record in await catalog.GetAsync(ids, cancellationToken))
        {
            if (visit(record))
            {
                changed.Add(record);
            }
        }

        await SaveUnchangedAsync(session, catalog, collection, rows, changed, cancellationToken);

        afterId = rows[^1].Id;

        return rows.Count < pageSize || afterId >= upToId
            ? null
            : afterId.ToString(CultureInfo.InvariantCulture) + RangeSeparator + upToId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Saves the records a walk changed, each only when nobody saved or deleted it after the walk read its index row, and
    /// writes them at once.
    /// </summary>
    /// <typeparam name="T">The records.</typeparam>
    /// <typeparam name="TIndex">Their index, with a row per record.</typeparam>
    /// <param name="session">The YesSql session, which must not have loaded the records before <paramref name="rows"/>
    /// were read (a resumable step's session is new for each step, see <see cref="ResumableSteps"/>).</param>
    /// <param name="catalog">The catalog, which saves the records.</param>
    /// <param name="collection">The collection of the records.</param>
    /// <param name="rows">The index rows the walk read before it loaded the records.</param>
    /// <param name="records">The records the walk changed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many records were saved.</returns>
    /// <remarks>
    /// <para>
    /// Normal saves don't use YesSql's concurrency check (they don't move a document's version), so that check can't see
    /// them. Every save writes a record's index row again under a new id instead (YesSql deletes and inserts the rows of
    /// a map index on each update), so a row id that changed since the walk read it means another save landed; a row
    /// that is gone means the record was deleted. Such a record is left as it is: an idempotent walk reaches its new row
    /// later, and a bounded walk relies on the normal save applying the same change (the stores sanitize, protect and
    /// reindex on every save).
    /// </para>
    /// <para>
    /// The rows are read again just before the save, and the save is written straight after, so what remains is the
    /// time between that read and the write, a few milliseconds, as with the check of a send
    /// (<c>SurveyInvitationService</c>, R7-M7). A save that lands after the walk's write wins, which is safe for the
    /// same reason.
    /// </para>
    /// </remarks>
    public static async Task<int> SaveUnchangedAsync<T, TIndex>(
        ISession session,
        DocumentCatalog<T, TIndex> catalog,
        string collection,
        IEnumerable<TIndex> rows,
        IReadOnlyCollection<T> records,
        CancellationToken cancellationToken = default)
        where T : CatalogItem
        where TIndex : CatalogItemIndex
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return 0;
        }

        var read = LatestRows(rows);
        var current = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var chunk in records.Select(record => record.ItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Chunk(500))
        {
            foreach (var (itemId, id) in LatestRows(await session.QueryIndex<TIndex>(index => index.ItemId.IsIn(chunk), collection: collection).ListAsync(cancellationToken)))
            {
                current[itemId] = id;
            }
        }

        var saved = 0;

        foreach (var record in records)
        {
            if (string.IsNullOrEmpty(record.ItemId)
                || !read.TryGetValue(record.ItemId, out var readId)
                || !current.TryGetValue(record.ItemId, out var currentId)
                || readId != currentId)
            {
                continue;
            }

            await catalog.UpdateAsync(record, cancellationToken);
            saved++;
        }

        if (saved > 0)
        {
            await session.FlushAsync(cancellationToken);
        }

        return saved;
    }

    private static Dictionary<string, long> LatestRows<TIndex>(IEnumerable<TIndex> rows)
        where TIndex : CatalogItemIndex
    {
        var latest = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (!string.IsNullOrEmpty(row.ItemId) && (!latest.TryGetValue(row.ItemId, out var id) || row.Id > id))
            {
                latest[row.ItemId] = row.Id;
            }
        }

        return latest;
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
