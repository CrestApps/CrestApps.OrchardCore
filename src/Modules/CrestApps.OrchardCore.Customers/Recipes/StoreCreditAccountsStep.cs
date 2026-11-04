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
/// Adds or updates customers' store credit, with its balance and activity, from a <c>StoreCreditAccounts</c> recipe
/// step. An account is matched by its <c>ItemId</c>, then by its customer and currency, so a second run changes it
/// instead of adding another.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "StoreCreditAccounts", "Accounts": [ { "ItemId": "...", "CustomerId": "...", "Currency": "USD", "Balance": 25, "Activity": [] } ] }
/// </code>
/// The customer is named by its id: import the customers first, with the <c>Customers</c> step.
/// </remarks>
internal sealed class StoreCreditAccountsStep : CatalogStoreRecipeStep<StoreCreditAccount>
{
    public const string EntriesName = "Accounts";

    private readonly IStoreCreditStore _store;

    internal readonly IStringLocalizer S;

    public StoreCreditAccountsStep(IStoreCreditStore store, IStringLocalizer<StoreCreditAccountsStep> stringLocalizer)
        : base(CustomersConstants.Recipes.StoreCreditAccounts, EntriesName, store)
    {
        _store = store;
        S = stringLocalizer;
    }

    protected override async ValueTask<CatalogRecipeLookup<StoreCreditAccount>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var customerId = token[nameof(StoreCreditAccount.CustomerId)]?.GetValue<string>();
        var currency = token[nameof(StoreCreditAccount.Currency)]?.GetValue<string>()?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(customerId) || string.IsNullOrEmpty(currency))
        {
            return CatalogRecipeLookup<StoreCreditAccount>.NotFound;
        }

        var account = await _store.FindAsync(customerId, currency);

        return account is null ? CatalogRecipeLookup<StoreCreditAccount>.NotFound : CatalogRecipeLookup<StoreCreditAccount>.Found(account);
    }

    protected override void Preparing(StoreCreditAccount entry, JsonObject token)
    {
        entry.Currency = entry.Currency?.Trim().ToUpperInvariant();
        entry.Activity ??= [];
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(StoreCreditAccount entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.CustomerId))
        {
            result.Fail(new ValidationResult(S["A store credit account needs a customer."]));
        }

        if (string.IsNullOrEmpty(entry.Currency))
        {
            result.Fail(new ValidationResult(S["A store credit account needs a currency."]));
        }

        return ValueTask.FromResult(result);
    }
}
