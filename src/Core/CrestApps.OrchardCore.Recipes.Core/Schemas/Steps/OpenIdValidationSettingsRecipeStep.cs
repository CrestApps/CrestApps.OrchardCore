using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the open id validation settings recipe step.
/// </summary>
public sealed class OpenIdValidationSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "OpenIdValidationSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("MetadataAddress", RecipeStepSchemaBuilders.String().Description("Absolute URL of the OpenID Connect metadata document.")),
                ("Audience", RecipeStepSchemaBuilders.String().Description("Expected token audience for validation.")),
                ("Authority", RecipeStepSchemaBuilders.String().Description("Authority or issuer base URL used for token validation.")),
                ("DisableTokenTypeValidation", RecipeStepSchemaBuilders.Boolean().Description("Whether token type checks should be skipped during validation.")),
                ("Tenant", RecipeStepSchemaBuilders.String().Description("Optional tenant or issuer hint used by the validation handler.")),
            ]);
}
