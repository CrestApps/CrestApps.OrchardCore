using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Deletes a report view and its stored rows, as the Report Views page does: it needs the permission that changes every
/// view, which a view's owner holds on it, and keeps a view that a report or another view still reads.
/// </summary>
public sealed class DeleteReportViewTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteReportView";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the view to delete, from listReportViews."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a report view that no report or other view reads; when some still read it, nothing is deleted and they are listed. Check its usages with listReportViews first.";

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

        var changes = ReportToolScopeChanges.Get(services);

        if (changes.WasChanged(id))
        {
            return ReportToolScopeChanges.ChangedMessage(id);
        }

        var designService = services.GetRequiredService<ReportDesignService>();
        var view = await designService.FindViewAsync(id);

        if (view is null)
        {
            return $"There is no report view with the id '{id}'. Call listReportViews to find it.";
        }

        if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageAllReportDesigns, view))
        {
            return NotAllowedMessage;
        }

        var usages = await designService.FindViewUsagesAsync(view.ItemId);

        if (usages.Count > 0)
        {
            return Json(new
            {
                deleted = false,
                error = $"The view cannot be deleted because these reports or views read it: {string.Join(", ", usages)}.",
                usedBy = usages,
            });
        }

        await designService.DeleteViewAsync(view);
        changes.Record(view.ItemId);

        return Json(new
        {
            deleted = true,
            id = view.ItemId,
            name = view.DisplayText,
        });
    }
}
