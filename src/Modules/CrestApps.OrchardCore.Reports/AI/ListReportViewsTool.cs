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
/// Lists the saved report views, as the Report Views page does: it needs the permission that designs reports. A view's
/// query is returned only to someone who may change the view, as its designer page does.
/// </summary>
public sealed class ListReportViewsTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listReportViews";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "Return this one view with its query and the reports and views that read it."
        },
        "query": {
          "type": "string",
          "description": "Only list the views whose name or description contains this text."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of views per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the saved report views (reusable queries that reports read as data sets) with owner, refresh interval and snapshot state, or one view by id with its query and the reports that use it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return NotAllowedMessage;
        }

        var designService = services.GetRequiredService<ReportDesignService>();
        var snapshots = services.GetRequiredService<ReportViewSnapshotStore>();
        var id = ReadString(arguments, "id");

        if (id is not null)
        {
            if (ReportToolScopeChanges.Get(services).WasChanged(id))
            {
                return ReportToolScopeChanges.ChangedMessage(id);
            }

            var view = await designService.FindViewAsync(id);

            if (view is null)
            {
                return $"There is no report view with the id '{id}'. Call listReportViews to find it.";
            }

            var canEdit = await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageAllReportDesigns, view);

            return Json(new
            {
                view = await DescribeAsync(services, view, canEdit, await snapshots.GetStatusAsync(view.ItemId)),
                query = canEdit ? JsonSerializer.SerializeToElement(view.Query, ReportDesignerJson.Options) : (JsonElement?)null,
                queryNote = canEdit ? null : "The query is shown only to people who may change the view.",
                usedBy = await designService.FindViewUsagesAsync(view.ItemId),
            });
        }

        var search = ReadString(arguments, "query");
        var (page, pageSize) = ReadPaging(arguments);
        var statuses = await snapshots.ListStatusesAsync();
        var views = (await designService.GetAllViewsAsync())
            .Where(view => search is null ||
                view.DisplayText?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true ||
                view.Description?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true)
            .ToList();
        var items = new List<object>();

        foreach (var view in views.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var canEdit = await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageAllReportDesigns, view);

            items.Add(await DescribeAsync(services, view, canEdit, statuses.GetValueOrDefault(view.ItemId)));
        }

        return Json(new
        {
            items,
            total = views.Count,
            page,
            pageSize,
            truncated = page * pageSize < views.Count ? true : (bool?)null,
        });
    }

    private static async Task<object> DescribeAsync(IServiceProvider services, ReportView view, bool canEdit, ReportViewSnapshotStatus snapshot)
    {
        var scheduled = view.RefreshIntervalMinutes > ReportViewRefreshIntervals.Live;

        return new
        {
            id = view.ItemId,
            name = view.DisplayText,
            description = view.Description,
            owner = view.Author,
            created = await ToLocalAsync(services, view.CreatedUtc),
            modified = await ToLocalAsync(services, view.ModifiedUtc),
            refreshIntervalMinutes = scheduled ? view.RefreshIntervalMinutes : (int?)null,
            live = !scheduled,
            snapshot = scheduled && snapshot is not null
                ? new
                {
                    refreshed = await ToLocalAsync(services, snapshot.RefreshedUtc),
                    lastAttempt = await ToLocalAsync(services, snapshot.AttemptedUtc),
                    rowCount = snapshot.RowCount,
                    lastError = snapshot.LastError,
                    lastErrorAt = await ToLocalAsync(services, snapshot.LastErrorUtc),
                }
                : null,
            canEdit,
        };
    }
}
