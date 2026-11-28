using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Creates or changes a report group through the same service as the Report Groups page, which checks the name, the
/// parent and that no sibling has the same name. It needs the permission that manages report groups.
/// </summary>
public sealed class SaveReportGroupTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveReportGroup";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the group to change, from listReportGroups. Leave it out to create a group."
        },
        "name": {
          "type": "string",
          "description": "The group's name. Required to create a group."
        },
        "parentId": {
          "type": "string",
          "description": "The id of the group to nest it in. Give an empty text to move it to the top level; leave it out to keep its place."
        },
        "order": {
          "type": "integer",
          "description": "Its position among the groups beside it; lower comes first."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a report group under a parent, or renames, moves or reorders one by id.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageReportGroups))
        {
            return NotAllowedMessage;
        }

        var groupService = services.GetRequiredService<ReportGroupService>();
        var id = ReadString(arguments, "id");
        ReportGroup existing = null;

        if (id is not null)
        {
            existing = await groupService.FindAsync(id);

            if (existing is null)
            {
                return $"There is no report group with the id '{id}'. Call listReportGroups to find it.";
            }
        }

        var incoming = new ReportGroup
        {
            DisplayText = ReadString(arguments, "name") ?? existing?.DisplayText,
            ParentId = Has(arguments, "parentId") ? ReadString(arguments, "parentId") : existing?.ParentId,
            Order = ReadInt(arguments, "order") ?? existing?.Order ?? 0,
        };

        var result = await groupService.SaveAsync(incoming, existing);

        if (!result.Succeeded)
        {
            return Json(new
            {
                saved = false,
                errors = result.Errors,
            });
        }

        // A report placed in a group created in this scope would lose it: the report service reads the cached groups.
        if (existing is null)
        {
            ReportToolScopeChanges.Get(services).Record(result.Group.ItemId);
        }

        var tree = await groupService.GetTreeAsync();

        return Json(new
        {
            saved = true,
            created = existing is null,
            id = result.Group.ItemId,
            name = result.Group.DisplayText,
            path = tree.GetPathText(result.Group.ItemId),
            parentId = result.Group.ParentId,
            order = result.Group.Order,
        });
    }
}
