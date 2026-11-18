using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Runs an ad hoc query with the caller's own access, as the Report Builder's preview does: it needs the permission that
/// designs reports, and every data set applies the caller's read access. Nothing is saved. The Report Builder's query
/// limits protect the site.
/// </summary>
public sealed class QueryReportDataTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "queryReportData";

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        "query": {{ReportQuerySchema.Query}},
        "maxRows": {
          "type": "integer",
          "minimum": 1,
          "maximum": 1000,
          "description": "The most rows to return, 200 by default and at most 1000."
        }
      },
      "required": ["query"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs an ad hoc query over one or more data sets with the user's own access, with joins, filters, grouping by a field or by year, quarter, month, week or day, aggregates (count, sum, average, min, max, median), sorting and a limit, and returns rows. Use it when no existing report answers the question; nothing is saved.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return NotAllowedMessage;
        }

        var problem = ReportQuerySchema.TryRead(ReadJson(arguments, "query"), out var query);

        if (problem is not null)
        {
            return problem;
        }

        var maxRows = ReportToolResults.GetRowLimit(ReadInt(arguments, "maxRows"));

        // An unsaved report with no visuals: its main query gives the rows, as the designer's view preview runs it.
        var design = new ReportDesign
        {
            DisplayText = "Query",
            Query = query,
            Visuals = [],
            Containers = [],
            Queries = [],
        };
        var result = await services.GetRequiredService<ReportDesignRunner>().RunAsync(
            design,
            user,
            filterValues: null,
            period: null,
            new ReportRunOptions { Source = ReportRunOptions.PreviewSource },
            cancellationToken);

        if (result.Errors.Count > 0 && result.Rows.Count == 0)
        {
            return Json(new
            {
                errors = result.Errors,
                warnings = result.Warnings.Count > 0 ? result.Warnings : null,
            });
        }

        var (columns, rows, truncated) = ReportToolResults.ToRows(result.Columns, result.Rows, maxRows);

        return Json(new
        {
            columns,
            rows,
            rowCount = result.RowCount,
            truncated = truncated ? true : (bool?)null,
            errors = result.Errors.Count > 0 ? result.Errors : null,
            warnings = result.Warnings.Count > 0 ? result.Warnings : null,
            note = "Dates are in the user's time zone. Nothing was saved; saveReportDesign saves a query as a report.",
        });
    }
}
