using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Shipping.Handlers;
using CrestApps.OrchardCore.Shipping.Models;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Shipping.Recipes;

/// <summary>
/// The "ShippingZones" step: creates or updates zones, found by id or by name, with their methods.
/// </summary>
internal sealed class ShippingZonesStep : NamedRecipeStepHandler
{
    private readonly INamedCatalogManager<ShippingZone> _manager;

    public ShippingZonesStep(INamedCatalogManager<ShippingZone> manager)
        : base(ShippingConstants.ZonesRecipeStep)
    {
        _manager = manager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in (context.Step["Zones"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var id = token[nameof(ShippingZone.ItemId)]?.GetValue<string>();
            var name = token[nameof(ShippingZone.Name)]?.GetValue<string>()?.Trim();

            var zone = !string.IsNullOrEmpty(id) ? await _manager.FindByIdAsync(id) : null;
            zone ??= !string.IsNullOrEmpty(name) ? await _manager.FindByNameAsync(name) : null;

            var isNew = zone is null;
            ShippingZone candidate;

            if (isNew)
            {
                zone = await _manager.NewAsync(token);

                if (!string.IsNullOrEmpty(id) && UniqueId.IsValid(id))
                {
                    zone.ItemId = id;
                }

                candidate = zone;
            }
            else
            {
                // The update is checked on a detached copy first, so an invalid zone is reported as a recipe error
                // instead of being written to the store.
                candidate = zone.Clone();
                ShippingZoneHandler.Populate(candidate, token);
            }

            var validation = await _manager.ValidateAsync(candidate);

            if (!validation.Succeeded)
            {
                foreach (var error in validation.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            if (isNew)
            {
                await _manager.CreateAsync(zone);
            }
            else
            {
                await _manager.UpdateAsync(zone, token);
            }
        }
    }
}
