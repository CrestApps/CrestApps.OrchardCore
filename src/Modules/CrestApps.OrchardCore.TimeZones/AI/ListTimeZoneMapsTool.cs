using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeZones.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TimeZones.AI;

/// <summary>
/// Lists the time zone maps, the friendly names every time zone picker shows while the Time Zones feature is on, as the
/// Time Zones admin list does.
/// </summary>
internal sealed class ListTimeZoneMapsTool : TimeZonesAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listTimeZoneMaps";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "Text in the friendly name or the time zone id, such as 'Denver' or 'America/'. Leave it out to list every map."
        },
        "page": {
          "type": "integer",
          "description": "The page, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "The maps per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the friendly time zone names shown in time zone pickers and the time zone each maps to, with an optional name filter.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var filter = ReadString(arguments, "name");
        var page = Math.Max(1, ReadInt(arguments, "page") ?? 1);
        var pageSize = Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize);
        var all = await services.GetRequiredService<INamedCatalog<TimeZoneMap>>().GetAllAsync(cancellationToken);

        var matches = all
            .Where(map => filter is null
                || (map.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || (map.TimeZoneId?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(map => map.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(map => map.TimeZoneId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = matches
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(map => new
            {
                id = map.ItemId,
                name = map.Name,
                timeZoneId = map.TimeZoneId,
            })
            .ToList();

        return Json(new
        {
            items,
            total = matches.Count,
            page,
            pageSize,
            truncated = page * pageSize < matches.Count,
            message = matches.Count == 0 && filter is null
                ? "No time zone map exists, so time zone pickers offer no time zone. Add one on the Time Zones admin page."
                : "Time zone pickers offer exactly these names, each storing its time zone id. Use listTimeZones for every valid time zone id.",
        });
    }
}
