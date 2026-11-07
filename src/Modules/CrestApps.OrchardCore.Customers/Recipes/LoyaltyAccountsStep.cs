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
/// Adds or updates customers' loyalty points, with their activity, from a <c>LoyaltyAccounts</c> recipe step. An
/// account is matched by its <c>ItemId</c>, then by its customer, so a second run changes it instead of adding another.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "LoyaltyAccounts", "Accounts": [ { "ItemId": "...", "CustomerId": "...", "Points": 1200, "Activity": [] } ] }
/// </code>
/// The customer is named by its id: import the customers first, with the <c>Customers</c> step.
/// </remarks>
internal sealed class LoyaltyAccountsStep : CatalogStoreRecipeStep<LoyaltyAccount>
{
    public const string EntriesName = "Accounts";

    private readonly ILoyaltyStore _store;

    internal readonly IStringLocalizer S;

    public LoyaltyAccountsStep(ILoyaltyStore store, IStringLocalizer<LoyaltyAccountsStep> stringLocalizer)
        : base(CustomersConstants.Recipes.LoyaltyAccounts, EntriesName, store)
    {
        _store = store;
        S = stringLocalizer;
    }

    protected override async ValueTask<CatalogRecipeLookup<LoyaltyAccount>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var customerId = token[nameof(LoyaltyAccount.CustomerId)]?.GetValue<string>();
        var account = string.IsNullOrEmpty(customerId) ? null : await _store.FindByCustomerAsync(customerId);

        return account is null ? CatalogRecipeLookup<LoyaltyAccount>.NotFound : CatalogRecipeLookup<LoyaltyAccount>.Found(account);
    }

    protected override void Preparing(LoyaltyAccount entry, JsonObject token)
    {
        entry.Points = Math.Max(0, entry.Points);
        entry.Activity ??= [];
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(LoyaltyAccount entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.CustomerId))
        {
            result.Fail(new ValidationResult(S["A loyalty account needs a customer."]));
        }

        return ValueTask.FromResult(result);
    }
}
