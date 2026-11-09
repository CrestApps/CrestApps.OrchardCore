using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Inventory.Recipes;

/// <summary>
/// Adds or updates the store's own stock locations from a <c>StockLocations</c> recipe step. A location keeps the id
/// the recipe gives it, such as <c>default</c>, so stock levels, registers and movements keep pointing at it; a
/// location without an id is matched by its name. A location needs a name, as on the Inventory settings page.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "StockLocations", "Locations": [ { "ItemId": "default", "Name": "Main warehouse", "Address": "1 Dock Rd", "Position": 1 } ] }
/// </code>
/// Locations another module owns, such as a technician's van, are kept by that module and are not part of the step.
/// </remarks>
internal sealed class StockLocationsStep : CatalogStoreRecipeStep<StockLocation>
{
    public const string EntriesName = "Locations";

    private readonly ICatalog<StockLocation> _store;

    internal readonly IStringLocalizer S;

    public StockLocationsStep(ICatalog<StockLocation> store, IStringLocalizer<StockLocationsStep> stringLocalizer)
        : base(InventoryConstants.StockLocationsRecipeStep, EntriesName, store)
    {
        _store = store;
        S = stringLocalizer;
    }

    protected override bool CanImport(JsonObject token, RecipeExecutionContext context)
        => string.IsNullOrEmpty(token[nameof(StockLocation.OwnerKind)]?.GetValue<string>());

    protected override async ValueTask<CatalogRecipeLookup<StockLocation>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var name = token[nameof(StockLocation.Name)]?.GetValue<string>()?.Trim();

        if (string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(token[nameof(StockLocation.ItemId)]?.GetValue<string>()))
        {
            return CatalogRecipeLookup<StockLocation>.NotFound;
        }

        var location = (await _store.GetAllAsync()).FirstOrDefault(entry => entry.OwnerKind is null && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

        return location is null ? CatalogRecipeLookup<StockLocation>.NotFound : CatalogRecipeLookup<StockLocation>.Found(location);
    }

    protected override async ValueTask<StockLocation> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var location = await base.NewAsync(token, context);
        var id = token[nameof(StockLocation.ItemId)]?.GetValue<string>()?.Trim();

        // Location ids are kept as they are, such as the default location's, because stock points at them.
        if (location is not null && !string.IsNullOrEmpty(id))
        {
            location.ItemId = id;
        }

        return location;
    }

    protected override void Preparing(StockLocation entry, JsonObject token)
    {
        entry.Name = entry.Name?.Trim();
        entry.Address = string.IsNullOrWhiteSpace(entry.Address) ? null : entry.Address.Trim();
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(StockLocation entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.Name))
        {
            result.Fail(new ValidationResult(S["Give the location a name, such as Main warehouse."]));
        }

        return ValueTask.FromResult(result);
    }
}
