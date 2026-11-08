using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the facebook core settings recipe step.
/// </summary>
public sealed class FacebookCoreSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "FacebookCoreSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("AppId", RecipeStepSchemaBuilders.String().Description("Facebook application ID.")),
                ("AppSecret", RecipeStepSchemaBuilders.String().Description("Facebook application secret. The step stores the value as given, and Orchard Core decrypts it with the tenant's Data Protection keys, so a clear value does not work. Prefer a secret: create it with the 'Secrets' step and reference it with 'AppSecretSecretName' in the 'FacebookSettings' property of the 'Settings' step.")),
                ("SdkJs", RecipeStepSchemaBuilders.String().Description("Optional URL override for the Facebook JavaScript SDK.")),
                ("FBInit", RecipeStepSchemaBuilders.Boolean().Description("Whether Orchard should emit the FB.init JavaScript bootstrap call.")),
                ("FBInitParams", RecipeStepSchemaBuilders.String().Description("JSON object serialized as the arguments for FB.init.")),
                ("Version", RecipeStepSchemaBuilders.String().Description("Facebook Graph API version to target, for example 'v20.0'.")),
            ]);
}
