using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Products.Models;

namespace CrestApps.OrchardCore.Products.Recipes;

/// <summary>
/// Imports the currencies carried by a recipe step. An entry is matched by its identifier, and then by its code.
/// </summary>
internal sealed class CurrencyStep : NamedCatalogRecipeStep<CurrencyEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyStep"/> class.
    /// </summary>
    /// <param name="manager">The currency manager.</param>
    public CurrencyStep(INamedCatalogManager<CurrencyEntry> manager)
        : base(ProductsConstants.Recipes.Currencies, "Currencies", manager)
    {
    }

    /// <inheritdoc/>
    protected override bool MatchByName => true;
}