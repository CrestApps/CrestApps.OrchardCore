using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the azure a d settings recipe step.
/// </summary>
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
