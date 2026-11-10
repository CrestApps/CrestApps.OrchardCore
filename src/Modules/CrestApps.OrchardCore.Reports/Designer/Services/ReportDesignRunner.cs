using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;

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
    public Task<ReportRunResult> RunAsync(
        ReportDesign design,
        IDictionary<string, IList<string>> filterValues,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(design, filterValues, period: null, cancellationToken);
    }

    /// <summary>
    /// Runs a saved report with its owner's access, for a period set on its first date filter its viewers can change.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="filterValues">The values entered for exposed filters, or <see langword="null"/> for the defaults.</param>
    /// <param name="period">
    /// The days the report covers, set on the first date range or relative date filter its viewers can change (see
    /// <see cref="FindPeriodFilter"/>), or <see langword="null"/> to keep that filter's value.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The run outcome.</returns>
    public Task<ReportRunResult> RunAsync(
        ReportDesign design,
        IDictionary<string, IList<string>> filterValues,
        ReportRunPeriod period,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(design, filterValues, period, options: null, cancellationToken);
    }

    /// <summary>
    /// Runs a saved report with its owner's access, with the values picked by clicking its visuals.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="filterValues">The values entered for exposed filters, or <see langword="null"/> for the defaults.</param>
    /// <param name="period">The days the report covers, or <see langword="null"/> to keep its filters' values.</param>
    /// <param name="options">The picked values and the visuals to show, or <see langword="null"/> for none and all.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The run outcome.</returns>
    public async Task<ReportRunResult> RunAsync(
        ReportDesign design,
        IDictionary<string, IList<string>> filterValues,
        ReportRunPeriod period,
        ReportRunOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(design);

        var owner = await _ownerResolver.ResolveAsync(design.OwnerId);

        if (owner is null)
        {
            _logger.LogWarning("Designed report '{ReportId}' cannot run because its owner '{OwnerId}' no longer exists or is disabled.", design.ItemId, design.OwnerId);

            var tags = ReportTelemetry.Tags(options?.Source);

            tags.Add(CrestAppsOrchardCoreTelemetry.ReasonTag, "owner_missing");
            ReportTelemetry.Add(ReportTelemetry.RejectedRuns, tags);

            var result = new ReportRunResult();
            result.Errors.Add(S["This report cannot run because its owner no longer has an active account. Ask someone who designs reports to clone it and share the copy again."]);

            return result;
        }

        return await RunAsync(design, owner, filterValues, period, options, cancellationToken);
    }

    /// <summary>
    /// Runs a report with the access of a given principal.
    /// </summary>
    /// <param name="design">The report, saved or not.</param>
    /// <param name="dataUser">The principal whose access reads the data.</param>
    /// <param name="filterValues">The values entered for exposed filters, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The run outcome.</returns>
    public Task<ReportRunResult> RunAsync(
        ReportDesign design,
        ClaimsPrincipal dataUser,
        IDictionary<string, IList<string>> filterValues,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(design, dataUser, filterValues, period: null, cancellationToken);
    }

    /// <summary>
    /// Runs a report with the access of a given principal, for a period set on its first date filter its viewers can
    /// change.
    /// </summary>
    /// <param name="design">The report, saved or not.</param>
    /// <param name="dataUser">The principal whose access reads the data.</param>
    /// <param name="filterValues">The values entered for exposed filters, or <see langword="null"/> for the defaults.</param>
    /// <param name="period">
    /// The days the report covers, set on the first date range or relative date filter its viewers can change (see
    /// <see cref="FindPeriodFilter"/>), or <see langword="null"/> to keep that filter's value.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The run outcome.</returns>
    public Task<ReportRunResult> RunAsync(
        ReportDesign design,
        ClaimsPrincipal dataUser,
        IDictionary<string, IList<string>> filterValues,
        ReportRunPeriod period,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(design, dataUser, filterValues, period, options: null, cancellationToken);
    }

    /// <summary>
    /// Runs a report with the access of a given principal, with the values picked by clicking its visuals. A visual that
    /// a click filters runs its query again with the picked values; the visual clicked keeps all of its rows and
    /// highlights the picked ones.
    /// </summary>
    /// <param name="design">The report, saved or not.</param>
    /// <param name="dataUser">The principal whose access reads the data.</param>
    /// <param name="filterValues">The values entered for exposed filters, or <see langword="null"/> for the defaults.</param>
    /// <param name="period">The days the report covers, or <see langword="null"/> to keep its filters' values.</param>
    /// <param name="options">The picked values and the visuals to show, or <see langword="null"/> for none and all.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The run outcome.</returns>
    public async Task<ReportRunResult> RunAsync(
        ReportDesign design,
        ClaimsPrincipal dataUser,
        IDictionary<string, IList<string>> filterValues,
        ReportRunPeriod period,
        ReportRunOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(design);

        var selections = options?.Selections ?? new Dictionary<string, IList<string>>(StringComparer.Ordinal);
        var only = options?.VisualIds;
        var source = ReportRunSources.Normalize(options?.Source);
        using var activity = ReportTelemetry.Start("report.run");
        var started = Stopwatch.GetTimestamp();
        var queries = new RunQueries();

        activity?.SetTag(ReportTelemetry.ReportIdTag, design.ItemId);
        activity?.SetTag(ReportTelemetry.ReportTitleTag, design.DisplayText);
        activity?.SetTag(ReportTelemetry.RunSourceTag, source);
        activity?.SetTag(ReportTelemetry.SelectionTag, selections.Values.Any(keys => keys?.Count > 0));
        activity?.SetTag("crestapps.report.refreshed_visuals", only is null ? null : string.Join(',', only));

        var outcome = CrestAppsOrchardCoreTelemetry.OutcomeError;
        ReportRunResult result = null;

        try
        {
            result = await RunCoreAsync(design, dataUser, filterValues, period, selections, only, source, queries, cancellationToken);

            outcome = result.Errors.Count > 0 ? CrestAppsOrchardCoreTelemetry.OutcomeError : CrestAppsOrchardCoreTelemetry.OutcomeOk;

            activity?.SetTag("crestapps.report.rows.returned", result.RowCount);
            activity?.SetTag("crestapps.report.warnings", result.Warnings.Count);

            if (result.Errors.Count > 0)
            {
                activity?.SetStatus(ActivityStatusCode.Error, string.Join(' ', result.Errors));
            }

            return result;
        }
        catch (Exception exception)
        {
            outcome = ReportTelemetry.GetOutcome(exception);
            ReportTelemetry.Fail(activity, exception);

            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);

            activity?.SetTag(ReportTelemetry.OutcomeTag, LegacyOutcome(outcome));
            activity?.SetTag(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome);
            activity?.SetTag("crestapps.report.queries", queries.Executed);
            activity?.SetTag("crestapps.report.queries.failed", queries.Invalid + queries.Failed);
            RecordRun(source, outcome, elapsed, queries);
            LogRun(design, source, elapsed, result);
        }
    }

    // The outcome of the legacy crestapps.report.outcome tag, kept for one release next to the shared outcome tag.
    private static string LegacyOutcome(string outcome)
    {
        return outcome switch
        {
            CrestAppsOrchardCoreTelemetry.OutcomeOk => "success",
            CrestAppsOrchardCoreTelemetry.OutcomeCancelled => "canceled",
            _ => "failure",
        };
    }

    // A run counts once in the run duration, and once among the failures at the run level when it failed; its queries
    // count in the queries histogram, and those that did not run in the skipped queries.
    private static void RecordRun(string source, string outcome, TimeSpan elapsed, RunQueries queries)
    {
        var tags = ReportTelemetry.Tags(source);

        if (ReportTelemetry.RunDuration.Enabled)
        {
            var durationTags = tags;

            durationTags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome);
            durationTags.Add(ReportTelemetry.OutcomeTag, LegacyOutcome(outcome));
            ReportTelemetry.RunDuration.Record(elapsed.TotalSeconds, durationTags);
        }

        if (outcome is CrestAppsOrchardCoreTelemetry.OutcomeError or CrestAppsOrchardCoreTelemetry.OutcomeTimeout && ReportTelemetry.Failures.Enabled)
        {
            var failureTags = tags;

            failureTags.Add(ReportTelemetry.LevelTag, ReportTelemetry.LevelRun);
            ReportTelemetry.Failures.Add(1, failureTags);
        }

        if (ReportTelemetry.RunQueries.Enabled)
        {
            ReportTelemetry.RunQueries.Record(queries.Executed, tags);
        }

        if (ReportTelemetry.SkippedQueries.Enabled)
        {
            AddSkipped(tags, "invalid", queries.Invalid);
            AddSkipped(tags, "failed", queries.Skipped);
        }
    }

    private static void AddSkipped(TagList tags, string reason, int count)
    {
        if (count <= 0)
        {
            return;
        }

        tags.Add(CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
        ReportTelemetry.SkippedQueries.Add(count, tags);
    }

    // A query whose plan is invalid counts once among the failures at the query level: the engine never runs it, so it
    // does not count it.
    private static void RecordQueryFailure(string source)
    {
        if (!ReportTelemetry.Failures.Enabled)
        {
            return;
        }

        var tags = ReportTelemetry.Tags(source);

        tags.Add(ReportTelemetry.LevelTag, ReportTelemetry.LevelQuery);
        ReportTelemetry.Failures.Add(1, tags);
    }

    private void LogRun(ReportDesign design, string source, TimeSpan elapsed, ReportRunResult result)
    {
        if (result is null)
        {
            return;
        }

        if (result.Errors.Count > 0)
        {
            ReportsModuleLog.RunEndedWithErrors(_logger, design.ItemId.SanitizeLogValue(), source, (long)elapsed.TotalMilliseconds, result.Errors.Count);
        }
        else if (elapsed > ReportTelemetry.SlowRunThreshold)
        {
            ReportsModuleLog.SlowRun(_logger, design.ItemId.SanitizeLogValue(), source, (long)elapsed.TotalMilliseconds, (long)ReportTelemetry.SlowRunThreshold.TotalMilliseconds, result.RowCount, result.Warnings.Count);
        }
    }

    private async Task<ReportRunResult> RunCoreAsync(
        ReportDesign design,
        ClaimsPrincipal dataUser,
        IDictionary<string, IList<string>> filterValues,
        ReportRunPeriod period,
        IDictionary<string, IList<string>> selections,
        ISet<string> only,
        string source,
        RunQueries stats,
        CancellationToken cancellationToken)
    {
        var result = new ReportRunResult();
        var context = await _contextFactory.CreateAsync(dataUser);

        context.Source = source;
        context.ReportId = design.ItemId;
        context.ReportTitle = design.DisplayText;
        context.SharedReads = new ReportSharedReads(context.Limits.MaxSharedReadRows);

        foreach (var (filterId, values) in filterValues ?? new Dictionary<string, IList<string>>())
        {
            context.FilterValues[filterId] = values;
        }

        var visuals = (design.Visuals ?? []).Where(visual => visual is not null).ToList();
        var queries = design.GetQueries().ToList();
        var sections = new List<ReportSection>();
        var shown = only is null ? visuals : visuals.Where(visual => only.Contains(visual.Id)).ToList();
        var selectionFilters = GetSelectionFilters(queries, visuals, selections, result);
        var jobs = new List<QueryJob>();

        foreach (var visual in visuals.Where(visual => !queries.Any(query => SameQuery(query.Id, visual.QueryId))))
        {
            result.Warnings.Add(S["The visual '{0}' shows a query the report no longer has.", visual.Title ?? visual.Id]);
        }

        foreach (var query in queries)
        {
            var isMain = string.IsNullOrEmpty(query.Id);
            var queryVisuals = shown.Where(visual => SameQuery(query.Id, visual.QueryId)).ToList();

            // The main query always runs for the whole report: its result is the report's rows, and it shows one table
            // when the report has no visuals. Another query runs only for the visuals that show it.
            if (queryVisuals.Count == 0 && (!isMain || only is not null))
            {
                continue;
            }

            // The visuals a click filters run the query again with the picked values, once for each set of values; the
            // visuals no click filters run it as it is, first, so its filter choices and rows are the unfiltered ones.
            var runs = queryVisuals
                .GroupBy(visual => SelectionKey(selectionFilters.GetValueOrDefault(visual.Id)), StringComparer.Ordinal)
                .OrderBy(group => group.Key.Length > 0)
                .Select(group => (Filters: selectionFilters.GetValueOrDefault(group.First().Id), Visuals: group.ToList()))
                .ToList();

            if (runs.Count == 0)
            {
                runs.Add((null, []));
            }

            for (var index = 0; index < runs.Count; index++)
            {
                var (filters, runVisuals) = runs[index];

                jobs.Add(new QueryJob
                {
                    Query = query,
                    Run = filters is null ? query : WithFilters(query, filters),
                    Visuals = runVisuals,
                    IsMain = isMain,

                    // The report's rows are those of the main query's first run: the unfiltered one, when it has one.
                    GivesRows = isMain && index == 0,
                });
            }
        }

        // Every query is planned before any runs, so the queries that read the same data sets read them once: the first
        // one keeps the rows and asks for the fields all of them need.
        foreach (var job in jobs)
        {
            try
            {
                job.Plan = await PlanQueryAsync(job.Run, context, period, result, cancellationToken);
            }
            catch (ReportQueryException exception)
            {
                RecordQueryFailure(source);

                if (AddErrors(job, exception, result))
                {
                    stats.Invalid++;

                    return result;
                }
            }

            if (job.Plan is null)
            {
                stats.Invalid++;

                if (job.IsMain)
                {
                    return result;
                }

                continue;
            }

            context.SharedReads.Register(job.Plan);
        }

        foreach (var job in jobs.Where(job => job.Plan is not null))
        {
            // The other runs of a query that failed are not run.
            if (job.Skipped)
            {
                stats.Skipped++;

                continue;
            }

            try
            {
                stats.Executed++;

                var queryResult = await RunQueryAsync(job, job.IsMain && visuals.Count == 0, context, result, sections, selections, cancellationToken);

                if (job.GivesRows)
                {
                    result.Columns = queryResult.Columns;
                    result.RowCount = queryResult.Rows.Count;
                    result.Rows = queryResult.Rows;
                }
            }
            catch (ReportQueryException exception)
            {
                stats.Failed++;

                if (AddErrors(job, exception, result))
                {
                    return result;
                }

                // The other runs of a query that can't run are skipped, like before.
                foreach (var other in jobs.Where(other => other.Query == job.Query))
                {
                    other.Skipped = true;
                }
            }
        }

        // The sections of every query, in the order of the report's visuals.
        result.Document = new ReportDocument
        {
            Title = design.DisplayText,
        };

        if (visuals.Count == 0)
        {
            foreach (var section in sections)
            {
                result.Document.Add(section);
            }
        }
        else
        {
            foreach (var visual in shown)
            {
                result.Document.Add(sections.FirstOrDefault(section => string.Equals(section.Id, visual.Id, StringComparison.Ordinal)));
            }
        }

        ReportDesignDocumentBuilder.AddContainers(result.Document, design.Containers);

        return result;
    }

    // Adds the problems of a query that can't run: the main query's are errors, another query's are warnings that name it.
    // Returns whether the report can't run.
    private bool AddErrors(QueryJob job, ReportQueryException exception, ReportRunResult result)
    {
        foreach (var error in exception.Errors)
        {
            if (job.IsMain)
            {
                result.Errors.Add(error);
            }
            else
            {
                result.Warnings.Add(S["{0}: {1}", job.Query.Name ?? job.Query.Id, error]);
            }
        }

        return job.IsMain;
    }

    // Plans one run of a query of a report. Returns null when the query cannot run: the main query's problems are
    // errors, another query's are warnings that name it.
    private async Task<ReportQueryPlan> PlanQueryAsync(
        ReportNamedQuery query,
        ReportQueryExecutionContext context,
        ReportRunPeriod period,
        ReportRunResult result,
        CancellationToken cancellationToken)
    {
        var isMain = string.IsNullOrEmpty(query.Id);

        context.QueryId = query.Id ?? string.Empty;

        using var activity = ReportTelemetry.Start("report.plan", context);
        var plan = await _planner.PlanAsync(query.Query ?? new ReportQueryDefinition(), context.DataSourceContext, cancellationToken);

        activity?.SetTag("crestapps.report.row_calculations", plan.RowCalculations.Count);
        activity?.SetTag("crestapps.report.calculated_fields", query.Query?.CalculatedFields?.Count ?? 0);
        activity?.SetTag("crestapps.report.errors", plan.Errors.Count);

        if (!plan.IsValid)
        {
            RecordQueryFailure(context.Source);

            foreach (var error in plan.Errors)
            {
                if (isMain)
                {
                    result.Errors.Add(error);
                }
                else
                {
                    result.Warnings.Add(S["{0}: {1}", query.Name ?? query.Id, error]);
                }
            }

            return null;
        }

        // The period overrides whatever was entered for the filter it is set on. A relative date filter ("in the last
        // 30 days") covers the period as a date range: the plan is this run's own, so its copy of the filter changes
        // and the saved report doesn't. The main query picks the filter; the filters of the other queries that share
        // its control cover the same period.
        if (period is not null)
        {
            if (isMain)
            {
                var periodFilter = FindPeriodFilter(plan);

                if (periodFilter is not null)
                {
                    var values = period.ToFilterValues();

                    periodFilter.Definition = AsDateRange(periodFilter.Definition, values);
                    context.FilterValues[periodFilter.Definition.Id] = values;
                    result.PeriodFilterId = periodFilter.Definition.Id;
                }
            }
            else if (result.PeriodFilterId is not null)
            {
                foreach (var filter in plan.Filters.Where(filter => filter.Definition.Exposed && filter.Definition.Id == result.PeriodFilterId))
                {
                    filter.Definition = AsDateRange(filter.Definition, context.FilterValues[result.PeriodFilterId]);
                }
            }
        }

        return plan;
    }

    // Runs one planned run of a query of a report for the visuals that show it, or as one table, adding their sections,
    // its warnings, and its viewer filters (a filter of an earlier query with the same identifier keeps its control).
    private async Task<ReportQueryResult> RunQueryAsync(
        QueryJob job,
        bool showAsTable,
        ReportQueryExecutionContext context,
        ReportRunResult result,
        List<ReportSection> sections,
        IDictionary<string, IList<string>> selections,
        CancellationToken cancellationToken)
    {
        var query = job.Query;
        var plan = job.Plan;
        var visuals = job.Visuals;

        context.QueryId = query.Id ?? string.Empty;

        // The rows behind a grouped result aren't kept, so the totals, subtotals, charts and pivots of the visuals are
        // grouped while the query runs.
        context.Groupings.Clear();

        foreach (var grouping in ReportDesignDocumentBuilder.GetGroupings(visuals, ReportQueryEngine.DescribeColumns(plan)))
        {
            context.Groupings.Add(grouping);
        }

        var queryResult = await _engine.ExecuteAsync(plan, context, cancellationToken);

        foreach (var error in _documentBuilder.Validate(visuals, queryResult.Columns))
        {
            result.Warnings.Add(job.IsMain ? error : S["{0}: {1}", query.Name ?? query.Id, error]);
        }

        foreach (var warning in queryResult.Warnings)
        {
            result.Warnings.Add(warning);
        }

        // A report without visuals shows its main query's result as one table.
        if (visuals.Count > 0 || showAsTable)
        {
            using var activity = ReportTelemetry.Start("report.render", context);

            activity?.SetTag("crestapps.report.visuals", string.Join(',', visuals.Select(visual => visual.Id)));

            foreach (var section in _documentBuilder.Build(null, visuals, queryResult, selections.AsReadOnly()).Sections)
            {
                sections.Add(section);
            }
        }

        foreach (var filter in plan.Filters.Where(filter => filter.Definition.Exposed))
        {
            if (result.Filters.Any(added => added.Definition.Id == filter.Definition.Id))
            {
                continue;
            }

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

        return queryResult;
    }

    // The filters the values picked in the visuals people clicked set on the visuals their clicks filter, by the
    // identifier of the visual filtered. A visual is never filtered by its own click.
    private Dictionary<string, List<ReportFilterDefinition>> GetSelectionFilters(
        IList<ReportNamedQuery> queries,
        IList<ReportVisualDefinition> visuals,
        IDictionary<string, IList<string>> selections,
        ReportRunResult result)
    {
        var filters = new Dictionary<string, List<ReportFilterDefinition>>(StringComparer.Ordinal);

        foreach (var source in visuals)
        {
            if (string.IsNullOrEmpty(source.Action?.ColumnId) ||
                !selections.TryGetValue(source.Id ?? string.Empty, out var keys) ||
                keys is null ||
                keys.Count == 0)
            {
                continue;
            }

            var column = queries.FirstOrDefault(query => SameQuery(query.Id, source.QueryId))?.Query?.Columns?
                .FirstOrDefault(candidate => candidate?.Id == source.Action.ColumnId);

            if (column is null || column.Aggregate != DataAggregate.None)
            {
                continue;
            }

            var name = source.Title ?? source.Id;

            foreach (var target in source.Action.Targets ?? [])
            {
                var visual = visuals.FirstOrDefault(candidate => candidate.Id == target?.VisualId);

                if (visual is null)
                {
                    // A visual removed or replaced since the click was set up: nothing it names is filtered.
                    if (!string.IsNullOrEmpty(target?.VisualId))
                    {
                        result.Warnings.Add(S["Clicking '{0}' cannot filter the visual '{1}': the report no longer has it. Pick the visuals it filters again.", name, target.VisualId]);
                    }

                    continue;
                }

                if (visual == source)
                {
                    continue;
                }

                var query = queries.FirstOrDefault(candidate => SameQuery(candidate.Id, visual.QueryId))?.Query;
                var field = string.IsNullOrWhiteSpace(target.Field) ? column.Field : target.Field.Trim();

                if (query is null || !Reads(query, field))
                {
                    result.Warnings.Add(S["Clicking '{0}' cannot filter '{1}': its query doesn't read the field '{2}'.", name, visual.Title ?? visual.Id, field]);

                    continue;
                }

                var filter = ReportSelectionKeys.ToFilter(ReportSelectionKeys.FieldPrefix + source.Id, field, column.Transform, keys);

                if (filter is null)
                {
                    continue;
                }

                if (!filters.TryGetValue(visual.Id, out var list))
                {
                    filters[visual.Id] = list = [];
                }

                list.Add(filter);
            }
        }

        return filters;
    }

    // Whether a query reads a field: a field of one of its data sets, or a calculated field of its own.
    private static bool Reads(ReportQueryDefinition query, string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return false;
        }

        var dot = field.IndexOf('.', StringComparison.Ordinal);

        if (dot > 0 && (query.DataSets ?? []).Any(dataSet => string.Equals(dataSet?.Alias, field[..dot], StringComparison.Ordinal)))
        {
            return true;
        }

        return (query.CalculatedFields ?? []).Any(calculated => string.Equals(calculated?.Name, field, StringComparison.Ordinal));
    }

    // The text that tells apart the sets of filters picked values set, so the visuals filtered alike run together.
    private static string SelectionKey(IList<ReportFilterDefinition> filters)
    {
        return filters is null
            ? string.Empty
            : string.Join('\u001F', filters.Select(filter => $"{filter.Id}\u001E{filter.Field}\u001E{filter.Operator}\u001E{string.Join('\u001E', filter.Values)}"));
    }

    // A copy of a query that also keeps the rows the picked values filter, so the saved report never changes.
    private static ReportNamedQuery WithFilters(ReportNamedQuery query, IEnumerable<ReportFilterDefinition> filters)
    {
        var copy = JsonSerializer.Deserialize<ReportQueryDefinition>(JsonSerializer.SerializeToUtf8Bytes(query.Query ?? new ReportQueryDefinition()));

        copy.Filters = [.. copy.Filters ?? [], .. filters];

        return new ReportNamedQuery
        {
            Id = query.Id,
            Name = query.Name,
            Query = copy,
        };
    }

    private static bool SameQuery(string queryId, string visualQueryId)
    {
        return string.Equals(queryId ?? string.Empty, visualQueryId ?? string.Empty, StringComparison.Ordinal);
    }

    // One run of a query of a report: the query as saved, the copy that runs (with the filters values picked in other
    // visuals set), the visuals it shows, and its plan.
    private sealed class QueryJob
    {
        public ReportNamedQuery Query { get; set; }

        public ReportNamedQuery Run { get; set; }

        public List<ReportVisualDefinition> Visuals { get; set; }

        public bool IsMain { get; set; }

        public bool GivesRows { get; set; }

        public ReportQueryPlan Plan { get; set; }

        public bool Skipped { get; set; }
    }

    // The queries of one run: those that ran, those whose plan is invalid, those that failed, and the other runs of a
    // failed query that were skipped.
    private sealed class RunQueries
    {
        public int Executed { get; set; }

        public int Invalid { get; set; }

        public int Failed { get; set; }

        public int Skipped { get; set; }
    }

    /// <summary>
    /// Finds the filter a period is set on: the first filter, in the report's order, that its viewers can change and
    /// that picks a date range or a number of days back (a relative date, such as "in the last 30 days"), on a date or
    /// a date and time.
    /// </summary>
    /// <param name="plan">The plan of the report's query.</param>
    /// <returns>The filter, or <see langword="null"/> when the report has no such filter.</returns>
    public static PlannedFilter FindPeriodFilter(ReportQueryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Filters.FirstOrDefault(filter =>
            filter.Definition.Exposed &&
            !string.IsNullOrEmpty(filter.Definition.Id) &&
            filter.DataType is DataFieldType.Date or DataFieldType.DateTime &&
            ResolveControl(filter.Definition, filter.DataType) is ReportFilterControl.DateRange or ReportFilterControl.RelativeDate);
    }

    // A copy of a filter that keeps the rows between the days of a period, whatever the filter compared before.
    private static ReportFilterDefinition AsDateRange(ReportFilterDefinition filter, IList<string> values)
    {
        return new ReportFilterDefinition
        {
            Id = filter.Id,
            Field = filter.Field,
            Stage = filter.Stage,
            Operator = DataFilterOperator.Between,
            Values = values,
            Exposed = filter.Exposed,
            Label = filter.Label,
            Control = ReportFilterControl.DateRange,
        };
    }

    /// <summary>
    /// Picks the control of an exposed filter when its definition asks for an automatic choice.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="dataType">The type of the filtered values.</param>
    /// <returns>The control to render.</returns>
    public static ReportFilterControl ResolveControl(ReportFilterDefinition filter, DataFieldType dataType)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.Control != ReportFilterControl.Auto)
        {
            return filter.Control;
        }

        if (dataType == DataFieldType.Boolean)
        {
            return ReportFilterControl.Boolean;
        }

        if (filter.Operator == DataFilterOperator.Between)
        {
            return DataValues.IsTemporal(dataType) ? ReportFilterControl.DateRange : ReportFilterControl.NumberRange;
        }

        if (ReportQueryEngine.UsesOptions(filter, dataType))
        {
            return filter.Operator is DataFilterOperator.In or DataFilterOperator.NotIn
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
    /// Gets or sets the rows of the result, in the order of <see cref="Columns"/>.
    /// </summary>
    public IReadOnlyList<object[]> Rows { get; set; } = [];

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

    /// <summary>
    /// Gets or sets the identifier of the filter the run's period was set on, or <see langword="null"/> when the run had
    /// no period or the report has no date range or relative date filter its viewers can change.
    /// </summary>
    public string PeriodFilterId { get; set; }
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
    public DataFieldType DataType { get; set; }

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
