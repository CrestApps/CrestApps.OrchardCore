using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeZones.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TimeZones.AI;

/// <summary>
/// Creates a time zone map, or changes the time zone of the map with the given name, as the Time Zones editor does. The
/// editor cannot rename a map, so a map is found by its name, as the <c>TimeZoneMaps</c> recipe step finds it. The map is
/// checked by the time zone map handler (a valid IANA time zone, one map per time zone) and the name must be unique.
/// </summary>
internal sealed class SaveTimeZoneMapTool : TimeZonesAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveTimeZoneMap";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The friendly name pickers show, such as 'Mountain Time (Denver)'. A map with this name is updated; otherwise a new map is created."
        },
        "timeZoneId": {
          "type": "string",
          "description": "The IANA time zone id the name stands for, such as 'America/Denver'. Use listTimeZones for valid ids."
        }
      },
      "required": ["name", "timeZoneId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a friendly time zone name for a time zone id. The name must be unique; use listTimeZones to find a valid time zone id.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var name = ReadString(arguments, "name");
        var timeZoneId = ReadString(arguments, "timeZoneId");

        if (name is null || timeZoneId is null)
        {
            return "Nothing was saved: give both the friendly name in name and the time zone id in timeZoneId.";
        }

        var manager = services.GetRequiredService<INamedCatalogManager<TimeZoneMap>>();
        var changes = services.GetRequiredService<TimeZoneMapScopeChanges>();
        var all = await changes.GetAllAsync(services.GetRequiredService<INamedCatalog<TimeZoneMap>>(), cancellationToken);
        var existing = all.FirstOrDefault(map => string.Equals(map.Name, name, StringComparison.OrdinalIgnoreCase));
        var isNew = existing is null;

        var map = isNew ? await manager.NewAsync(name, cancellationToken: cancellationToken) : existing.Clone();
        var previousTimeZoneId = map.TimeZoneId;

        map.TimeZoneId = timeZoneId;

        // The handler checks the name and the time zone, writes the time zone's canonical id, and refuses a time zone
        // another map already has, as the recipe step does; the admin editor skips that check.
        var validation = await manager.ValidateAsync(map, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}";
        }

        if (all.FirstOrDefault(other => other.ItemId != map.ItemId && string.Equals(other.TimeZoneId, map.TimeZoneId, StringComparison.OrdinalIgnoreCase)) is { } sameZone)
        {
            return $"Nothing was saved: the time zone {map.TimeZoneId} already has the friendly name '{sameZone.Name}'. A time zone can have one name; change that map instead.";
        }

        if (isNew)
        {
            await manager.CreateAsync(map, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(map, cancellationToken: cancellationToken);
        }

        changes.Saved(map);

        return Json(new
        {
            id = map.ItemId,
            name = map.Name,
            timeZoneId = map.TimeZoneId,
            created = isNew,
            previousTimeZoneId = isNew ? null : previousTimeZoneId,
            message = isNew
                ? $"Time zone pickers now offer '{map.Name}' for {map.TimeZoneId}."
                : $"'{map.Name}' now stands for {map.TimeZoneId}. Records that stored the old time zone id keep it.",
        });
    }
}
