using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Markets.Services;
using CrestApps.OrchardCore.Products.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Markets.Recipes;

/// <summary>
/// Adds or updates markets from a <c>Markets</c> recipe step. A market is matched by its <c>ItemId</c>, then by its
/// name, so a second run changes it instead of adding another. A market is checked the way its admin screen checks
/// it: a name, two-letter countries that no other market has, a currency code and a positive exchange rate.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "Markets", "Markets": [ { "ItemId": "...", "Name": "Europe", "Countries": [ "DE", "AT" ], "Currency": "EUR", "ExchangeRate": 0.92 } ] }
/// </code>
/// </remarks>
internal sealed class MarketsStep : CatalogStoreRecipeStep<Market>
{
    public const string EntriesName = "Markets";

    private readonly IMarketStore _store;

    internal readonly IStringLocalizer S;

    public MarketsStep(IMarketStore store, IStringLocalizer<MarketsStep> stringLocalizer)
        : base(MarketsConstants.Recipes.Markets, EntriesName, store)
    {
        _store = store;
        S = stringLocalizer;
    }

    protected override async ValueTask<CatalogRecipeLookup<Market>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var name = token[nameof(Market.Name)]?.GetValue<string>()?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return CatalogRecipeLookup<Market>.NotFound;
        }

        var market = (await _store.GetAllAsync()).FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

        return market is null ? CatalogRecipeLookup<Market>.NotFound : CatalogRecipeLookup<Market>.Found(market);
    }

    protected override void Preparing(Market entry, JsonObject token)
    {
        entry.Name = entry.Name?.Trim();
        entry.Currency = entry.Currency?.Trim().ToUpperInvariant();
        entry.Countries = [.. (entry.Countries ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)];
        entry.Culture = string.IsNullOrWhiteSpace(entry.Culture) ? null : entry.Culture.Trim();
    }

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(Market entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.Name))
        {
            result.Fail(new ValidationResult(S["Name the market, such as Europe or Germany."]));
        }

        if (entry.Countries.Count == 0 || !entry.Countries.All(CountryCodeUtility.IsCountryCode))
        {
            result.Fail(new ValidationResult(S["List the market's countries by their two-letter codes, such as DE, AT."]));
        }
        else
        {
            // A country is in one market only, so a shopper's market is never a guess between two.
            var taken = (await _store.GetAllAsync())
                .Where(other => other.ItemId != entry.ItemId)
                .SelectMany(other => other.Countries.Where(entry.Countries.Contains).Select(code => (code, other.Name)))
                .FirstOrDefault();

            if (taken.code is not null)
            {
                result.Fail(new ValidationResult(S["{0} is already in the market {1}.", taken.code, taken.Name]));
            }
        }

        if (!CurrencyCodeUtility.IsValid(entry.Currency))
        {
            result.Fail(new ValidationResult(S["Enter the market's three-letter currency code, such as EUR."]));
        }

        if (entry.ExchangeRate <= 0m)
        {
            result.Fail(new ValidationResult(S["Enter how much of the market's currency one unit of the store's currency is worth."]));
        }

        return result;
    }
}
