using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Products.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Products.Recipes;

internal sealed class CurrencyStep : NamedRecipeStepHandler
{
    private readonly INamedCatalogManager<CurrencyEntry> _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyStep"/> class.
    /// </summary>
    /// <param name="manager">The currency manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CurrencyStep(
        INamedCatalogManager<CurrencyEntry> manager,
        IStringLocalizer<CurrencyStep> stringLocalizer)
        : base(ProductsConstants.Recipes.Currencies)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<CurrenciesStepModel>();
        var tokens = model.Currencies?.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            CurrencyEntry currency = null;
            var isNew = false;

            var id = token[nameof(CurrencyEntry.ItemId)]?.GetValue<string>();
            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                currency = await _manager.FindByIdAsync(id);
            }

            if (currency is null)
            {
                var code = token[nameof(CurrencyEntry.Name)]?.GetValue<string>()?.Trim();

                if (!string.IsNullOrEmpty(code))
                {
                    currency = await _manager.FindByNameAsync(code);
                }
            }

            if (currency is not null)
            {
                await _manager.UpdateAsync(currency, token);
            }
            else
            {
                isNew = true;
                currency = await _manager.NewAsync(token);

                if (hasId && UniqueId.IsValid(id))
                {
                    currency.ItemId = id;
                }
            }

            var validationResult = await _manager.ValidateAsync(currency);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            if (isNew)
            {
                await _manager.CreateAsync(currency);
            }
        }
    }

    private sealed class CurrenciesStepModel
    {
        /// <summary>
        /// Gets or sets the currencies to import.
        /// </summary>
        public JsonArray Currencies { get; set; }
    }
}
