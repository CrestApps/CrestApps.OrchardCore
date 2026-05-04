using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the open id validation settings recipe step.
/// </summary>
public sealed class OpenIdValidationSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "OpenIdValidationSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
    [
        ("MetadataAddress", RecipeStepSchemaBuilders.String()),
        ("Audience", RecipeStepSchemaBuilders.String()),
        ("Authority", RecipeStepSchemaBuilders.String()),
        ("DisableTokenTypeValidation", RecipeStepSchemaBuilders.Boolean()),
        ("Tenant", RecipeStepSchemaBuilders.String()),
        ]);
}
