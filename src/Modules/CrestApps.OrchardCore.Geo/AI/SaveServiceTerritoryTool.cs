using System.Text.Json;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Geo.AI;

/// <summary>
/// Creates or updates a service territory as the territory editor does: the territory manager's checks (a name, a shape
/// or postal codes, a readable shape, a travel speed in range, no loop of parents) and the editor's member merge, with
/// the members of the kinds not given kept. Stricter than the editor, which can only post what it lists: the parent
/// must exist, the time zone must be one the time zone pickers offer, and each member must be one its kind lists.
/// </summary>
internal sealed class SaveServiceTerritoryTool : GeoAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveServiceTerritory";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the territory to update, from listServiceTerritories. Leave it out to create a territory."
        },
        "name": {
          "type": "string",
          "description": "The territory's name. Required for a new territory."
        },
        "parentId": {
          "type": "string",
          "description": "The id of the territory it is part of. An empty text makes it a top-level territory."
        },
        "shape": {
          "type": "string",
          "description": "The shape as GeoJSON: a Polygon or MultiPolygon geometry, Feature or FeatureCollection, with [longitude, latitude] positions. An empty text removes the shape."
        },
        "postalCodes": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The postal codes it covers; one ending with * covers every code that starts with the rest, such as 787*. Replaces the stored codes; an empty list removes them."
        },
        "timeZoneId": {
          "type": "string",
          "description": "The territory's time zone, one the time zone pickers offer (see listTimeZones or listTimeZoneMaps). An empty text uses the site's."
        },
        "travelSpeedKmh": {
          "type": "number",
          "description": "The average travel speed in km/h, more than 0 and at most 200. 0 uses the site's."
        },
        "members": {
          "type": "object",
          "description": "Who serves the territory, as lists of ids by member kind, such as { \"Team\": [\"id1\"] }. The kinds and ids come from listServiceTerritories with an id. Each kind given replaces that kind's members; kinds not given are kept.",
          "additionalProperties": {
            "type": "array",
            "items": { "type": "string" }
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a service territory: name, parent, GeoJSON shape or postal codes, time zone, travel speed and members by kind. Members of kinds you do not pass are kept.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => GeoPermissions.ManageTerritories;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<ServiceTerritoryManager>();
        var id = ReadString(arguments, "id");
        var isNew = id is null;
        var item = isNew ? await manager.NewAsync(cancellationToken: cancellationToken) : await manager.FindByIdAsync(id, cancellationToken);

        if (item is null)
        {
            return $"Nothing was saved: no service territory has the id '{id}'. Use listServiceTerritories to find it, or leave id out to create one.";
        }

        if (isNew && ReadString(arguments, "name") is null)
        {
            return "Nothing was saved: a new territory needs a name.";
        }

        if (Has(arguments, "name"))
        {
            item.Name = ReadString(arguments, "name");
        }

        var all = await manager.GetAllAsync(cancellationToken);

        if (Has(arguments, "parentId"))
        {
            var parentId = ReadString(arguments, "parentId");

            if (parentId is not null && !all.Any(other => other.ItemId == parentId))
            {
                return $"Nothing was saved: no service territory has the id '{parentId}' to be the parent.";
            }

            item.ParentId = parentId;
        }

        if (Has(arguments, "shape"))
        {
            item.Shape = ReadString(arguments, "shape");
        }

        if (Has(arguments, "postalCodes"))
        {
            item.PostalCodes = ReadStringList(arguments, "postalCodes") ?? [];
        }

        if (Has(arguments, "timeZoneId"))
        {
            var timeZoneId = ReadString(arguments, "timeZoneId");

            if (timeZoneId is not null && !string.Equals(timeZoneId, item.TimeZoneId, StringComparison.OrdinalIgnoreCase))
            {
                var offered = await services.GetRequiredService<ITimeZoneSelectListProvider>().GetTimeZoneSelectListAsync(cancellationToken);
                var match = offered.FirstOrDefault(zone => string.Equals(zone.Key, timeZoneId, StringComparison.OrdinalIgnoreCase) || string.Equals(zone.Value, timeZoneId, StringComparison.OrdinalIgnoreCase));

                if (match.Key is null)
                {
                    return $"Nothing was saved: '{timeZoneId}' is not a time zone the time zone pickers offer. Use listTimeZones (or listTimeZoneMaps when friendly names are set up) for the ids.";
                }

                timeZoneId = match.Key;
            }

            item.TimeZoneId = timeZoneId;
        }

        if (Has(arguments, "travelSpeedKmh"))
        {
            var speed = ReadDouble(arguments, "travelSpeedKmh");

            item.TravelSpeedKmh = speed is null or 0 ? null : speed;
        }

        if (ReadJson(arguments, "members") is { } membersJson)
        {
            if (membersJson.ValueKind != JsonValueKind.Object)
            {
                return "Nothing was saved: members must be an object of id lists by member kind, such as { \"Team\": [\"id1\"] }.";
            }

            var sources = await TerritoryToolDescriptions.ReadMemberSourcesAsync(services.GetServices<ITerritoryMemberSource>(), cancellationToken);
            var idsByKind = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var property in membersJson.EnumerateObject())
            {
                var kind = sources.Keys.FirstOrDefault(key => string.Equals(key, property.Name, StringComparison.OrdinalIgnoreCase));

                if (kind is null)
                {
                    return sources.Count == 0
                        ? $"Nothing was saved: no module lists members for territories, so '{property.Name}' cannot be set."
                        : $"Nothing was saved: '{property.Name}' is not a member kind. Use one of: {string.Join(", ", sources.Keys)}.";
                }

                if (property.Value.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null))
                {
                    return $"Nothing was saved: the members of '{kind}' must be a list of ids.";
                }

                var ids = property.Value.ValueKind == JsonValueKind.Null
                    ? []
                    : property.Value.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : value.ToString()).Where(value => !string.IsNullOrEmpty(value)).ToList();

                if (ids.FirstOrDefault(memberId => !sources[kind].Names.ContainsKey(memberId)) is { } unknown)
                {
                    return $"Nothing was saved: '{unknown}' is not one of the {sources[kind].DisplayName ?? kind} that can serve a territory. Use listServiceTerritories with an id for the ids.";
                }

                idsByKind[kind] = ids;
            }

            item.Members = TerritoryCommands.MergeMembers(item.Members, idsByKind.Keys, idsByKind);
        }

        var validation = await manager.ValidateAsync(item, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}";
        }

        if (isNew)
        {
            await manager.CreateAsync(item, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(item, cancellationToken: cancellationToken);
        }

        var names = all.ToDictionary(territory => territory.ItemId, territory => territory.Name, StringComparer.Ordinal);
        var memberSources = await TerritoryToolDescriptions.ReadMemberSourcesAsync(services.GetServices<ITerritoryMemberSource>(), cancellationToken);

        return Json(new
        {
            territory = TerritoryToolDescriptions.Describe(item, names, memberSources),
            created = isNew,
            message = isNew ? $"The territory '{item.Name}' was created." : $"The territory '{item.Name}' was updated.",
        });
    }
}
