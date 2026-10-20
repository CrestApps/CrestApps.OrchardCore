using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using YesSql;
using YesSql.Indexes;
using YesSql.Services;

namespace CrestApps.OrchardCore.Reports.Designer.DataSources;

/// <summary>
/// Reads the records of a YesSql map index a page at a time, newest first, for the <c>LoadAsync</c> of a data set that
/// reads an index: any number of records can be read, each page costs the same however many came before it, and each
/// page leaves the session once it was read, so a long read doesn't keep every record in memory.
/// </summary>
/// <remarks>
/// A page is found in the index table alone: the rows that match the filter, ordered by a date column and then by their
/// row identifier, after the last row of the page before (keyset paging), with no join to the documents and no
/// grouping, so the database seeks to the page through an index on the date column and the row identifier. Only then
/// are the records of the page loaded, by the identifiers of their index rows. Skipping pages with an offset instead
/// makes the database read, join, group and sort every record before a page, for every page. Index the date column and
/// the row identifier together, such as with <c>CreateIndex("IDX_..._Report", "CreatedUtc", "Id")</c>, for the seek.
/// Rows without a date come last.
/// </remarks>
public static class ReportIndexPages
{
    /// <summary>
    /// The number of records read per page.
    /// </summary>
    public const int PageSize = 500;

    /// <summary>
    /// Reads the records whose index rows match a filter, newest first by a date column.
    /// </summary>
    /// <typeparam name="TRecord">The type of the records.</typeparam>
    /// <typeparam name="TIndex">The index the records are read through.</typeparam>
    /// <param name="session">The session the records are read from.</param>
    /// <param name="filter">Adds the conditions on the index rows, or <see langword="null"/> to read every record.</param>
    /// <param name="date">The date column the records are ordered by, newest first, or <see langword="null"/> to order
    /// them by their index rows only, newest first.</param>
    /// <param name="collection">The collection of the records, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The records.</returns>
    public static IAsyncEnumerable<TRecord> ReadAsync<TRecord, TIndex>(
        ISession session,
        Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>> filter,
        Expression<Func<TIndex, DateTime>> date,
        string collection = null,
        CancellationToken cancellationToken = default)
        where TRecord : class
        where TIndex : MapIndex
    {
        return Flatten(ReadPagesAsync<TRecord, TIndex>(session, filter, date, collection, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Reads the records whose index rows match a filter, newest first by a date column that may be empty.
    /// </summary>
    /// <typeparam name="TRecord">The type of the records.</typeparam>
    /// <typeparam name="TIndex">The index the records are read through.</typeparam>
    /// <param name="session">The session the records are read from.</param>
    /// <param name="filter">Adds the conditions on the index rows, or <see langword="null"/> to read every record.</param>
    /// <param name="date">The date column the records are ordered by, newest first; the records without one come
    /// last.</param>
    /// <param name="collection">The collection of the records, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The records.</returns>
    public static IAsyncEnumerable<TRecord> ReadAsync<TRecord, TIndex>(
        ISession session,
        Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>> filter,
        Expression<Func<TIndex, DateTime?>> date,
        string collection = null,
        CancellationToken cancellationToken = default)
        where TRecord : class
        where TIndex : MapIndex
    {
        return Flatten(ReadPagesAsync<TRecord, TIndex>(session, filter, date, collection, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Reads the records whose index rows match a filter a page at a time, newest first by a date column, such as to
    /// look up what the records of a page refer to at once. A page leaves the session when the next one is read.
    /// </summary>
    /// <typeparam name="TRecord">The type of the records.</typeparam>
    /// <typeparam name="TIndex">The index the records are read through.</typeparam>
    /// <param name="session">The session the records are read from.</param>
    /// <param name="filter">Adds the conditions on the index rows, or <see langword="null"/> to read every record.</param>
    /// <param name="date">The date column the records are ordered by, newest first, or <see langword="null"/> to order
    /// them by their index rows only, newest first.</param>
    /// <param name="collection">The collection of the records, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The pages of records.</returns>
    public static IAsyncEnumerable<IReadOnlyList<TRecord>> ReadPagesAsync<TRecord, TIndex>(
        ISession session,
        Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>> filter,
        Expression<Func<TIndex, DateTime>> date,
        string collection = null,
        CancellationToken cancellationToken = default)
        where TRecord : class
        where TIndex : MapIndex
    {
        ArgumentNullException.ThrowIfNull(session);

        return ReadPagesCoreAsync<TRecord, TIndex>(session, filter, new PageOrder<TIndex>(date), collection, cancellationToken);
    }

    /// <summary>
    /// Reads the records whose index rows match a filter a page at a time, newest first by a date column that may be
    /// empty, such as to look up what the records of a page refer to at once. A page leaves the session when the next
    /// one is read.
    /// </summary>
    /// <typeparam name="TRecord">The type of the records.</typeparam>
    /// <typeparam name="TIndex">The index the records are read through.</typeparam>
    /// <param name="session">The session the records are read from.</param>
    /// <param name="filter">Adds the conditions on the index rows, or <see langword="null"/> to read every record.</param>
    /// <param name="date">The date column the records are ordered by, newest first; the records without one come
    /// last.</param>
    /// <param name="collection">The collection of the records, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The pages of records.</returns>
    public static IAsyncEnumerable<IReadOnlyList<TRecord>> ReadPagesAsync<TRecord, TIndex>(
        ISession session,
        Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>> filter,
        Expression<Func<TIndex, DateTime?>> date,
        string collection = null,
        CancellationToken cancellationToken = default)
        where TRecord : class
        where TIndex : MapIndex
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(date);

        return ReadPagesCoreAsync<TRecord, TIndex>(session, filter, new PageOrder<TIndex>(date), collection, cancellationToken);
    }

    private static async IAsyncEnumerable<TRecord> Flatten<TRecord>(
        IAsyncEnumerable<IReadOnlyList<TRecord>> pages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var page in pages.WithCancellation(cancellationToken))
        {
            foreach (var record in page)
            {
                yield return record;
            }
        }
    }

    private static async IAsyncEnumerable<IReadOnlyList<TRecord>> ReadPagesCoreAsync<TRecord, TIndex>(
        ISession session,
        Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>> filter,
        PageOrder<TIndex> order,
        string collection,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TRecord : class
        where TIndex : MapIndex
    {
        // The rows with a date, newest first, then the rows without one.
        bool[] parts = order.Nullable ? [true, false] : [order.Dated];

        foreach (var dated in parts)
        {
            TIndex last = null;

            while (true)
            {
                var rows = session.QueryIndex<TIndex>(collection);

                if (filter is not null)
                {
                    rows = filter(rows);
                }

                if (order.Nullable)
                {
                    rows = rows.Where(dated ? order.WithDate : order.WithoutDate);
                }

                if (last is not null)
                {
                    rows = rows.Where(order.After(last, dated));
                }

                var page = (await order.Sort(rows, dated).Take(PageSize).ListAsync(cancellationToken)).ToArray();

                if (page.Length == 0)
                {
                    break;
                }

                var ids = page.Select(row => row.Id).ToArray();
                var records = (await order.Sort(session.Query<TRecord, TIndex>(index => index.Id.IsIn(ids), collection), dated)
                    .ListAsync(cancellationToken)).ToArray();

                if (records.Length > 0)
                {
                    yield return records;
                }

                session.Detach(records, collection);

                if (page.Length < PageSize)
                {
                    break;
                }

                last = page[^1];
            }
        }
    }

    // The order of the pages: by a date column, newest first, then by the row identifier, newest first.
    private sealed class PageOrder<TIndex>
        where TIndex : MapIndex
    {
        private static readonly Expression<Func<TIndex, object>> _rowId = index => index.Id;

        private readonly ParameterExpression _parameter;
        private readonly Expression _date;
        private readonly Expression<Func<TIndex, object>> _boxedDate;
        private readonly Func<TIndex, object> _readDate;

        public PageOrder(LambdaExpression date)
        {
            if (date is null)
            {
                return;
            }

            _parameter = date.Parameters[0];
            _date = date.Body;
            _boxedDate = Expression.Lambda<Func<TIndex, object>>(Expression.Convert(_date, typeof(object)), _parameter);
            _readDate = _boxedDate.Compile();
            Dated = true;

            if (System.Nullable.GetUnderlyingType(_date.Type) is not null)
            {
                var empty = Expression.Constant(null, _date.Type);

                Nullable = true;
                WithDate = Expression.Lambda<Func<TIndex, bool>>(Expression.NotEqual(_date, empty), _parameter);
                WithoutDate = Expression.Lambda<Func<TIndex, bool>>(Expression.Equal(_date, empty), _parameter);
            }
        }

        // Whether the records are ordered by a date before their row identifier.
        public bool Dated { get; }

        // Whether the date may be empty.
        public bool Nullable { get; }

        public Expression<Func<TIndex, bool>> WithDate { get; }

        public Expression<Func<TIndex, bool>> WithoutDate { get; }

        public IQueryIndex<TIndex> Sort(IQueryIndex<TIndex> rows, bool dated)
        {
            return dated
                ? rows.OrderByDescending(_boxedDate).ThenByDescending(_rowId)
                : rows.OrderByDescending(_rowId);
        }

        public IQuery<TRecord, TIndex> Sort<TRecord>(IQuery<TRecord, TIndex> records, bool dated)
            where TRecord : class
        {
            return dated
                ? records.OrderByDescending(_boxedDate).ThenByDescending(_rowId)
                : records.OrderByDescending(_rowId);
        }

        // The rows after a row in the order: an earlier date, or the same date and a smaller row identifier. It is
        // written as "date <= last and (date < last or id < last id)", not as "date < last or (date = last and id <
        // last id)": databases seek to a range on the first form only, and scan the index from its start to the last
        // row on the second, which makes each page cost more than the one before.
        public Expression<Func<TIndex, bool>> After(TIndex row, bool dated)
        {
            var id = row.Id;

            if (!dated)
            {
                return index => index.Id < id;
            }

            var date = Expression.Constant(_readDate(row), _date.Type);
            var rowId = Expression.Property(_parameter, nameof(MapIndex.Id));

            return Expression.Lambda<Func<TIndex, bool>>(
                Expression.AndAlso(
                    Expression.LessThanOrEqual(_date, date),
                    Expression.OrElse(
                        Expression.LessThan(_date, date),
                        Expression.LessThan(rowId, Expression.Constant(id)))),
                _parameter);
        }
    }
}
