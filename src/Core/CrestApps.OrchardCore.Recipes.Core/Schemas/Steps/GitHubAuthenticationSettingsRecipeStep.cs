using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the git hub authentication settings recipe step.
/// </summary>
public sealed class GitHubAuthenticationSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "GitHubAuthenticationSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("ConsumerKey", RecipeStepSchemaBuilders.String().Description("GitHub OAuth application client ID.")),
                ("ConsumerSecret", RecipeStepSchemaBuilders.String().Description("GitHub OAuth application client secret.")),
                ("CallbackPath", RecipeStepSchemaBuilders.String().Description("Relative callback path that GitHub redirects back to after sign-in.")),
            ]);
}
