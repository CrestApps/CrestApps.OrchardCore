using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public sealed class AzureADSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "AzureADSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
        [
            ("DisplayName", RecipeStepSchemaBuilders.String()),
            ("AppId", RecipeStepSchemaBuilders.String()),
            ("TenantId", RecipeStepSchemaBuilders.String()),
            ("CallbackPath", RecipeStepSchemaBuilders.String()),
        ]);
}
