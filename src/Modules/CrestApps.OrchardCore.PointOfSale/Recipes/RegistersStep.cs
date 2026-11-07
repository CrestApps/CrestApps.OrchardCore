using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.PointOfSale.Recipes;

/// <summary>
/// Adds or updates point-of-sale registers from a <c>Registers</c> recipe step. A register is matched by its
/// <c>ItemId</c>, then by its name, so a second run changes it instead of adding another. A register is checked the way
/// its admin screen checks it: a name, a store location that exists, a street address with a city and a two-letter
/// country, and a three-letter currency or none.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "Registers", "Registers": [ { "ItemId": "...", "Name": "Front till", "LocationId": "default",
///   "Address": { "AddressLine1": "1 Main St", "City": "Springfield", "Country": "US" }, "Currency": "USD" } ] }
/// </code>
/// The store location is named by its id: import the stock locations first, with the <c>StockLocations</c> step.
/// </remarks>
internal sealed class RegistersStep : CatalogStoreRecipeStep<Register>
{
    public const string EntriesName = "Registers";

    private readonly IRegisterStore _store;
    private readonly IInventoryLocationService _locations;

    internal readonly IStringLocalizer S;

    public RegistersStep(
        IRegisterStore store,
        IInventoryLocationService locations,
        IStringLocalizer<RegistersStep> stringLocalizer)
        : base(PosConstants.Recipes.Registers, EntriesName, store)
    {
        _store = store;
        _locations = locations;
        S = stringLocalizer;
    }

    protected override async ValueTask<CatalogRecipeLookup<Register>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var name = token[nameof(Register.Name)]?.GetValue<string>()?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return CatalogRecipeLookup<Register>.NotFound;
        }

        var register = (await _store.GetAllAsync()).FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

        return register is null ? CatalogRecipeLookup<Register>.NotFound : CatalogRecipeLookup<Register>.Found(register);
    }

    protected override void Preparing(Register entry, JsonObject token)
    {
        entry.Name = entry.Name?.Trim();
        entry.Currency = string.IsNullOrWhiteSpace(entry.Currency) ? null : entry.Currency.Trim().ToUpperInvariant();
        entry.Roles ??= [];
    }

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(Register entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.Name))
        {
            result.Fail(new ValidationResult(S["Name the register, such as the store and till it's at."]));
        }

        if (!(await _locations.GetLocationsAsync()).Any(location => location.LocationId == entry.LocationId))
        {
            result.Fail(new ValidationResult(S["Choose the store location the register sells from."]));
        }

        if (string.IsNullOrWhiteSpace(entry.Address?.AddressLine1) || string.IsNullOrWhiteSpace(entry.Address.City) || !CountryCodeUtility.IsCountryCode(entry.Address.Country))
        {
            result.Fail(new ValidationResult(S["Enter the store's street address, city and two-letter country code: its sales are taxed by it."]));
        }

        if (entry.Currency is not null && entry.Currency.Length != 3)
        {
            result.Fail(new ValidationResult(S["Enter a three-letter currency code, such as USD, or leave it blank."]));
        }

        return result;
    }
}
