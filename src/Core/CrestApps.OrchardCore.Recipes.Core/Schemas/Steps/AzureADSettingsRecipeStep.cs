using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the azure a d settings recipe step.
/// </summary>
public sealed class AzureADSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "AzureADSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("DisplayName", RecipeStepSchemaBuilders.String().Description("Display caption shown for the Azure AD login option.")),
                ("AppId", RecipeStepSchemaBuilders.String().Description("Azure AD application (client) ID.")),
                ("TenantId", RecipeStepSchemaBuilders.String().Description("Azure AD tenant identifier that owns the application.")),
                ("CallbackPath", RecipeStepSchemaBuilders.String().Description("Relative callback path that Azure AD redirects back to after sign-in.")),
            ]);
}
