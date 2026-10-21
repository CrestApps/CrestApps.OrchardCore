using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
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

internal sealed class ShippingZonesDeploymentSource : NamedCatalogDeploymentSource<ShippingZonesDeploymentStep, ShippingZone>
{
    public ShippingZonesDeploymentSource(INamedCatalogManager<ShippingZone> manager)
        : base(ShippingConstants.ZonesRecipeStepEntries, manager)
    {
    }

    /// <inheritdoc/>
    protected override IEnumerable<ShippingZone> Order(IEnumerable<ShippingZone> entries)
        => entries.OrderBy(zone => zone.Position).ThenBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase);
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
