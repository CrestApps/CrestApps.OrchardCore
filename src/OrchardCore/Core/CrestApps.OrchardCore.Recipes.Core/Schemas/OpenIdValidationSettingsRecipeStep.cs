using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

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
