using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the twitter settings recipe step.
/// </summary>
public sealed class TwitterSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "TwitterSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("ConsumerKey", RecipeStepSchemaBuilders.String().Description("Twitter/X API consumer key.")),
                ("ConsumerSecret", RecipeStepSchemaBuilders.String().Description("Twitter/X API consumer secret. The step stores the value as given, and Orchard Core decrypts it with the tenant's Data Protection keys, so a clear value does not work. Prefer a secret: create it with the 'Secrets' step and reference it with 'ConsumerSecretSecretName' in the 'TwitterSettings' property of the 'Settings' step.")),
                ("AccessToken", RecipeStepSchemaBuilders.String().Description("Twitter/X API access token used for authenticated requests.")),
                ("AccessTokenSecret", RecipeStepSchemaBuilders.String().Description("Twitter/X API access token secret. The step stores the value as given, and Orchard Core decrypts it with the tenant's Data Protection keys, so a clear value does not work. Prefer a secret: create it with the 'Secrets' step and reference it with 'AccessTokenSecretSecretName' in the 'TwitterSettings' property of the 'Settings' step.")),
            ]);
}
