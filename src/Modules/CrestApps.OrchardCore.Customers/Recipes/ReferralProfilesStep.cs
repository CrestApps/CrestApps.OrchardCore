using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Customers.Recipes;

/// <summary>
/// Adds or updates customers' referral codes, with the referrals they earned, from a <c>ReferralProfiles</c> recipe
/// step. A profile is matched by its <c>ItemId</c>, then by its customer, so a second run changes it instead of adding
/// another. A code that another customer already uses is refused.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "ReferralProfiles", "Profiles": [ { "ItemId": "...", "CustomerId": "...", "Code": "ANA10", "Referrals": [] } ] }
/// </code>
/// The customer is named by its id: import the customers first, with the <c>Customers</c> step.
/// </remarks>
internal sealed class ReferralProfilesStep : CatalogStoreRecipeStep<ReferralProfile>
{
    public const string EntriesName = "Profiles";

    private readonly IReferralStore _store;

    internal readonly IStringLocalizer S;

    public ReferralProfilesStep(IReferralStore store, IStringLocalizer<ReferralProfilesStep> stringLocalizer)
        : base(CustomersConstants.Recipes.ReferralProfiles, EntriesName, store)
    {
        _store = store;
        S = stringLocalizer;
    }

    protected override async ValueTask<CatalogRecipeLookup<ReferralProfile>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var customerId = token[nameof(ReferralProfile.CustomerId)]?.GetValue<string>();
        var profile = string.IsNullOrEmpty(customerId) ? null : await _store.FindByCustomerAsync(customerId);

        return profile is null ? CatalogRecipeLookup<ReferralProfile>.NotFound : CatalogRecipeLookup<ReferralProfile>.Found(profile);
    }

    protected override void Preparing(ReferralProfile entry, JsonObject token)
    {
        entry.Code = entry.Code?.Trim();
        entry.Referrals ??= [];
    }

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(ReferralProfile entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.CustomerId))
        {
            result.Fail(new ValidationResult(S["A referral profile needs a customer."]));
        }

        if (string.IsNullOrEmpty(entry.Code))
        {
            result.Fail(new ValidationResult(S["A referral profile needs a code."]));
        }
        else if (await _store.FindByCodeAsync(entry.Code) is { } other && other.ItemId != entry.ItemId)
        {
            result.Fail(new ValidationResult(S["The referral code '{0}' is already used by another customer.", entry.Code]));
        }

        return result;
    }
}
