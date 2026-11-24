using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dapper;
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
/// grouping, and only the row identifier and the date are read, so the database seeks to the page through an index on
/// the date column and the row identifier, and the other columns can't fail the read. Only then
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
            RowKey last = null;

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

                var page = await ReadKeysAsync(session, order.Sort(rows, dated).Take(PageSize), order, dated, cancellationToken);

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

    // Reads the identifier and the date of each row of a page, not the whole rows. The rows are read with YesSql's own
    // query, so with the filter, the order and the page size the caller gave, but only these two columns are selected:
    // Dapper builds the reader of a query from the kind of value of each column in its first row, and SQLite keeps a
    // decimal column's whole numbers as integers and its other numbers as real numbers, so reading whole rows fails
    // as soon as a row holds the other kind of value than the first one in any decimal column. YesSql doesn't let a
    // query over an index choose its columns, so the selector is set on the query's builder; when the query isn't the
    // one YesSql builds, such as after an update of YesSql, the whole rows are read as before.
    private static async Task<RowKey[]> ReadKeysAsync<TIndex>(
        ISession session,
        IQueryIndex<TIndex> rows,
        PageOrder<TIndex> order,
        bool dated,
        CancellationToken cancellationToken)
        where TIndex : MapIndex
    {
        var state = IndexQueryState<TIndex>.Of(rows);

        if (state is null || (dated && order.DateColumn is null))
        {
            return [.. (await rows.ListAsync(cancellationToken)).Select(row => new RowKey { Id = row.Id, Date = dated ? order.ReadDate(row) : null })];
        }

        // As YesSql reads a query: the pending changes first, on the session's connection and in its transaction.
        await session.FlushAsync(cancellationToken);

        var connection = await session.CreateConnectionAsync(cancellationToken);
        var configuration = session.Store.Configuration;
        var builder = state._sqlBuilder;
        var alias = state.GetTypeAlias(typeof(TIndex));

        state.FlushFilters();

        var selector = $"{builder.FormatColumn(alias, nameof(MapIndex.Id), configuration.Schema, isAlias: true)} AS {configuration.SqlDialect.QuoteForAliasName(nameof(RowKey.Id))}";

        if (dated)
        {
            selector += $", {builder.FormatColumn(alias, order.DateColumn, configuration.Schema, isAlias: true)} AS {configuration.SqlDialect.QuoteForAliasName(nameof(RowKey.Date))}";
        }

        builder.Selector(selector);

        var keys = await connection.QueryAsync<RowKey>(new CommandDefinition(
            builder.ToSqlString(),
            new DynamicParameters(builder.Parameters),
            session.CurrentTransaction,
            cancellationToken: cancellationToken));

        return [.. keys];
    }

    // The identifier and the date of an index row.
    private sealed class RowKey
    {
        public long Id { get; set; }

        public DateTime? Date { get; set; }
    }

    // The state of a query YesSql builds over an index, which holds its SQL builder.
    private static class IndexQueryState<TIndex>
        where TIndex : MapIndex
    {
        private static readonly FieldInfo _query = typeof(DefaultQuery.Query<TIndex>).GetField("_query", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo _state = typeof(DefaultQuery).GetField("_queryState", BindingFlags.Instance | BindingFlags.NonPublic);

        public static QueryState Of(IQueryIndex<TIndex> rows)
        {
            if (_query is null || _state is null || rows is not DefaultQuery.Query<TIndex>)
            {
                return null;
            }

            return _query.GetValue(rows) is DefaultQuery query ? _state.GetValue(query) as QueryState : null;
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
        private readonly string _dateColumn;

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

            // The column of the date, when the date is a property of the row as it is for every data set.
            if (_date is MemberExpression { Member: PropertyInfo property } member && member.Expression == _parameter)
            {
                _dateColumn = property.Name;
            }

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

        // The column of the date, or null when the date is not a property of the row.
        public string DateColumn => _dateColumn;

        public DateTime? ReadDate(TIndex row)
        {
            return (DateTime?)_readDate(row);
        }

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
        public Expression<Func<TIndex, bool>> After(RowKey row, bool dated)
        {
            var id = row.Id;

            if (!dated)
            {
                return index => index.Id < id;
            }

            var date = Expression.Constant(row.Date, _date.Type);
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
