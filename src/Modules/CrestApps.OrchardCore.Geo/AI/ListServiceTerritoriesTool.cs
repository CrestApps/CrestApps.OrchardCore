using System.Text.Json;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Geo.AI;

/// <summary>
/// Lists the service territories as the Territories page does, ordered by name. Given an id, it also returns the
/// territory's GeoJSON shape and the member kinds that can be assigned, as the territory editor offers them.
/// </summary>
internal sealed class ListServiceTerritoriesTool : GeoAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listServiceTerritories";

    /// <summary>
    /// The most characters of a shape returned.
    /// </summary>
    public const int MaxShapeLength = 20_000;

    /// <summary>
    /// The most members of each kind offered.
    /// </summary>
    public const int MaxMemberOptions = 50;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "A territory's id, to get that territory with its GeoJSON shape and the member kinds that can be assigned."
        },
        "name": {
          "type": "string",
          "description": "Text in the territory's name."
        },
        "page": {
          "type": "integer",
          "description": "The page, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "The territories per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the service territories with parent, postal codes, time zone, travel speed, members and whether a shape is drawn; pass an id to also get its GeoJSON shape and the member kinds that can be assigned.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => GeoPermissions.ManageTerritories;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<ServiceTerritoryManager>();
        var all = (await manager.GetAllAsync(cancellationToken)).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var names = all.ToDictionary(item => item.ItemId, item => item.Name, StringComparer.Ordinal);
        var sources = await TerritoryToolDescriptions.ReadMemberSourcesAsync(services.GetServices<ITerritoryMemberSource>(), cancellationToken);
        var id = ReadString(arguments, "id");

        if (id is not null)
        {
            var territory = all.FirstOrDefault(item => item.ItemId == id);

            if (territory is null)
            {
                return $"No service territory has the id '{id}'. Call {Name} without an id to list them.";
            }

            var shape = territory.Shape;

            return Json(new
            {
                territory = TerritoryToolDescriptions.Describe(territory, names, sources),
                allPostalCodes = territory.PostalCodes,
                shape = shape is null || shape.Length <= MaxShapeLength ? shape : null,
                shapeTruncated = shape is not null && shape.Length > MaxShapeLength,
                children = all.Where(item => item.ParentId == territory.ItemId).Select(item => new { id = item.ItemId, name = item.Name }).ToList(),
                memberKinds = sources.Select(source => new
                {
                    kind = source.Key,
                    displayName = source.Value.DisplayName,
                    total = source.Value.Names.Count,
                    options = source.Value.Names.Take(MaxMemberOptions).Select(option => new { id = option.Key, name = option.Value }).ToList(),
                }).ToList(),
            });
        }

        var filter = ReadString(arguments, "name");
        var page = Math.Max(1, ReadInt(arguments, "page") ?? 1);
        var pageSize = Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize);
        var matches = all.Where(item => filter is null || (item.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        return Json(new
        {
            items = matches.Skip((page - 1) * pageSize).Take(pageSize).Select(item => TerritoryToolDescriptions.Describe(item, names, sources)).ToList(),
            total = matches.Count,
            page,
            pageSize,
            truncated = page * pageSize < matches.Count,
            memberKinds = sources.Select(source => new { kind = source.Key, displayName = source.Value.DisplayName }).ToList(),
        });
    }
}
