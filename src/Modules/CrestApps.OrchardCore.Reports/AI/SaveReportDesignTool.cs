using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Creates and publishes a designed report, or publishes a change to one, through the same service as the designer's
/// Publish, which keeps a version and checks the query against the data sources with the caller's access. Creating a
/// report needs the permission that designs reports; changing one needs the permission that changes it, with the report
/// as the resource. It never changes who the report is shared with: a change starts from the stored report, sharing
/// included, and only <c>shareReportDesign</c> changes sharing.
/// </summary>
public sealed class SaveReportDesignTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveReportDesign";

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the designed report to change, from listReports. Leave it out to create a report."
        },
        "title": {
          "type": "string",
          "description": "The report's title. Required to create a report."
        },
        "description": {
          "type": "string",
          "description": "The text shown above the report."
        },
        "group": {
          "type": "string",
          "description": "The path of an existing report group, such as 'Sales' or 'Sales / Regional', from listReportGroups. Give an empty text for no group; leave it out to keep the report's group."
        },
        "query": {{ReportQuerySchema.Query}},
        "visuals": {
          "type": "array",
          "description": "The visuals in display order; they replace the report's visuals. A new report without visuals gets one table of every column.",
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "string", "description": "A short identifier of the visual." },
              "type": { "type": "string", "enum": ["Table", "Chart", "Metrics", "Pivot"], "description": "The kind of visual." },
              "title": { "type": "string", "description": "The visual's title." },
              "width": { "type": "integer", "minimum": 1, "maximum": 12, "description": "Its width in twelfths of the page, 12 by default." },
              "chartType": { "type": "string", "enum": ["Line", "Bar", "Doughnut", "Pie", "Area", "HorizontalBar"], "description": "For a chart: its kind." },
              "categoryColumnId": { "type": "string", "description": "For a chart or a pivot: the column id along the axis or down the rows." },
              "seriesColumnId": { "type": "string", "description": "For a chart or a pivot: the column id that splits the values into series or columns." },
              "valueColumnIds": { "type": "array", "items": { "type": "string" }, "description": "For a chart, metrics or a pivot: the column ids of the measures shown." },
              "columnIds": { "type": "array", "items": { "type": "string" }, "description": "For a table: the column ids shown, all by default." },
              "showTotals": { "type": "boolean", "description": "For a table or a pivot: whether to show totals." },
              "stacked": { "type": "boolean", "description": "For a chart: whether series are stacked." },
              "showLegend": { "type": "boolean", "description": "For a chart: whether to show the legend." }
            },
            "required": ["type"],
            "additionalProperties": false
          }
        },
        "showInAdminMenu": {
          "type": "boolean",
          "description": "Whether the report has its own item under Reports in the admin menu."
        },
        "allowExport": {
          "type": "boolean",
          "description": "Whether the people the report is shared with may export it. True for a new report."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates and publishes a designed report, or publishes a change to an existing one by id, from a definition with its query, visuals, group, export and admin menu options. Returns the planner's errors and warnings; it does not change who the report is shared with. Test the query with queryReportData first.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var designService = services.GetRequiredService<ReportDesignService>();
        var changes = ReportToolScopeChanges.Get(services);
        var id = ReadString(arguments, "id");
        ReportDesign existing = null;
        long revision = 0;

        if (id is not null)
        {
            if (changes.WasChanged(id))
            {
                return ReportToolScopeChanges.ChangedMessage(id);
            }

            existing = await designService.FindAsync(id);

            if (existing is null)
            {
                var draft = await services.GetRequiredService<ReportDesignHistoryService>().FindUnpublishedAsync(id);

                return draft is not null && await ReportToolCatalog.CanManageAsync(services, user, draft.Design)
                    ? "This report was never published. Publish it in the Report Builder first."
                    : ReportToolCatalog.NotFoundMessage(id);
            }

            if (!await ReportToolCatalog.CanManageAsync(services, user, existing))
            {
                return NotAllowedMessage;
            }

            string problem;
            (revision, problem) = await ReportDesignPublishing.GetRevisionAsync(services, existing);

            if (problem is not null)
            {
                return problem;
            }
        }
        else if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return NotAllowedMessage;
        }

        // A change starts from the stored report, so what is not given, sharing included, stays as it is.
        var incoming = existing?.Clone() ?? new ReportDesign
        {
            AllowExport = true,
        };

        if (ReadString(arguments, "title") is { } title)
        {
            incoming.DisplayText = title;
        }

        if (Has(arguments, "description"))
        {
            incoming.Description = ReadString(arguments, "description");
        }

        if (Has(arguments, "group"))
        {
            var (groupId, groupProblem) = await ResolveGroupAsync(services, ReadString(arguments, "group"));

            if (groupProblem is not null)
            {
                return groupProblem;
            }

            incoming.GroupId = groupId;
        }

        if (Has(arguments, "query") || existing is null)
        {
            var queryProblem = ReportQuerySchema.TryRead(ReadJson(arguments, "query"), out var query);

            if (queryProblem is not null)
            {
                return queryProblem;
            }

            incoming.Query = query;
        }

        if (ReadJson(arguments, "visuals") is { ValueKind: JsonValueKind.Array } visuals)
        {
            try
            {
                incoming.Visuals = visuals.Deserialize<List<ReportVisualDefinition>>(ReportDesignerJson.Options) ?? [];
            }
            catch (JsonException exception)
            {
                return $"The visuals could not be read: {exception.Message}";
            }

            for (var index = 0; index < incoming.Visuals.Count; index++)
            {
                if (incoming.Visuals[index] is { } visual && string.IsNullOrWhiteSpace(visual.Id))
                {
                    visual.Id = "v" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
        else if (existing is null)
        {
            // As the designer starts a new report: one table with totals.
            incoming.Visuals =
            [
                new ReportVisualDefinition
                {
                    Id = "table",
                    Type = ReportVisualType.Table,
                    ShowTotals = true,
                },
            ];
        }

        if (ReadBool(arguments, "showInAdminMenu") is { } showInAdminMenu)
        {
            incoming.ShowInAdminMenu = showInAdminMenu;
        }

        if (ReadBool(arguments, "allowExport") is { } allowExport)
        {
            incoming.AllowExport = allowExport;
        }

        if (string.IsNullOrWhiteSpace(incoming.DisplayText))
        {
            return "Give the report's title in title.";
        }

        var canSharePublicly = await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ShareReportsPublicly);
        var published = await services.GetRequiredService<ReportDesignHistoryService>().PublishAsync(incoming, existing, revision, force: false, user, canSharePublicly);
        var failure = await ReportDesignPublishing.DescribeFailureAsync(services, published);

        if (failure is not null)
        {
            return failure;
        }

        var result = published.Save;

        if (result is null || !result.Saved)
        {
            return Json(new
            {
                saved = false,
                errors = result?.Errors ?? [],
            });
        }

        changes.Record(result.Id);

        return Json(new
        {
            saved = true,
            created = existing is null,
            id = result.Id,
            title = result.Design?.DisplayText ?? incoming.DisplayText,
            versionNumber = published.VersionNumber,
            runUrl = GetRoutePath(services, "ReportDesignsRun", new { id = result.Id }),
            warnings = result.Warnings.Count > 0 ? result.Warnings : null,
            note = result.Warnings.Count > 0
                ? "The report was published with problems; it shows them when it runs until they are fixed."
                : null,
            sharing = existing is null
                ? "The report is not shared with anybody yet."
                : "Who the report is shared with did not change.",
        });
    }

    // Finds an existing group by its path. The tool never creates a group here: the report service reads the cached
    // groups, so a group created in the same message would be dropped from the report.
    private static async Task<(string GroupId, string Problem)> ResolveGroupAsync(IServiceProvider services, string path)
    {
        var names = ReportGroupService.SplitPath(path);

        if (names.Count == 0)
        {
            return (null, null);
        }

        var tree = await services.GetRequiredService<ReportGroupService>().GetTreeAsync();
        string parentId = null;

        foreach (var name in names)
        {
            var match = tree.GetChildren(parentId)
                .FirstOrDefault(group => string.Equals(group.DisplayText, name, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                return (null, $"There is no report group '{path}'. Call listReportGroups to find it, or create it with saveReportGroup and save the report in your next reply.");
            }

            parentId = match.ItemId;
        }

        if (ReportToolScopeChanges.Get(services).WasChanged(parentId))
        {
            return (null, $"The report group '{path}' was created earlier in this message. Save the report in your next reply, once the group is saved.");
        }

        return (parentId, null);
    }
}
