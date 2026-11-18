using System.Text.Json;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Geo.AI;

/// <summary>
/// Deletes a service territory as the Territories page does: its child territories move up to its parent first.
/// </summary>
internal sealed class DeleteServiceTerritoryTool : GeoAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteServiceTerritory";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the territory, from listServiceTerritories."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a service territory. Its child territories move up to its parent.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => GeoPermissions.ManageTerritories;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");
        var manager = services.GetRequiredService<ServiceTerritoryManager>();
        var territory = id is null ? null : await manager.FindByIdAsync(id, cancellationToken);

        if (territory is null)
        {
            return $"Nothing was deleted: no service territory has the id '{id}'. Use listServiceTerritories to find it.";
        }

        var children = await TerritoryCommands.DeleteAsync(manager, territory, cancellationToken);

        return Json(new
        {
            id = territory.ItemId,
            name = territory.Name,
            deleted = true,
            movedChildren = children.Select(child => new { id = child.ItemId, name = child.Name }).ToList(),
            newParentId = territory.ParentId,
            message = children.Count == 0
                ? $"The territory '{territory.Name}' was deleted."
                : territory.ParentId is null
                    ? $"The territory '{territory.Name}' was deleted; its {children.Count} child territories are now top-level territories."
                    : $"The territory '{territory.Name}' was deleted; its {children.Count} child territories moved up to its parent.",
        });
    }
}
