using System.Linq.Expressions;
using System.Reflection;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.DataSources;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Indexes;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Workforce.Reports;

/// <summary>
/// The base of the Workforce data sets: rows are limited to the teams the reader manages (all of them with Manage the
/// whole workforce), and carry their worker's and team's names.
/// </summary>
/// <typeparam name="TRecord">The record's type.</typeparam>
/// <typeparam name="TIndex">The index the records are read through.</typeparam>
public abstract class WorkforceReportDataSet<TRecord, TIndex> : IndexedReportDataSet<TRecord, TIndex, WorkforceReportRow<TRecord>>, IWorkforceReportDataSet
    where TRecord : class
    where TIndex : MapIndex
{
    private static readonly MethodInfo _isIn = ((MethodCallExpression)((Expression<Func<string, bool>>)(value => value.IsIn(Array.Empty<string>()))).Body).Method;

    private readonly IWorkforceScopeService _scopeService;
    private Dictionary<string, string> _workers = new(StringComparer.Ordinal);
    private Dictionary<string, string> _teams = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceReportDataSet{TRecord, TIndex}"/> class.
    /// </summary>
    /// <param name="descriptor">The data set's descriptor.</param>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="permission">The permission the reader needs.</param>
    protected WorkforceReportDataSet(DataSetDescriptor descriptor, ISession session, IAuthorizationService authorizationService, IWorkforceScopeService scopeService, Permission permission)
        : base(descriptor, session, authorizationService, WorkforceConstants.CollectionName, permission)
    {
        _scopeService = scopeService;
    }

    /// <summary>
    /// Gets the index column of the record's team.
    /// </summary>
    protected abstract Expression<Func<TIndex, string>> TeamColumn { get; }

    /// <summary>
    /// Returns the record's worker.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The worker's id.</returns>
    protected abstract string WorkerOf(TRecord record);

    /// <summary>
    /// Returns the record's team.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The team's id.</returns>
    protected abstract string TeamOf(TRecord record);

    /// <inheritdoc />
    protected override IEnumerable<WorkforceReportRow<TRecord>> GetRows(TRecord document)
    {
        yield return Row(document);
    }

    /// <summary>
    /// Makes a row of a record, with its names.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The row.</returns>
    protected WorkforceReportRow<TRecord> Row(TRecord record)
    {
        return new WorkforceReportRow<TRecord>
        {
            Record = record,
            WorkerName = _workers.GetValueOrDefault(WorkerOf(record) ?? string.Empty),
            TeamName = _teams.GetValueOrDefault(TeamOf(record) ?? string.Empty),
        };
    }

    /// <inheritdoc />
    protected override async Task<Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.GetManagedScopeAsync(context?.User, cancellationToken);

        if (!scope.All && scope.TeamIds.Count == 0)
        {
            return null;
        }

        _workers = (await Session.QueryIndex<WorkerIndex>(collection: WorkforceConstants.CollectionName).ListAsync(cancellationToken))
            .GroupBy(index => index.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.Ordinal);
        _teams = (await Session.QueryIndex<TeamIndex>(collection: WorkforceConstants.CollectionName).ListAsync(cancellationToken))
            .GroupBy(index => index.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);

        if (scope.All)
        {
            return rows => rows;
        }

        var teamIds = scope.TeamIds.ToArray();
        var filter = Expression.Lambda<Func<TIndex, bool>>(
            Expression.Call(_isIn, TeamColumn.Body, Expression.Constant(teamIds, _isIn.GetParameters()[1].ParameterType)),
            TeamColumn.Parameters);

        return rows => rows.Where(filter);
    }
}
