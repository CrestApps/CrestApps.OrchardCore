using System.Runtime.CompilerServices;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Exposes saved report views as data sets, so a report or another view can build on a view's prepared result. Each
/// result column of the view becomes a field named after the column identifier. A view that reads itself, directly or
/// through other views, is refused. A scheduled view is read from its stored result while that result still has the
/// view's fields; otherwise, and for a live view, the view runs.
/// </summary>
public sealed class ReportViewsDataSource : IDataSource
{
    private readonly ICatalog<ReportView> _views;
    private readonly IAuthorizationService _authorizationService;
    private readonly ReportViewRunner _runner;
    private readonly ReportViewSnapshotStore _snapshots;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportViewsDataSource"/> class.
    /// </summary>
    /// <param name="views">The view catalog.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="runner">The view runner.</param>
    /// <param name="snapshots">The store of scheduled views' results.</param>
    public ReportViewsDataSource(
        ICatalog<ReportView> views,
        IAuthorizationService authorizationService,
        ReportViewRunner runner,
        ReportViewSnapshotStore snapshots)
    {
        _views = views;
        _authorizationService = authorizationService;
        _runner = runner;
        _snapshots = snapshots;
    }

    /// <inheritdoc/>
    public static string Name => ReportsConstants.ViewsDataSource;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ReportViewsDataSource>("Report views");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<ReportViewsDataSource>("Reusable views saved in the report builder.");

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dataSets = new List<DataSetDescriptor>();

        foreach (var view in await _views.GetAllAsync(cancellationToken))
        {
            if (await CanReadAsync(context, view))
            {
                dataSets.Add(new DataSetDescriptor(view.ItemId, view.DisplayText, view.Description));
            }
        }

        return dataSets
            .OrderBy(dataSet => dataSet.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var view = await FindReadableAsync(dataSet, context, cancellationToken);

        if (view is null)
        {
            return null;
        }

        // The schema is always the view's planned one, so designs keep working whether the view is live or scheduled.
        var plan = await _runner.EnterAsync(view, context, () => _runner.PlanValidAsync(view, context, cancellationToken));

        return new DataSetSchema
        {
            DataSet = new DataSetDescriptor(view.ItemId, view.DisplayText, view.Description),
            Fields = ReportViewRunner.DescribeFields(plan),
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var view = await FindReadableAsync(query.DataSet, query.Context, cancellationToken);

        if (view is null)
        {
            yield break;
        }

        var maxRows = query.MaxRows > 0 ? query.MaxRows : int.MaxValue;

        // The view is being read until its rows are, so a view that reads itself is refused.
        using var entered = _runner.Enter(view, query.Context);

        var plan = await _runner.PlanAsync(view, query.Context, cancellationToken);

        if (view.RefreshIntervalMinutes > ReportViewRefreshIntervals.Live && plan.IsValid)
        {
            var snapshot = await _snapshots.FindAsync(view.ItemId);

            // A snapshot taken before the view changed its fields is ignored, and the view runs, until the next
            // refresh. A view whose first refresh has not happened yet runs too.
            if (snapshot?.RefreshedUtc is not null && ReportViewSnapshotData.Matches(snapshot, ReportViewRunner.DescribeFields(plan)))
            {
                var table = ReportViewSnapshotData.ToTable(snapshot, query.Fields, maxRows);

                yield return new DataBatch(table.Fields, [.. table.Rows]);

                yield break;
            }
        }

        // A live view streams its rows as its query computes them, without keeping them.
        await foreach (var batch in _runner.ReadAsync(plan, query.Context, maxRows, query.BatchSize, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return batch;
        }
    }

    private async Task<ReportView> FindReadableAsync(string viewId, DataSourceContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(viewId))
        {
            return null;
        }

        var view = await _views.FindByIdAsync(viewId, cancellationToken);

        return view is not null && await CanReadAsync(context, view) ? view : null;
    }

    private async Task<bool> CanReadAsync(DataSourceContext context, ReportView view)
    {
        return context.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, ReportDesignerPermissions.ViewAllReportDesigns, view);
    }
}
