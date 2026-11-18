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
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Returns the download link of a report export for a period and filter values. The link is the report page's own
/// export address, which checks the permissions again and builds the file when it is opened; nothing is stored. A
/// built-in report needs its permission and its export permission when it has one; a designed report needs to be
/// viewable by the caller and to allow export, unless the caller may change it.
/// </summary>
public sealed class ExportReportTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "exportReport";

    // The prefix the date range filter's fields have on the report page, which is the filter model's type name.
    internal const string FilterFieldPrefix = nameof(ReportFilter);

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The report's id from listReports."
        },
        "format": {
          "type": "string",
          "description": "The file format: csv (always available), xlsx or pdf when their features are on. getReport lists the formats. csv when left out."
        },
        {{ReportRunArguments.SchemaProperties}}
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Exports a report for a period and filter values as CSV, Excel or PDF and returns a download link with the file name. The file is built when the link is opened, with the same permission checks as the report page. Excel and PDF need their features to be enabled. A built-in report can only be given a period.";

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

        var report = ReportToolCatalog.FindBuiltIn(services, id);
        ReportDesign design = null;

        if (report is not null)
        {
            if (!await IsAuthorizedAsync(services, user, report.Permission) || !await ReportToolCatalog.CanExportAsync(services, user, report))
            {
                return NotAllowedMessage;
            }
        }
        else
        {
            if (ReportToolScopeChanges.Get(services).WasChanged(id))
            {
                return ReportToolScopeChanges.ChangedMessage(id);
            }

            design = await ReportToolCatalog.FindDesignedAsync(services, id);

            if (design is null)
            {
                return ReportToolCatalog.NotFoundMessage(id);
            }

            if (!await ReportToolCatalog.CanViewAsync(services, user, design) || !await ReportToolCatalog.CanExportAsync(services, user, design))
            {
                return NotAllowedMessage;
            }
        }

        var formatName = ReadString(arguments, "format")?.ToLowerInvariant() ?? ReportsConstants.CsvExportFormat;
        var exportManager = services.GetRequiredService<IReportExportManager>();
        var format = exportManager.GetDescriptor(formatName);

        if (format is null)
        {
            return $"The format '{formatName}' is not available. Available formats: {string.Join(", ", exportManager.GetFormats().Select(available => available.Name))}.";
        }

        return report is not null
            ? await ExportBuiltInAsync(services, report, format, run)
            : await ExportDesignedAsync(services, design, format, run);
    }

    private static async Task<object> ExportBuiltInAsync(IServiceProvider services, ReportDescriptor report, ReportExportFormatDescriptor format, ReportRunArguments run)
    {
        var problem = run.CheckFor(report);

        if (problem is not null)
        {
            return problem;
        }

        var resolver = services.GetRequiredService<ReportDateRangeResolver>();
        var range = await resolver.ResolveDaysAsync(report.Name, run.From, run.To, run.AllTime);
        var query = new List<KeyValuePair<string, string>>();

        // The export reads the period from the same fields as the report page's date range picker. Without them the
        // report runs on its default period, as when it is opened.
        if (run.AllTime)
        {
            query.Add(new($"{FilterFieldPrefix}.Range", ReportDefaultDateRange.AllTimeKey));
        }
        else if (run.From.HasValue || run.To.HasValue)
        {
            if (run.From is { } from)
            {
                query.Add(new($"{FilterFieldPrefix}.From", from.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));
            }

            if (run.To is { } to)
            {
                query.Add(new($"{FilterFieldPrefix}.To", to.ToDateTime(new TimeOnly(23, 59, 59)).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));
            }

            query.Add(new($"{FilterFieldPrefix}.Range", ReportDefaultDateRange.CustomKey));
        }

        var url = GetRoutePath(services, "ReportsExport", new { id = report.Name, format = format.Name }, query);

        if (url is null)
        {
            return "The download link could not be built here. Open the report on the Reports page and export it from there.";
        }

        return Json(new
        {
            id = report.Name,
            kind = ReportToolCatalog.BuiltIn,
            title = services.GetRequiredService<IStringLocalizerFactory>().Localize(report.DisplayName).Value,
            format = format.Name,
            fileName = Controllers.ReportsController.GetFileName(report.Name, range) + format.Extension,
            downloadUrl = url,
            period = new
            {
                from = await ToLocalAsync(services, range.FromUtc),
                to = await ToLocalAsync(services, range.ToUtc),
                allTime = range.FromUtc is null && range.ToUtc is null ? true : (bool?)null,
            },
            note = "The file is built when the link is opened, for whoever opens it with their own permissions.",
        });
    }

    private static async Task<object> ExportDesignedAsync(IServiceProvider services, ReportDesign design, ReportExportFormatDescriptor format, ReportRunArguments run)
    {
        var problem = run.CheckFor(design);

        if (problem is not null)
        {
            return problem;
        }

        // The export address reads the filter values as the report page's filter form sends them. The period goes on the
        // first date range filter its viewers can change; a report with none gets no period.
        var values = new Dictionary<string, IList<string>>(run.Filters ?? [], StringComparer.Ordinal);
        string periodFilter = null;

        if (run.HasPeriod)
        {
            var dateFilter = ReportToolCatalog.GetExposedFilters(design)
                .FirstOrDefault(filter => filter.Control is ReportFilterControl.DateRange or ReportFilterControl.RelativeDate);

            if (dateFilter is null)
            {
                return "This report has no date filter its viewers can change, so it cannot be exported for a period. Leave from, to and allTime out, or give its filters.";
            }

            if (dateFilter.Control != ReportFilterControl.DateRange)
            {
                return $"This report's period filter '{dateFilter.Id}' counts days back from today and cannot take a period in an export link. Use runReport for a period, or export it as it is.";
            }

            periodFilter = dateFilter.Id;
            values[dateFilter.Id] = run.GetPeriod().ToFilterValues();
        }

        var query = new List<KeyValuePair<string, string>>
        {
            new(ReportFilterValueBinder.AppliedField, "1"),
        };
        var exposed = ReportToolCatalog.GetExposedFilters(design);

        foreach (var filter in exposed)
        {
            if (!values.TryGetValue(filter.Id, out var filterValues))
            {
                // A submitted form sends every filter; the report page shows a filter left out with its default values.
                filterValues = filter.Values ?? [];
            }

            if (ReportToolCatalog.IsRange(filter))
            {
                query.Add(new(ReportFilterValueBinder.FieldName(filter.Id, "from"), filterValues.ElementAtOrDefault(0) ?? string.Empty));
                query.Add(new(ReportFilterValueBinder.FieldName(filter.Id, "to"), filterValues.ElementAtOrDefault(1) ?? string.Empty));

                continue;
            }

            foreach (var value in filterValues)
            {
                query.Add(new(ReportFilterValueBinder.FieldName(filter.Id), value));
            }
        }

        var url = GetRoutePath(services, "ReportDesignsExport", new { id = design.ItemId, format = format.Name }, query);

        if (url is null)
        {
            return "The download link could not be built here. Open the report in the Report Builder and export it from there.";
        }

        return Json(new
        {
            id = design.ItemId,
            kind = ReportToolCatalog.Designed,
            title = design.DisplayText,
            format = format.Name,
            fileName = DesignedReportPresenter.GetFileName(design.DisplayText, services.GetRequiredService<IClock>().UtcNow) + format.Extension,
            downloadUrl = url,
            periodFilter,
            note = "The file is built when the link is opened, for whoever opens it with their own permissions.",
        });
    }
}
