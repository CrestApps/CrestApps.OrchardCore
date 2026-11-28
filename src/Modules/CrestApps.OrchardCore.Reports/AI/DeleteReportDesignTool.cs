using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Deletes a designed report with its share links, draft and versions, or a report that was never published, as the
/// Report Builder list's Delete actions do. It needs the permission that changes the report, with the report as the
/// resource.
/// </summary>
public sealed class DeleteReportDesignTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteReportDesign";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the designed report, or of a report that was never published, from listReports or the Report Builder."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a designed report with its drafts, versions and share links, or an unpublished draft that was never published. People it was shared with lose it.";

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

        var changes = ReportToolScopeChanges.Get(services);

        if (changes.WasChanged(id))
        {
            return ReportToolScopeChanges.ChangedMessage(id);
        }

        var history = services.GetRequiredService<ReportDesignHistoryService>();
        var design = await services.GetRequiredService<ReportDesignService>().FindAsync(id);

        if (design is not null)
        {
            if (!await ReportToolCatalog.CanManageAsync(services, user, design))
            {
                return NotAllowedMessage;
            }

            await history.DeleteAsync(design, user);
            changes.Record(id);

            return Json(new
            {
                deleted = true,
                id,
                title = design.DisplayText,
                sharedWith = new
                {
                    users = design.SharedUserNames ?? [],
                    roles = design.SharedRoles ?? [],
                },
                note = "The report, its draft, its versions and its share links were deleted.",
            });
        }

        var draft = await history.FindUnpublishedAsync(id);

        if (draft is null)
        {
            return ReportToolCatalog.NotFoundMessage(id);
        }

        if (!await ReportToolCatalog.CanManageAsync(services, user, draft.Design))
        {
            return NotAllowedMessage;
        }

        var result = await history.DeleteUnpublishedAsync(id, draft.Revision, force: true, user);
        var failure = await ReportDesignPublishing.DescribeFailureAsync(services, result);

        if (failure is not null)
        {
            return failure;
        }

        changes.Record(id);

        return Json(new
        {
            deleted = true,
            id,
            title = draft.Design.DisplayText,
            note = "The unpublished report was deleted.",
        });
    }
}
