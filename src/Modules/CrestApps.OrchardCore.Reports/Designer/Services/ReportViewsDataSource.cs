using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Exposes saved report views as data sets, so a report or another view can build on a view's prepared result. Each
/// result column of the view becomes a field named after the column identifier. A view that reads itself, directly or
/// through other views, is refused.
/// </summary>
public sealed class ReportViewsDataSource : IReportDataSource
{
    private const string StackKey = "ReportViews:Stack";
    private const int MaximumDepth = 8;

    private readonly ICatalog<ReportView> _views;
    private readonly IAuthorizationService _authorizationService;
    private readonly Lazy<ReportQueryPlanner> _planner;
    private readonly Lazy<ReportQueryEngine> _engine;
    private readonly ReportExecutionContextFactory _contextFactory;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportViewsDataSource"/> class.
    /// </summary>
    /// <param name="views">The view catalog.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="planner">The query planner, resolved lazily because it reads this data source.</param>
    /// <param name="engine">The query engine, resolved lazily because it reads this data source.</param>
    /// <param name="contextFactory">The run context factory.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportViewsDataSource(
        ICatalog<ReportView> views,
        IAuthorizationService authorizationService,
        Lazy<ReportQueryPlanner> planner,
        Lazy<ReportQueryEngine> engine,
        ReportExecutionContextFactory contextFactory,
        IStringLocalizer<ReportViewsDataSource> stringLocalizer)
    {
        _views = views;
        _authorizationService = authorizationService;
        _planner = planner;
        _engine = engine;
        _contextFactory = contextFactory;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => ReportsConstants.ViewsDataSource;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Report views"];

    /// <inheritdoc/>
    public LocalizedString Description => S["Reusable views saved in the report designer."];

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

        var plan = await EnterAsync(view, context, () => _planner.Value.PlanAsync(view.Query, context, cancellationToken));

        if (!plan.IsValid)
        {
            throw new ReportQueryException(S["The view '{0}' cannot run: {1}", view.DisplayText, string.Join(" ", plan.Errors)]);
        }

        return new ReportDataSetSchema
        {
            DataSet = new ReportDataSetDescriptor(view.ItemId, view.DisplayText, view.Description),
            Fields = plan.Columns
                .Select(column => new ReportFieldDescriptor(column.Definition.Id, column.Label, column.DataType)
                {
                    IsIdentifier = column.Field.Kind == PlannedFieldKind.DataSetField &&
                        column.Definition.Aggregate == ReportAggregate.None &&
                        column.Field.FieldName.EndsWith("Id", StringComparison.Ordinal),
                })
                .ToList(),
        };
    }

    /// <inheritdoc/>
    public async Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var view = await FindReadableAsync(query.DataSet, query.Context, cancellationToken) ??
            throw new ReportQueryException(S["The view '{0}' does not exist or is not available.", query.DataSet]);

        return await EnterAsync(view, query.Context, async () =>
        {
            var plan = await _planner.Value.PlanAsync(view.Query, query.Context, cancellationToken);
            var context = await _contextFactory.CreateAsync(query.Context.User, query.Context);

            context.Limits.MaxResultRows = Math.Max(1, query.MaxRows);

            var result = await _engine.Value.ExecuteAsync(plan, context, cancellationToken);
            var fields = result.Columns
                .Select(column => new ReportFieldDescriptor(column.Id, column.Label, column.DataType))
                .ToList();
            var rows = new List<object[]>(result.Rows.Count);

            foreach (var source in result.Rows)
            {
                var row = new object[source.Length];

                for (var index = 0; index < source.Length; index++)
                {
                    row[index] = result.Columns[index].DataType == ReportDataType.DateTime && source[index] is DateTime local
                        ? context.ToUtc(local)
                        : source[index];
                }

                rows.Add(row);
            }

            return new ReportDataTable
            {
                Fields = fields,
                Rows = rows,
                Truncated = result.Warnings.Count > 0 && rows.Count >= query.MaxRows,
            };
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

    private async Task<T> EnterAsync<T>(ReportView view, ReportDataSourceContext context, Func<Task<T>> action)
    {
        if (!context.Properties.TryGetValue(StackKey, out var value) || value is not HashSet<string> stack)
        {
            stack = new HashSet<string>(StringComparer.Ordinal);
            context.Properties[StackKey] = stack;
        }

        if (stack.Contains(view.ItemId))
        {
            throw new ReportQueryException(S["The view '{0}' reads itself through other views. Remove the loop.", view.DisplayText]);
        }

        if (stack.Count >= MaximumDepth)
        {
            throw new ReportQueryException(S["Views are nested more than {0} levels deep.", MaximumDepth]);
        }

        stack.Add(view.ItemId);

        try
        {
            return await action();
        }
        finally
        {
            stack.Remove(view.ItemId);
        }
    }
}
