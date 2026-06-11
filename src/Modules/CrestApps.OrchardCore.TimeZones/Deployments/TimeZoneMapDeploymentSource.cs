using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeZones.Models;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.TimeZones.Deployments;

internal sealed class TimeZoneMapDeploymentSource : DeploymentSourceBase<TimeZoneMapDeploymentStep>
{
    private readonly INamedCatalog<TimeZoneMap> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneMapDeploymentSource"/> class.
    /// </summary>
    /// <param name="catalog">The time zone map catalog.</param>
    public TimeZoneMapDeploymentSource(INamedCatalog<TimeZoneMap> catalog)
    {
        _catalog = catalog;
    }

    protected override async Task ProcessAsync(TimeZoneMapDeploymentStep step, DeploymentPlanResult result)
    {
        var maps = await _catalog.GetAllAsync();
        var mapObjects = new JsonArray();
        var mapIds = step.IncludeAll ? [] : step.MapIds ?? [];

        foreach (var map in maps.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (mapIds.Length > 0 && !mapIds.Contains(map.ItemId))
            {
                continue;
            }

            mapObjects.Add(new JsonObject
            {
                ["ItemId"] = map.ItemId,
                ["Name"] = map.Name,
                ["TimeZoneId"] = map.TimeZoneId,
                ["CreatedUtc"] = map.CreatedUtc,
                ["ModifiedUtc"] = map.ModifiedUtc,
                ["OwnerId"] = map.OwnerId,
                ["Author"] = map.Author,
            });
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Maps"] = mapObjects,
        });
    }
}
