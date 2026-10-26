using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.DataSources;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Indexes;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Reports.Designer.DataSources;

/// <summary>
/// A data set whose rows come from the documents of one record type, read through a YesSql map index a page at a time,
/// newest first. Only a principal that holds every permission the data set asks for may read it. The date range the
/// conditions put on the data set's date column, the keys of a join, and the values a viewer's filter allows for the
/// declared columns are pushed down to the index; the consumer filters every row again.
/// </summary>
/// <remarks>
/// A document can give one row, such as an order, or several, such as the lines of an order: <see cref="GetRows"/>
/// turns each document read into the rows of the data set. A data set whose rows are the documents themselves derives
/// from <see cref="IndexedReportDataSet{TRecord, TIndex}"/>.
/// </remarks>
/// <typeparam name="TDocument">The type of the documents read.</typeparam>
/// <typeparam name="TIndex">The index the documents are read through.</typeparam>
/// <typeparam name="TRow">The type of the rows of the data set.</typeparam>
public abstract class IndexedReportDataSet<TDocument, TIndex, TRow> : RecordDataSet<TRow>, IAggregateRecordDataSet
    where TDocument : class
    where TIndex : MapIndex
    where TRow : class
{
    // Bounds are widened by this much before they reach the store, so a date the store keeps with less precision
    // than the record never falls outside a bound the record itself is inside. The consumer filters every row again.
    private static readonly TimeSpan _boundSlack = TimeSpan.FromSeconds(1);

    // YesSql's IN operator, found from a sample expression so it can be applied to any key column.
    private static readonly MethodInfo _isIn = ((MethodCallExpression)((Expression<Func<string, bool>>)(value => value.IsIn(Array.Empty<string>()))).Body).Method;

    private readonly IAuthorizationService _authorizationService;
    private readonly Permission[] _permissions;
    private IReadOnlyDictionary<string, string> _aggregateColumns;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexedReportDataSet{TDocument, TIndex, TRow}"/> class.
    /// </summary>
    /// <param name="descriptor">The data set's descriptor.</param>
    /// <param name="session">The YesSql session the documents are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="collection">The collection the documents are stored in, or <see langword="null"/>.</param>
    /// <param name="permissions">The permissions a principal needs to read the data set.</param>
    protected IndexedReportDataSet(
        DataSetDescriptor descriptor,
        ISession session,
        IAuthorizationService authorizationService,
        string collection,
        params Permission[] permissions)
        : base(descriptor)
    {
        Session = session;
        Collection = collection;
        _authorizationService = authorizationService;
        _permissions = permissions ?? [];
    }

    /// <summary>
    /// Gets the YesSql session the documents are read from.
    /// </summary>
    protected ISession Session { get; }

    /// <summary>
    /// Gets the collection the documents are stored in, or <see langword="null"/>.
    /// </summary>
    protected string Collection { get; }

    /// <summary>
    /// Gets the date field whose range is pushed down to the store, and the index column that holds the same value;
    /// <see langword="null"/> when the data set has none. Documents are then read newest first by that column, and
    /// otherwise newest first by their index rows.
    /// </summary>
    protected virtual (string Field, Expression<Func<TIndex, DateTime>> Column)? DateColumn => null;

    /// <summary>
    /// Gets the text columns of the index a join can narrow the read on, by field name. Each column must hold exactly
    /// the value of its field, for every row a document gives.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, Expression<Func<TIndex, string>>> KeyColumns
        => new Dictionary<string, Expression<Func<TIndex, string>>>(StringComparer.Ordinal);

    /// <summary>
    /// Gets a value indicating whether the data set can group and aggregate itself in the database. By default, only a
    /// data set whose rows are its documents can, since the index holds one row per document. Data sets that restrict
    /// which documents a principal reads (<see cref="RestrictAsync"/>) must not, since the grouping statement reads
    /// the whole index.
    /// </summary>
    protected virtual bool CanAggregate => typeof(TRow) == typeof(TDocument);

    /// <inheritdoc/>
    public Task<IReadOnlyList<object[]>> AggregateAsync(DataAggregateQuery query, CancellationToken cancellationToken = default)
    {
        if (!CanAggregate)
        {
            return Task.FromResult<IReadOnlyList<object[]>>(null);
        }

        _aggregateColumns ??= GetAggregateColumns();

        return ReportIndexAggregator.AggregateAsync<TIndex>(Session, Collection, query, _aggregateColumns, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (context?.User is null)
        {
            return false;
        }

        foreach (var permission in _permissions)
        {
            if (!await _authorizationService.AuthorizeAsync(context.User, permission))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Finishes describing the data set once its constructor added its fields: its date field and the fields a join can
    /// narrow the read on.
    /// </summary>
    protected void CompleteDescription()
    {
        Descriptor.DefaultDateField = DateColumn?.Field;
        Fields.MarkKeyFilterable(KeyColumns.Keys);
    }

    /// <summary>
    /// Gets the rows a document gives.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The rows.</returns>
    protected abstract IEnumerable<TRow> GetRows(TDocument document);

    /// <summary>
    /// Maps the fields the index copies from the document unchanged to their index column, so they can be grouped,
    /// aggregated, and filtered there. The default maps each field to the index column of the same name; a data set
    /// whose index changes a value it copies, such as an email address kept in upper case, leaves that field out.
    /// </summary>
    /// <returns>The index column of each field, by field name.</returns>
    protected virtual IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        return ReportIndexAggregator.MapByName<TIndex>(Fields.Select(field => field.Name));
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<TRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var restrict = await RestrictAsync(query.Context, cancellationToken);

        if (restrict is null)
        {
            yield break;
        }

        var keyFilters = new List<(Expression<Func<TIndex, string>> Column, string[] Values)>();

        foreach (var (field, column) in KeyColumns)
        {
            if (DataConditions.Keys(query.Conditions, field) is not { } keys)
            {
                continue;
            }

            if (keys.Count == 0)
            {
                yield break;
            }

            keyFilters.Add((column, keys.Select(key => key?.ToString()).ToArray()));
        }

        var columns = new ReportIndexConditions<TIndex>(query.Conditions);

        DescribeColumns(columns);

        if (columns.MatchesNothing)
        {
            yield break;
        }

        var dateColumn = DateColumn;
        var (from, to) = dateColumn is { } dated
            ? DataConditions.DateRange(query.Conditions, dated.Field)
            : (null, null);

        IQueryIndex<TIndex> Filter(IQueryIndex<TIndex> rows)
        {
            rows = columns.Apply(restrict(rows));

            foreach (var (column, values) in keyFilters)
            {
                rows = rows.Where(In(column, values));
            }

            if (dateColumn is { } date)
            {
                if (from.HasValue)
                {
                    rows = rows.Where(Compare(date.Column, ExpressionType.GreaterThanOrEqual, Earlier(from.Value)));
                }

                if (to.HasValue)
                {
                    rows = rows.Where(Compare(date.Column, ExpressionType.LessThanOrEqual, Later(to.Value)));
                }
            }

            return rows;
        }

        await foreach (var document in ReportIndexPages.ReadAsync<TDocument, TIndex>(
            Session,
            Filter,
            dateColumn?.Column,
            Collection,
            cancellationToken))
        {
            foreach (var row in GetRows(document))
            {
                yield return row;
            }
        }
    }

    /// <summary>
    /// Declares the text and enumeration columns of the index that hold the values of fields, so a viewer's filter
    /// that limits such a field to a list of values, such as a status filter, narrows the read itself. Declare only a
    /// column that holds exactly the value of its field for every row a document gives. The default declares none.
    /// </summary>
    /// <param name="columns">The columns to declare the fields on.</param>
    protected virtual void DescribeColumns(ReportIndexConditions<TIndex> columns)
    {
    }

    /// <summary>
    /// Decides which documents the principal may read, when the data set's permissions alone do not decide it. The
    /// default reads every document.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A function that narrows the index rows read to those of the documents the principal may read, or
    /// <see langword="null"/> when it may read no document.</returns>
    protected virtual Task<Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>>> RestrictAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>>>(rows => rows);
    }

    private static DateTime Earlier(DateTime value)
    {
        return value.Ticks > _boundSlack.Ticks ? value - _boundSlack : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    }

    private static DateTime Later(DateTime value)
    {
        return DateTime.MaxValue.Ticks - value.Ticks > _boundSlack.Ticks ? value + _boundSlack : DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
    }

    private static Expression<Func<TIndex, bool>> Compare(Expression<Func<TIndex, DateTime>> column, ExpressionType comparison, DateTime value)
    {
        return Expression.Lambda<Func<TIndex, bool>>(
            Expression.MakeBinary(comparison, column.Body, Expression.Constant(value, typeof(DateTime))),
            column.Parameters);
    }

    private static Expression<Func<TIndex, bool>> In(Expression<Func<TIndex, string>> column, string[] values)
    {
        return Expression.Lambda<Func<TIndex, bool>>(
            Expression.Call(_isIn, column.Body, Expression.Constant(values, _isIn.GetParameters()[1].ParameterType)),
            column.Parameters);
    }
}

/// <summary>
/// A data set with one row per document of one record type, read through a YesSql map index a page at a time, newest
/// first. See <see cref="IndexedReportDataSet{TDocument, TIndex, TRow}"/>.
/// </summary>
/// <typeparam name="TRecord">The type of the records.</typeparam>
/// <typeparam name="TIndex">The index the records are read through.</typeparam>
public abstract class IndexedReportDataSet<TRecord, TIndex> : IndexedReportDataSet<TRecord, TIndex, TRecord>
    where TRecord : class
    where TIndex : MapIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IndexedReportDataSet{TRecord, TIndex}"/> class.
    /// </summary>
    /// <param name="descriptor">The data set's descriptor.</param>
    /// <param name="session">The YesSql session the records are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="collection">The collection the records are stored in, or <see langword="null"/>.</param>
    /// <param name="permissions">The permissions a principal needs to read the data set.</param>
    protected IndexedReportDataSet(
        DataSetDescriptor descriptor,
        ISession session,
        IAuthorizationService authorizationService,
        string collection,
        params Permission[] permissions)
        : base(descriptor, session, authorizationService, collection, permissions)
    {
    }

    /// <inheritdoc/>
    protected sealed override IEnumerable<TRecord> GetRows(TRecord document)
    {
        yield return document;
    }
}
