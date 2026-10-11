using System.Linq.Expressions;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security.Permissions;
using System.Reflection;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources;

/// <summary>
/// A Contact Center data set whose rows are the documents of one record type in the Contact Center collection. Only a
/// principal that may view Contact Center reports, and holds every extra permission the data set asks for, may read
/// it. Records are read newest first through their index, and only the date range the report's conditions put on the
/// data set's date column is pushed down to the store.
/// </summary>
/// <typeparam name="TRecord">The record type.</typeparam>
/// <typeparam name="TIndex">The index the records are queried through.</typeparam>
public abstract class ContactCenterReportDataSet<TRecord, TIndex> : ReportRecordDataSet<TRecord>, IContactCenterReportDataSet, IReportAggregateDataSet
    where TRecord : class
    where TIndex : CatalogItemIndex
{
    // Bounds are widened by this much before they reach the store, so a date the store keeps with less precision
    // than the record never falls outside a bound the record itself is inside. The report filters every row again.
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
        ReportDataSetDescriptor descriptor,
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

    /// <inheritdoc/>
    public override string DefaultDateField => DateColumn?.Field;

    /// <summary>
    /// Gets the index columns a join can narrow the read on, by field name. Every data set has its <c>ItemId</c>;
    /// data sets add the foreign keys their index holds.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, Expression<Func<TIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<string> KeyFilterableFields => KeyColumns.Keys;

    /// <summary>
    /// Gets a value indicating whether the data set can group and aggregate itself in the database. Data sets that
    /// restrict which records a principal reads (<see cref="RestrictAsync"/>) cannot, since the grouping statement
    /// reads the whole index.
    /// </summary>
    protected virtual bool CanAggregate => true;

    /// <inheritdoc/>
    public Task<ReportAggregateTable> AggregateAsync(ReportAggregateQuery query, CancellationToken cancellationToken)
    {
        if (!CanAggregate)
        {
            return Task.FromResult<ReportAggregateTable>(null);
        }

        // The index copies these fields from the record unchanged, so a field with an index column of the same name
        // can be grouped, aggregated, and filtered there.
        _aggregateColumns ??= ReportIndexAggregator.MapByName<TIndex>(Fields.Select(field => field.Name));

        return ReportIndexAggregator.AggregateAsync<TIndex>(_session, ContactCenterStorage.CollectionName, query, _aggregateColumns, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(ReportDataSourceContext context)
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

    /// <inheritdoc/>
    protected override async Task<IEnumerable<TRecord>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
    {
        var records = await RestrictAsync(
            _session.Query<TRecord, TIndex>(collection: ContactCenterStorage.CollectionName),
            query.Context,
            cancellationToken);

        if (records is null)
        {
            return [];
        }

        foreach (var (field, column) in KeyColumns)
        {
            if (ReportJoinKeys.For(query.Conditions, field) is not { } keys)
            {
                continue;
            }

            if (keys.Count == 0)
            {
                return [];
            }

            records = records.Where(In(column, keys.ToArray()));
        }

        if (DateColumn is { } dateColumn)
        {
            var (from, to) = ReportDateRange.For(query.Conditions, dateColumn.Field);

            if (from.HasValue)
            {
                records = records.Where(Compare(dateColumn.Column, ExpressionType.GreaterThanOrEqual, Earlier(from.Value)));
            }

            if (to.HasValue)
            {
                records = records.Where(Compare(dateColumn.Column, ExpressionType.LessThanOrEqual, Later(to.Value)));
            }

            records = records
                .OrderByDescending(Boxed(dateColumn.Column))
                .ThenByDescending(index => index.Id);
        }
        else
        {
            records = records.OrderByDescending(index => index.Id);
        }

        return await records.Take(take).ListAsync(cancellationToken);
    }

    /// <summary>
    /// Narrows the query to the records the principal may read, when the data set's permission alone does not decide
    /// it. The default reads every record.
    /// </summary>
    /// <param name="records">The query.</param>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The narrowed query, or <see langword="null"/> when the principal may read no record.</returns>
    protected virtual Task<IQuery<TRecord, TIndex>> RestrictAsync(
        IQuery<TRecord, TIndex> records,
        ReportDataSourceContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(records);
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

    private static Expression<Func<TIndex, object>> Boxed(Expression<Func<TIndex, DateTime>> column)
    {
        return Expression.Lambda<Func<TIndex, object>>(Expression.Convert(column.Body, typeof(object)), column.Parameters);
    }
}
