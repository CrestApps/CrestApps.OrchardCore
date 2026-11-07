using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Adds or updates the store's own tax registrations from a <c>MerchantTaxRegistration</c> recipe step. A registration
/// is matched by its <c>ItemId</c>, then by its name, so a second run changes it instead of adding another. A
/// registration needs a name and the jurisdiction it is registered in.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "MerchantTaxRegistration", "MerchantTaxRegistrations": [ { "ItemId": "...", "Name": "California sales tax", "JurisdictionId": "...", "TaxType": "SalesTax", "RegistrationNumber": "123-456", "IsActive": true } ] }
/// </code>
/// </remarks>
internal sealed class MerchantTaxRegistrationStep : CatalogStoreRecipeStep<MerchantTaxRegistration>
{
    public const string EntriesName = TaxationDeploymentSteps.MerchantTaxRegistrationEntries;

    private readonly IMerchantTaxRegistrationStore _store;

    internal readonly IStringLocalizer S;

    public MerchantTaxRegistrationStep(IMerchantTaxRegistrationStore store, IStringLocalizer<MerchantTaxRegistrationStep> stringLocalizer)
        : base(TaxationDeploymentSteps.MerchantTaxRegistration, EntriesName, store)
    {
        _store = store;
        S = stringLocalizer;
    }

    protected override async ValueTask<CatalogRecipeLookup<MerchantTaxRegistration>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var name = token[nameof(MerchantTaxRegistration.Name)]?.GetValue<string>()?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return CatalogRecipeLookup<MerchantTaxRegistration>.NotFound;
        }

        var registration = (await _store.GetAllAsync()).FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

        return registration is null ? CatalogRecipeLookup<MerchantTaxRegistration>.NotFound : CatalogRecipeLookup<MerchantTaxRegistration>.Found(registration);
    }

    protected override void Preparing(MerchantTaxRegistration entry, JsonObject token)
    {
        entry.Name = entry.Name?.Trim();
        entry.RegistrationNumber = entry.RegistrationNumber?.Trim();
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(MerchantTaxRegistration entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.Name))
        {
            result.Fail(new ValidationResult(S["A tax registration needs a name."]));
        }

        if (string.IsNullOrWhiteSpace(entry.JurisdictionId))
        {
            result.Fail(new ValidationResult(S["The tax registration '{0}' needs the jurisdiction it is registered in.", entry.Name]));
        }

        return ValueTask.FromResult(result);
    }
}
