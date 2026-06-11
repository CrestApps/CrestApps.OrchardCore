using OrchardCore.Data.Migration;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.TimeZones.Migrations;

/// <summary>
/// Seeds default time zone maps for the Time Zones feature.
/// </summary>
public sealed class TimeZoneMapMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneMapMigrations"/> class.
    /// </summary>
    /// <param name="recipeMigrator">The recipe migrator.</param>
    public TimeZoneMapMigrations(IRecipeMigrator recipeMigrator)
    {
        _recipeMigrator = recipeMigrator;
    }

    /// <summary>
    /// Creates the initial migration state.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await _recipeMigrator.ExecuteAsync($"default-timezones{RecipesConstants.RecipeExtension}", this);

        return 1;
    }
}
