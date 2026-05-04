using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the git hub authentication settings recipe step.
/// </summary>
public sealed class GitHubAuthenticationSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "GitHubAuthenticationSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
    [
        ("ConsumerKey", RecipeStepSchemaBuilders.String()),
        ("ConsumerSecret", RecipeStepSchemaBuilders.String()),
        ("CallbackPath", RecipeStepSchemaBuilders.String()),
        ]);
}
