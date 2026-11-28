using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Deletes an empty report group through the same service as the Report Groups page, which keeps a group that still
/// holds groups or reports and says what it holds. It needs the permission that manages report groups.
/// </summary>
public sealed class DeleteReportGroupTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteReportGroup";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the group to delete, from listReportGroups."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a report group. Check what it contains first; the service refuses or reports what blocks the deletion.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageReportGroups))
        {
            return NotAllowedMessage;
        }

        var id = ReadString(arguments, "id");

        if (id is null)
        {
            return "Give the group's id in id. Call listReportGroups to find it.";
        }

        var groupService = services.GetRequiredService<ReportGroupService>();
        var group = await groupService.FindAsync(id);

        if (group is null)
        {
            return $"There is no report group with the id '{id}'. Call listReportGroups to find it.";
        }

        var result = await groupService.DeleteAsync(group);

        if (!result.Succeeded)
        {
            return Json(new
            {
                deleted = false,
                errors = result.Errors,
            });
        }

        return Json(new
        {
            deleted = true,
            id = group.ItemId,
            name = group.DisplayText,
        });
    }
}
