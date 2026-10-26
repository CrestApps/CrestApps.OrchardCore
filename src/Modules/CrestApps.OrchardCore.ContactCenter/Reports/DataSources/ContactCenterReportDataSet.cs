using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.DataSources;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources;

/// <summary>
/// A Contact Center data set whose rows are the documents of one record type in the Contact Center collection. Only a
/// principal that may view Contact Center reports, and holds every extra permission the data set asks for, may read
/// it. Records are read newest first through their index, a page at a time, and only the date range the conditions put
/// on the data set's date column, and the keys of a join, are pushed down to the store.
/// </summary>
/// <typeparam name="TRecord">The record type.</typeparam>
/// <typeparam name="TIndex">The index the records are queried through.</typeparam>
public abstract class ContactCenterReportDataSet<TRecord, TIndex> : RecordDataSet<TRecord>, IContactCenterReportDataSet, IAggregateRecordDataSet
    where TRecord : class
    where TIndex : CatalogItemIndex
{
    // Bounds are widened by this much before they reach the store, so a date the store keeps with less precision
    // than the record never falls outside a bound the record itself is inside. The consumer filters every row again.
    private static readonly TimeSpan _boundSlack = TimeSpan.FromSeconds(1);

    // YesSql's IN operator, found from a sample expression so it can be applied to any key column.
    private static readonly MethodInfo _isIn = ((MethodCallExpression)((Expression<Func<string, bool>>)(value => value.IsIn(Array.Empty<string>()))).Body).Method;

    private readonly ISession _session;
    private IReadOnlyDictionary<string, string> _aggregateColumns;
    private readonly IAuthorizationService _authorizationService;
    private readonly Permission[] _permissions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterReportDataSet{TRecord, TIndex}"/> class.
    /// </summary>
    /// <param name="descriptor">The data set's descriptor.</param>
    /// <param name="session">The YesSql session records are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="permissions">The permissions a principal needs besides viewing Contact Center reports.</param>
    protected ContactCenterReportDataSet(
        DataSetDescriptor descriptor,
        ISession session,
        IAuthorizationService authorizationService,
        params Permission[] permissions)
        : base(descriptor)
    {
        _session = session;
        _authorizationService = authorizationService;
        _permissions = permissions ?? [];
    }

    /// <summary>
    /// Gets the date field whose range is pushed down to the store, and the index column that holds the same value;
    /// <see langword="null"/> when the data set has none. Records are then read newest first by that column.
    /// </summary>
    protected virtual (string Field, Expression<Func<TIndex, DateTime>> Column)? DateColumn => null;

    /// <summary>
    /// Gets the index columns a join can narrow the read on, by field name. Every data set has its <c>ItemId</c>;
    /// data sets add the foreign keys their index holds.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, Expression<Func<TIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
        };

    /// <summary>
    /// Gets a value indicating whether the data set can group and aggregate itself in the database. Data sets that
    /// restrict which records a principal reads (<see cref="RestrictAsync"/>) cannot, since the grouping statement
    /// reads the whole index.
    /// </summary>
    protected virtual bool CanAggregate => true;

    /// <inheritdoc/>
    public Task<IReadOnlyList<object[]>> AggregateAsync(DataAggregateQuery query, CancellationToken cancellationToken = default)
    {
        if (!CanAggregate)
        {
            return Task.FromResult<IReadOnlyList<object[]>>(null);
        }

        // The index copies these fields from the record unchanged, so a field with an index column of the same name
        // can be grouped, aggregated, and filtered there.
        _aggregateColumns ??= ReportIndexAggregator.MapByName<TIndex>(Fields.Select(field => field.Name));

        return ReportIndexAggregator.AggregateAsync<TIndex>(_session, ContactCenterStorage.CollectionName, query, _aggregateColumns, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (context?.User is null ||
            !await _authorizationService.AuthorizeAsync(context.User, ContactCenterPermissions.ViewReports))
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

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<TRecord> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var restrict = await RestrictAsync(query.Context, cancellationToken);

        if (restrict is null)
        {
            yield break;
        }

        var keyFilters = new List<(Expression<Func<TIndex, string>> Column, string[] Values)>();

        // Only the keys of a join are matched exactly. A viewer's filter on a key field is left to the consumer, which
        // compares text without regard to case, as an exact match in the database could drop a row it keeps.
        var joinKeys = query.Conditions?.Where(condition => condition is { IsJoinKey: true }).ToArray() ?? [];

        foreach (var (field, column) in KeyColumns)
        {
            if (DataConditions.Keys(joinKeys, field) is not { } keys)
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

        await foreach (var record in ReportIndexPages.ReadAsync<TRecord, TIndex>(
            _session,
            Filter,
            dateColumn?.Column,
            ContactCenterStorage.CollectionName,
            cancellationToken))
        {
            yield return record;
        }
    }

    /// <summary>
    /// Declares the text and enumeration columns of the index that hold the values of fields, so a viewer's filter
    /// that limits such a field to a list of values, such as a status filter, narrows the read itself. The default
    /// declares none.
    /// </summary>
    /// <param name="columns">The columns to declare the fields on.</param>
    protected virtual void DescribeColumns(ReportIndexConditions<TIndex> columns)
    {
    }

    /// <summary>
    /// Decides which records the principal may read, when the data set's permission alone does not decide it. The
    /// default reads every record.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A function that narrows the index rows read to those of the records the principal may read, or
    /// <see langword="null"/> when it may read no record.</returns>
    protected virtual Task<Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>>> RestrictAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>>>(rows => rows);
    }

    /// <summary>
    /// Gets the seconds between two instants, or <see langword="null"/> when either is missing or they are out of
    /// order.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <returns>The seconds, to the millisecond.</returns>
    protected static double? SecondsBetween(DateTime? start, DateTime? end)
    {
        return start.HasValue && end.HasValue && end.Value >= start.Value
            ? Math.Round((end.Value - start.Value).TotalSeconds, 3)
            : null;
    }

    /// <summary>
    /// Joins a list of values into one comma-separated text, or <see langword="null"/> when it is empty.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The text.</returns>
    protected static string Join(IEnumerable<string> values)
    {
        var text = string.Join(", ", (values ?? []).Where(value => !string.IsNullOrEmpty(value)));

        return text.Length == 0 ? null : text;
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
