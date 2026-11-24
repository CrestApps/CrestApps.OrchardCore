using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Runs a report the caller may open and returns its numbers and rows, as the report page shows them. A built-in report
/// needs its own permission and runs for a period only; a designed report needs to be viewable by the caller and runs
/// with its owner's access, as it does on its page and for the people it is shared with.
/// </summary>
public sealed class RunReportTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "runReport";

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The report's id from listReports."
        },
        {{ReportRunArguments.SchemaProperties}},
        "maxRows": {
          "type": "integer",
          "minimum": 1,
          "maximum": 1000,
          "description": "The most rows to return from each table, 200 by default and at most 1000."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs a report for a period and filter values and returns its headline numbers and its tables as columns and rows, capped at a row limit with the total row count, and echoes the filters it applied and any it could not apply. A built-in report can only be given a period; its other filters keep their defaults. Use it to answer questions from a report; use exportReport when the user wants a file.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");

        if (id is null)
        {
            return "Give the report's id in id. Call listReports to find it.";
        }

        var problem = ReadRunArguments(arguments, out var run);

        if (problem is not null)
        {
            return problem;
        }

        var maxRows = ReportToolResults.GetRowLimit(ReadInt(arguments, "maxRows"));
        var report = ReportToolCatalog.FindBuiltIn(services, id);

        if (report is not null)
        {
            if (!await IsAuthorizedAsync(services, user, report.Permission))
            {
                return NotAllowedMessage;
            }

            return await RunBuiltInAsync(services, report, run, maxRows, cancellationToken);
        }

        if (ReportToolScopeChanges.Get(services).WasChanged(id))
        {
            return ReportToolScopeChanges.ChangedMessage(id);
        }

        var design = await ReportToolCatalog.FindDesignedAsync(services, id);

        if (design is null)
        {
            return ReportToolCatalog.NotFoundMessage(id);
        }

        if (!await ReportToolCatalog.CanViewAsync(services, user, design))
        {
            return NotAllowedMessage;
        }

        return await RunDesignedAsync(services, user, design, run, maxRows, cancellationToken);
    }

    private static async Task<object> RunBuiltInAsync(IServiceProvider services, ReportDescriptor report, ReportRunArguments run, int maxRows, CancellationToken cancellationToken)
    {
        var problem = run.CheckFor(report);

        if (problem is not null)
        {
            return problem;
        }

        var range = await services.GetRequiredService<ReportDateRangeResolver>().ResolveDaysAsync(report.Name, run.From, run.To, run.AllTime);
        var filter = new ReportFilter
        {
            ReportName = report.Name,
        };

        filter.SetDateRange(range);

        var reportManager = services.GetRequiredService<IReportManager>();
        var document = await ReportTelemetry.RunCodedReportAsync(reportManager.GetReport(report.Name), report, new ReportContext(filter), cancellationToken);

        await services.GetRequiredService<ReportDisplayValueResolver>().ResolveAsync(document);

        var (sections, truncated) = ReportToolResults.FlattenSections(document, maxRows);

        return Json(new
        {
            id = report.Name,
            kind = ReportToolCatalog.BuiltIn,
            title = services.GetRequiredService<IStringLocalizerFactory>().Localize(report.DisplayName).Value,
            period = new
            {
                from = await ToLocalAsync(services, range.FromUtc),
                to = await ToLocalAsync(services, range.ToUtc),
                allTime = range.FromUtc is null && range.ToUtc is null ? true : (bool?)null,
            },
            sections,
            truncated = truncated ? true : (bool?)null,
            appliedFilters = new
            {
                period = new
                {
                    requestedFrom = run.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    requestedTo = run.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    requestedAllTime = run.AllTime ? true : (bool?)null,
                    usedDefaultPeriod = run.HasPeriod ? (bool?)null : true,
                },
            },
            filtersNote = "A built-in report applies only the period here; every other filter keeps its default. Do not describe the result as filtered by anything else.",
            note = truncated ? "Some rows were left out to keep the answer short. Use exportReport for every row." : null,
        });
    }

    private static async Task<object> RunDesignedAsync(IServiceProvider services, ClaimsPrincipal user, ReportDesign design, ReportRunArguments run, int maxRows, CancellationToken cancellationToken)
    {
        var problem = run.CheckFor(design);

        if (problem is not null)
        {
            return problem;
        }

        // The report reads its data with its owner's access, as on its page; the caller was checked above.
        var result = await services.GetRequiredService<ReportDesignRunner>().RunAsync(
            design,
            run.Filters,
            run.GetPeriod(),
            new ReportRunOptions { Source = ReportRunSources.Other },
            cancellationToken);

        if (result.Document is not null)
        {
            await services.GetRequiredService<ReportDisplayValueResolver>().ResolveAsync(result.Document);
        }

        // Only what the report's visuals show, as on its page and in its export: the report reads with its owner's
        // access, so a column of the query that no visual shows is not for its viewers.
        var (sections, truncated) = ReportToolResults.FlattenSections(result.Document, maxRows);

        // Echo what was applied, so the answer never claims a filter the run could not apply.
        var applied = new List<object>();
        var notApplied = new List<string>();

        foreach (var (filterId, values) in run.Filters ?? new Dictionary<string, IList<string>>())
        {
            var filter = result.Filters.FirstOrDefault(exposed => string.Equals(exposed.Definition?.Id, filterId, StringComparison.Ordinal));

            if (filter is not null)
            {
                applied.Add(new
                {
                    id = filterId,
                    label = filter.Definition.Label ?? filter.Definition.Field,
                    requested = values,
                    applied = filter.Values,
                });
            }
            else
            {
                notApplied.Add($"The filter '{filterId}' was not applied: the report did not run with it.");
            }
        }

        var periodApplied = run.HasPeriod && result.PeriodFilterId is not null;

        if (run.HasPeriod && !periodApplied)
        {
            notApplied.Add("The period was not applied: the report has no date filter its viewers can change.");
        }

        return Json(new
        {
            id = design.ItemId,
            kind = ReportToolCatalog.Designed,
            title = design.DisplayText,
            sections,
            rowCount = result.RowCount,
            truncated = truncated ? true : (bool?)null,
            filters = result.Filters.Select(filter => new
            {
                id = filter.Definition?.Id,
                label = filter.Definition?.Label ?? filter.Definition?.Field,
                values = filter.Values,
            }).ToArray(),
            periodFilter = result.PeriodFilterId,
            appliedFilters = new
            {
                period = periodApplied
                    ? new
                    {
                        filterId = result.PeriodFilterId,
                        from = run.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        to = run.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        allTime = run.AllTime ? true : (bool?)null,
                    }
                    : null,
                filters = applied,
            },
            notApplied = notApplied.Count > 0 ? notApplied : null,
            periodNote = run.HasPeriod && result.PeriodFilterId is null && result.Errors.Count == 0
                ? "This report has no date filter its viewers can change, so the period was not applied."
                : null,
            errors = result.Errors.Count > 0 ? result.Errors : null,
            warnings = result.Warnings.Count > 0 ? result.Warnings : null,
            note = truncated ? "Some rows were left out to keep the answer short. Use exportReport for every row." : null,
        });
    }
}
