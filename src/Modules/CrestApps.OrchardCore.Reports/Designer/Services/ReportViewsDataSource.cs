using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Exposes saved report views as data sets, so a report or another view can build on a view's prepared result. Each
/// result column of the view becomes a field named after the column identifier. A view that reads itself, directly or
/// through other views, is refused. A scheduled view is read from its stored result while that result still has the
/// view's fields; otherwise, and for a live view, the view runs.
/// </summary>
public sealed class ReportViewsDataSource : IReportDataSource
{
    private readonly ICatalog<ReportView> _views;
    private readonly IAuthorizationService _authorizationService;
    private readonly ReportViewRunner _runner;
    private readonly ReportViewSnapshotStore _snapshots;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportViewsDataSource"/> class.
    /// </summary>
    /// <param name="views">The view catalog.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="runner">The view runner.</param>
    /// <param name="snapshots">The store of scheduled views' results.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportViewsDataSource(
        ICatalog<ReportView> views,
        IAuthorizationService authorizationService,
        ReportViewRunner runner,
        ReportViewSnapshotStore snapshots,
        IStringLocalizer<ReportViewsDataSource> stringLocalizer)
    {
        _views = views;
        _authorizationService = authorizationService;
        _runner = runner;
        _snapshots = snapshots;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => ReportsConstants.ViewsDataSource;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Report views"];

    /// <inheritdoc/>
    public LocalizedString Description => S["Reusable views saved in the report builder."];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dataSets = new List<ReportDataSetDescriptor>();

        foreach (var view in await _views.GetAllAsync(cancellationToken))
        {
            if (await CanReadAsync(context, view))
            {
                dataSets.Add(new ReportDataSetDescriptor(view.ItemId, view.DisplayText, view.Description));
            }
        }

        return dataSets
            .OrderBy(dataSet => dataSet.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var view = await FindReadableAsync(dataSet, context, cancellationToken);

        if (view is null)
        {
            return null;
        }

        // The schema is always the view's planned one, so designs keep working whether the view is live or scheduled.
        var plan = await _runner.EnterAsync(view, context, () => _runner.PlanValidAsync(view, context, cancellationToken));

        return new ReportDataSetSchema
        {
            DataSet = new ReportDataSetDescriptor(view.ItemId, view.DisplayText, view.Description),
            Fields = ReportViewRunner.DescribeFields(plan),
        };
    }

    /// <inheritdoc/>
    public async Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var view = await FindReadableAsync(query.DataSet, query.Context, cancellationToken) ??
            throw new ReportQueryException(S["The view '{0}' does not exist or is not available.", query.DataSet]);

        return await _runner.EnterAsync(view, query.Context, async () =>
        {
            var plan = await _runner.PlanAsync(view, query.Context, cancellationToken);

            if (view.RefreshIntervalMinutes > ReportViewRefreshIntervals.Live && plan.IsValid)
            {
                var snapshot = await _snapshots.FindAsync(view.ItemId);

                // A snapshot taken before the view changed its fields is ignored, and the view runs, until the next
                // refresh. A view whose first refresh has not happened yet runs too.
                if (snapshot?.RefreshedUtc is not null && ReportViewSnapshotData.Matches(snapshot, ReportViewRunner.DescribeFields(plan)))
                {
                    return ReportViewSnapshotData.ToTable(snapshot, query.Fields, query.MaxRows);
                }
            }

            return await _runner.ExecuteAsync(plan, query.Context, query.MaxRows, cancellationToken);
        });
    }

    private async Task<ReportView> FindReadableAsync(string viewId, ReportDataSourceContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(viewId))
        {
            return null;
        }

        var view = await _views.FindByIdAsync(viewId, cancellationToken);

        return view is not null && await CanReadAsync(context, view) ? view : null;
    }

    private async Task<bool> CanReadAsync(ReportDataSourceContext context, ReportView view)
    {
        return context.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, ReportDesignerPermissions.ViewAllReportDesigns, view);
    }
}
