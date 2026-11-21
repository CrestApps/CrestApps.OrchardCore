using CrestApps.OrchardCore.Surveys.Core.Indexes;
using OrchardCore.Data.Migration;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Creates the index tables of Surveys and seeds the default survey templates.
/// </summary>
internal sealed class SurveysMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysMigrations"/> class.
    /// </summary>
    /// <param name="recipeMigrator">The recipe migrator.</param>
    public SurveysMigrations(IRecipeMigrator recipeMigrator)
    {
        _recipeMigrator = recipeMigrator;
    }

    /// <summary>
    /// Creates the index tables and runs the recipe of the default templates.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await SurveysIndexSchema.CreateAsync(SchemaBuilder);
        await _recipeMigrator.ExecuteAsync($"survey-templates{RecipesConstants.RecipeExtension}", this);

        return 1;
    }
}
