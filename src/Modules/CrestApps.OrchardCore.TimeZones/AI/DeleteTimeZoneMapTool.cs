using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeZones.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TimeZones.AI;

/// <summary>
/// Deletes a time zone map through the time zone map manager, as the Time Zones list's Delete action does. Records that
/// store the time zone id are not touched; pickers stop offering the name.
/// </summary>
internal sealed class DeleteTimeZoneMapTool : TimeZonesAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteTimeZoneMap";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the map, from listTimeZoneMaps. Give the id or the name."
        },
        "name": {
          "type": "string",
          "description": "The friendly name of the map, such as 'Hawaii'. Give the id or the name."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a friendly time zone name, so pickers no longer offer it. Records that already store the time zone id keep working.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");
        var name = ReadString(arguments, "name");

        if (id is null && name is null)
        {
            return "Nothing was deleted: give the map's id or its name.";
        }

        var changes = services.GetRequiredService<TimeZoneMapScopeChanges>();
        var all = await changes.GetAllAsync(services.GetRequiredService<INamedCatalog<TimeZoneMap>>(), cancellationToken);
        var map = id is not null
            ? all.FirstOrDefault(item => string.Equals(item.ItemId, id, StringComparison.Ordinal))
            : all.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

        if (map is null)
        {
            return id is not null
                ? $"Nothing was deleted: no time zone map has the id '{id}'. Use listTimeZoneMaps to find it."
                : $"Nothing was deleted: no time zone map is named '{name}'. Use listTimeZoneMaps to find it.";
        }

        if (!await services.GetRequiredService<INamedCatalogManager<TimeZoneMap>>().DeleteAsync(map, cancellationToken))
        {
            return $"Nothing was deleted: the time zone map '{map.Name}' could not be deleted. It may have been deleted already.";
        }

        changes.Deleted(map.ItemId);

        return Json(new
        {
            id = map.ItemId,
            name = map.Name,
            timeZoneId = map.TimeZoneId,
            deleted = true,
            message = $"Time zone pickers no longer offer '{map.Name}'. Records that store {map.TimeZoneId} keep it.",
        });
    }
}
