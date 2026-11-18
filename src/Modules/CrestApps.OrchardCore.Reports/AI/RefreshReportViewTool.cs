using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Recomputes the stored rows of a scheduled report view now, as the Report Views page's Refresh now does. It needs the
/// permission that changes every view, which a view's owner holds on it.
/// </summary>
public sealed class RefreshReportViewTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "refreshReportView";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the view, from listReportViews."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Recomputes the stored snapshot of a report view now. Only views with a refresh interval keep a snapshot.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");

        if (id is null)
        {
            return "Give the view's id in id. Call listReportViews to find it.";
        }

        if (ReportToolScopeChanges.Get(services).WasChanged(id))
        {
            return ReportToolScopeChanges.ChangedMessage(id);
        }

        var view = await services.GetRequiredService<ReportDesignService>().FindViewAsync(id);

        if (view is null)
        {
            return $"There is no report view with the id '{id}'. Call listReportViews to find it.";
        }

        if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageAllReportDesigns, view))
        {
            return NotAllowedMessage;
        }

        var result = await services.GetRequiredService<ReportViewSnapshotRefresher>().RefreshAsync(view, onlyWhenDue: false, cancellationToken);

        return result.Status switch
        {
            ReportViewRefreshStatus.NotScheduled => "The view runs live, so it keeps no stored rows. Give it a refresh interval with saveReportView first.",
            ReportViewRefreshStatus.Busy => "The view is being refreshed already. Try again in a moment.",
            ReportViewRefreshStatus.NotFound => $"There is no report view with the id '{id}'. Call listReportViews to find it.",
            _ => Json(new
            {
                id = view.ItemId,
                name = view.DisplayText,
                refreshed = result.Status == ReportViewRefreshStatus.Refreshed,
                status = result.Status.ToString(),
                refreshedAt = await ToLocalAsync(services, result.Snapshot?.RefreshedUtc),
                rowCount = result.Snapshot?.RowCount ?? 0,
                error = result.Status == ReportViewRefreshStatus.Failed ? result.Snapshot?.LastError : null,
                errorAt = result.Status == ReportViewRefreshStatus.Failed ? await ToLocalAsync(services, result.Snapshot?.LastErrorUtc) : null,
            }),
        };
    }
}
