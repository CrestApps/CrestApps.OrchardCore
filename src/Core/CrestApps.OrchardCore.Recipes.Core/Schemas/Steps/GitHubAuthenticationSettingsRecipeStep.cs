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
                ("ConsumerSecret", RecipeStepSchemaBuilders.String().Description("GitHub OAuth application client secret. The step stores the value as given, and Orchard Core decrypts it with the tenant's Data Protection keys, so a clear value does not work. Prefer a secret: create it with the 'Secrets' step and reference it with 'ClientSecretSecretName' in the 'GitHubAuthenticationSettings' property of the 'Settings' step.")),
                ("CallbackPath", RecipeStepSchemaBuilders.String().Description("Relative callback path that GitHub redirects back to after sign-in.")),
            ]);
}
