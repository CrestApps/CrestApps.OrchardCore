using System.Text.Json;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Geo.AI;

/// <summary>
/// Finds the territory of a point or a postal code with the territory resolver, as the Territories page's finder does:
/// the smallest shape the point is in, else the territory with the postal code. It never geocodes.
/// </summary>
internal sealed class FindServiceTerritoryTool : GeoAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "findServiceTerritory";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "latitude": {
          "type": "number",
          "description": "The latitude in degrees, from -90 to 90. Give it with longitude."
        },
        "longitude": {
          "type": "number",
          "description": "The longitude in degrees, from -180 to 180. Give it with latitude."
        },
        "postalCode": {
          "type": "string",
          "description": "The postal code, used when the point is in no territory's shape or no point is given."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Finds which service territory covers a latitude/longitude or a postal code. Use geocodeAddress first when you only have a street address.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => GeoPermissions.ManageTerritories;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var latitude = ReadDouble(arguments, "latitude");
        var longitude = ReadDouble(arguments, "longitude");
        var postalCode = ReadString(arguments, "postalCode");

        if ((latitude is null) != (longitude is null))
        {
            return "Nothing was searched: give both latitude and longitude, or neither.";
        }

        var point = GeoPoint.From(latitude, longitude);

        if (latitude is not null && point is null)
        {
            return "Nothing was searched: the latitude must be from -90 to 90 and the longitude from -180 to 180.";
        }

        if (point is null && postalCode is null)
        {
            return "Nothing was searched: give a latitude and longitude, a postal code, or both. Use geocodeAddress to get the point of a street address.";
        }

        var match = await services.GetRequiredService<ITerritoryResolver>().ResolveAsync(point, postalCode, cancellationToken);

        if (match is null)
        {
            return Json(new
            {
                found = false,
                latitude,
                longitude,
                postalCode,
                message = "No service territory covers this point or postal code.",
            });
        }

        var all = await services.GetRequiredService<ServiceTerritoryManager>().GetAllAsync(cancellationToken);
        var names = all.ToDictionary(item => item.ItemId, item => item.Name, StringComparer.Ordinal);
        var sources = await TerritoryToolDescriptions.ReadMemberSourcesAsync(services.GetServices<ITerritoryMemberSource>(), cancellationToken);

        return Json(new
        {
            found = true,
            matchedBy = match.MatchedBy.ToString(),
            territory = TerritoryToolDescriptions.Describe(match.Territory, names, sources),
            message = match.MatchedBy == TerritoryMatchKind.Shape
                ? $"The point is inside the shape of '{match.Territory.Name}'."
                : $"The postal code belongs to '{match.Territory.Name}'.",
        });
    }
}
