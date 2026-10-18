using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Shipping.Deployments;

/// <summary>
/// Exports every shipping zone with its methods.
/// </summary>
public sealed class ShippingZonesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ShippingZonesDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ShippingZonesDeploymentStep>("Shipping Zones");

    public ShippingZonesDeploymentStep()
    {
        Name = ShippingConstants.ZonesRecipeStep;
        Category = _category;
        Title = _title;
    }
}

internal sealed class ShippingZonesDeploymentSource : DeploymentSourceBase<ShippingZonesDeploymentStep>
{
    private static readonly string[] _environmentOwned = ["CreatedUtc", "ModifiedUtc", "OwnerId", "Author"];

    private readonly INamedCatalog<ShippingZone> _catalog;

    public ShippingZonesDeploymentSource(INamedCatalog<ShippingZone> catalog)
    {
        _catalog = catalog;
    }

    protected override async Task ProcessAsync(ShippingZonesDeploymentStep step, DeploymentPlanResult result)
    {
        var zones = new JsonArray();

        foreach (var zone in (await _catalog.GetAllAsync()).OrderBy(zone => zone.Position).ThenBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase))
        {
            var node = JsonSerializer.SerializeToNode(zone)!.AsObject();

            foreach (var member in _environmentOwned)
            {
                node.Remove(member);
            }

            zones.Add(node);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Zones"] = zones,
        });
    }
}

internal sealed class ShippingZonesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ShippingZonesDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(ShippingZonesDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("ShippingZonesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ShippingZonesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(ShippingZonesDeploymentStep step, BuildEditorContext context)
        => View("ShippingZonesDeploymentStep_Fields_Edit", step).Location("Content");
}
