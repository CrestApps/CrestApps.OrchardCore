using OrchardCore.Data.Migration;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Products.Migrations;

/// <summary>
/// Seeds the managed currencies used by products and subscriptions.
/// </summary>
public sealed class CurrencyMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyMigrations"/> class.
    /// </summary>
    /// <param name="recipeMigrator">The recipe migrator.</param>
    public CurrencyMigrations(IRecipeMigrator recipeMigrator)
    {
        _recipeMigrator = recipeMigrator;
    }

    /// <summary>
    /// Creates the initial migration state.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await _recipeMigrator.ExecuteAsync($"default-currencies{RecipesConstants.RecipeExtension}", this);

        return 1;
    }
}
