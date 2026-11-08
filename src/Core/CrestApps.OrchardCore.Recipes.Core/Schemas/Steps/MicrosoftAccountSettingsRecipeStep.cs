using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the microsoft account settings recipe step.
/// </summary>
public sealed class MicrosoftAccountSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "MicrosoftAccountSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("AppId", RecipeStepSchemaBuilders.String().Description("Microsoft application (client) ID.")),
                ("AppSecret", RecipeStepSchemaBuilders.String().Description("Microsoft application client secret. The step stores the value as given, and Orchard Core decrypts it with the tenant's Data Protection keys, so a clear value does not work. Prefer a secret: create it with the 'Secrets' step and reference it with 'AppSecretSecretName' in the 'MicrosoftAccountSettings' property of the 'Settings' step.")),
                ("CallbackPath", RecipeStepSchemaBuilders.String().Description("Relative callback path that Microsoft redirects back to after sign-in.")),
            ]);
}
