using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Creates or changes a report view through the same service as the view designer, which checks the query against the
/// data sources with the caller's access and drops a scheduled view's stored rows when its query changes. Creating a
/// view needs the permission that designs reports; changing one needs the permission that changes every view, which a
/// view's owner holds on it.
/// </summary>
public sealed class SaveReportViewTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveReportView";

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the view to change, from listReportViews. Leave it out to create a view."
        },
        "name": {
          "type": "string",
          "description": "The view's name, which reports see as a data set. Required to create a view."
        },
        "description": {
          "type": "string",
          "description": "What the view returns."
        },
        "query": {{ReportQuerySchema.Query}},
        "refreshIntervalMinutes": {
          "type": "integer",
          "enum": [0, 15, 60, 360, 1440],
          "description": "0 runs the view live each time it is read; otherwise its rows are stored and refreshed every so many minutes."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a report view from a query, or updates one by id, with an optional refresh interval for its stored snapshot. Returns the planner's errors and warnings.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var designService = services.GetRequiredService<ReportDesignService>();
        var changes = ReportToolScopeChanges.Get(services);
        var id = ReadString(arguments, "id");
        ReportView existing = null;

        if (id is not null)
        {
            if (changes.WasChanged(id))
            {
                return ReportToolScopeChanges.ChangedMessage(id);
            }

            existing = await designService.FindViewAsync(id);

            if (existing is null)
            {
                return $"There is no report view with the id '{id}'. Call listReportViews to find it.";
            }

            if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageAllReportDesigns, existing))
            {
                return NotAllowedMessage;
            }
        }
        else if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return NotAllowedMessage;
        }

        ReportQueryDefinition query = existing?.Query;

        if (Has(arguments, "query") || existing is null)
        {
            var problem = ReportQuerySchema.TryRead(ReadJson(arguments, "query"), out query);

            if (problem is not null)
            {
                return problem;
            }
        }

        var interval = ReadInt(arguments, "refreshIntervalMinutes");

        if (interval.HasValue && !ReportViewRefreshIntervals.Options.Contains(interval.Value))
        {
            return $"Give refreshIntervalMinutes as one of {string.Join(", ", ReportViewRefreshIntervals.Options)}.";
        }

        var incoming = new ReportView
        {
            DisplayText = ReadString(arguments, "name") ?? existing?.DisplayText,
            Description = Has(arguments, "description") ? ReadString(arguments, "description") : existing?.Description,
            Query = query,
            RefreshIntervalMinutes = interval ?? existing?.RefreshIntervalMinutes ?? ReportViewRefreshIntervals.Live,
        };

        var result = await designService.SaveViewAsync(incoming, existing, user);

        if (!result.Saved)
        {
            return Json(new
            {
                saved = false,
                errors = result.Errors,
            });
        }

        changes.Record(result.Id);

        return Json(new
        {
            saved = true,
            created = existing is null,
            id = result.Id,
            name = incoming.DisplayText,
            refreshIntervalMinutes = incoming.RefreshIntervalMinutes,
            warnings = result.Warnings.Count > 0 ? result.Warnings : null,
            note = result.Warnings.Count > 0
                ? "The view was saved with problems; it shows them when it runs until they are fixed."
                : null,
        });
    }
}
