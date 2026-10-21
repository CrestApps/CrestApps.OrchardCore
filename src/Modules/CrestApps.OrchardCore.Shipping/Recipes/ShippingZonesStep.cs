using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Shipping.Handlers;
using CrestApps.OrchardCore.Shipping.Models;

namespace CrestApps.OrchardCore.Shipping.Recipes;

/// <summary>
/// The "ShippingZones" step: creates or updates zones, found by id or by name, with their methods.
/// </summary>
internal sealed class ShippingZonesStep : NamedCatalogRecipeStep<ShippingZone>
{
    public ShippingZonesStep(INamedCatalogManager<ShippingZone> manager)
        : base(ShippingConstants.ZonesRecipeStep, ShippingConstants.ZonesRecipeStepEntries, manager)
    {
    }

    /// <inheritdoc/>
    protected override bool MatchByName => true;

    /// <inheritdoc/>
    protected override void Populate(ShippingZone entry, JsonObject data)
        => ShippingZoneHandler.PopulateZone(entry, data);
}
