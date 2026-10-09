using System.Security.Claims;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Runs a designed report and builds its document and filter controls. A saved report reads its data with its
/// owner's access, whoever views it; the designer preview reads with the designer's access.
/// </summary>
public sealed class ReportDesignRunner
{
    private readonly ReportQueryPlanner _planner;
    private readonly ReportQueryEngine _engine;
    private readonly ReportDesignDocumentBuilder _documentBuilder;
    private readonly ReportExecutionContextFactory _contextFactory;
    private readonly ReportOwnerPrincipalResolver _ownerResolver;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignRunner"/> class.
    /// </summary>
    /// <param name="planner">The query planner.</param>
    /// <param name="engine">The query engine.</param>
    /// <param name="documentBuilder">The document builder.</param>
    /// <param name="contextFactory">The run context factory.</param>
    /// <param name="ownerResolver">The report owner principal resolver.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignRunner(
        ReportQueryPlanner planner,
        ReportQueryEngine engine,
        ReportDesignDocumentBuilder documentBuilder,
        ReportExecutionContextFactory contextFactory,
        ReportOwnerPrincipalResolver ownerResolver,
        ILogger<ReportDesignRunner> logger,
        IStringLocalizer<ReportDesignRunner> stringLocalizer)
    {
        _planner = planner;
        _engine = engine;
        _documentBuilder = documentBuilder;
        _contextFactory = contextFactory;
        _ownerResolver = ownerResolver;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Runs a saved report with its owner's access.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="filterValues">The values entered for exposed filters, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The run outcome.</returns>
    public async Task<ReportRunResult> RunAsync(
        ReportDesign design,
        IDictionary<string, IList<string>> filterValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(design);

        var owner = await _ownerResolver.ResolveAsync(design.OwnerId);

        if (owner is null)
        {
            _logger.LogWarning("Designed report '{ReportId}' cannot run because its owner '{OwnerId}' no longer exists or is disabled.", design.ItemId, design.OwnerId);

            var result = new ReportRunResult();
            result.Errors.Add(S["This report cannot run because its owner no longer has an active account. Ask an administrator to give it a new owner."]);

            return result;
        }

        return await RunAsync(design, owner, filterValues, cancellationToken);
    }

    /// <summary>
    /// Runs a report with the access of a given principal.
    /// </summary>
    /// <param name="design">The report, saved or not.</param>
    /// <param name="dataUser">The principal whose access reads the data.</param>
    /// <param name="filterValues">The values entered for exposed filters, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The run outcome.</returns>
    public async Task<ReportRunResult> RunAsync(
        ReportDesign design,
        ClaimsPrincipal dataUser,
        IDictionary<string, IList<string>> filterValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(design);

        var result = new ReportRunResult();
        var context = await _contextFactory.CreateAsync(dataUser);

        foreach (var (filterId, values) in filterValues ?? new Dictionary<string, IList<string>>())
        {
            context.FilterValues[filterId] = values;
        }

        try
        {
            var plan = await _planner.PlanAsync(design.Query ?? new ReportQueryDefinition(), context.DataSourceContext, cancellationToken);

            if (!plan.IsValid)
            {
                foreach (var error in plan.Errors)
                {
                    result.Errors.Add(error);
                }

                return result;
            }

            var queryResult = await _engine.ExecuteAsync(plan, context, cancellationToken);
            var visuals = design.Visuals ?? [];

            foreach (var error in _documentBuilder.Validate(visuals, queryResult.Columns))
            {
                result.Warnings.Add(error);
            }

            foreach (var warning in queryResult.Warnings)
            {
                result.Warnings.Add(warning);
            }

            result.Document = _documentBuilder.Build(design.DisplayText, visuals, queryResult);
            result.Columns = queryResult.Columns;
            result.RowCount = queryResult.Rows.Count;

            foreach (var filter in plan.Filters.Where(filter => filter.Definition.Exposed))
            {
                result.Filters.Add(new ReportExposedFilter
                {
                    Definition = filter.Definition,
                    DataType = filter.DataType,
                    Control = ResolveControl(filter.Definition, filter.DataType),
                    Values = context.FilterValues.TryGetValue(filter.Definition.Id, out var values)
                        ? values
                        : filter.Definition.Values,
                    Options = queryResult.FilterOptions.TryGetValue(filter.Definition.Id, out var options) ? options : [],
                });
            }
        }
        catch (ReportQueryException exception)
        {
            foreach (var error in exception.Errors)
            {
                result.Errors.Add(error);
            }
        }

        return result;
    }

    /// <summary>
    /// Picks the control of an exposed filter when its definition asks for an automatic choice.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="dataType">The type of the filtered values.</param>
    /// <returns>The control to render.</returns>
    public static ReportFilterControl ResolveControl(ReportFilterDefinition filter, ReportDataType dataType)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.Control != ReportFilterControl.Auto)
        {
            return filter.Control;
        }

        if (dataType == ReportDataType.Boolean)
        {
            return ReportFilterControl.Boolean;
        }

        if (filter.Operator == ReportFilterOperator.Between)
        {
            return ReportDataValues.IsTemporal(dataType) ? ReportFilterControl.DateRange : ReportFilterControl.NumberRange;
        }

        if (ReportQueryEngine.UsesOptions(filter, dataType))
        {
            return filter.Operator is ReportFilterOperator.In or ReportFilterOperator.NotIn
                ? ReportFilterControl.MultiSelect
                : ReportFilterControl.Select;
        }

        return ReportFilterControl.Text;
    }
}

/// <summary>
/// The outcome of running a designed report.
/// </summary>
public sealed class ReportRunResult
{
    /// <summary>
    /// Gets or sets the report document, or <see langword="null"/> when the report could not run.
    /// </summary>
    public ReportDocument Document { get; set; }

    /// <summary>
    /// Gets or sets the result columns.
    /// </summary>
    public IReadOnlyList<ReportResultColumn> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of result rows.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Gets the problems that stopped the report from running.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets the problems that did not stop the report, such as data that was cut off at a limit.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets the exposed filters to render, with their current values and choices.
    /// </summary>
    public IList<ReportExposedFilter> Filters { get; } = [];
}

/// <summary>
/// An exposed filter ready to render.
/// </summary>
public sealed class ReportExposedFilter
{
    /// <summary>
    /// Gets or sets the filter definition.
    /// </summary>
    public ReportFilterDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the type of the filtered values.
    /// </summary>
    public ReportDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets the control to render.
    /// </summary>
    public ReportFilterControl Control { get; set; }

    /// <summary>
    /// Gets or sets the current values.
    /// </summary>
    public IList<string> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets the choices of a drop-down or multi-select filter.
    /// </summary>
    public IReadOnlyList<ReportFilterOption> Options { get; set; } = [];
}
