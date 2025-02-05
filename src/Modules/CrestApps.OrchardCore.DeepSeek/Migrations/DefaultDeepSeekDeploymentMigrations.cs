using OrchardCore.Data.Migration;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.DeepSeek.Migrations;

internal sealed class DefaultDeepSeekDeploymentMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;

    public DefaultDeepSeekDeploymentMigrations(IRecipeMigrator recipeMigrator)
    {
        _recipeMigrator = recipeMigrator;
    }

    public async Task<int> CreateAsync()
    {
        await _recipeMigrator.ExecuteAsync($"deepseek-cloud-default-deployments{RecipesConstants.RecipeExtension}", this);

        return 1;
    }
}
