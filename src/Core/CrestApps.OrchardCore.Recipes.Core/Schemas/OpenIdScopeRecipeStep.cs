using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the open id scope recipe step.
/// </summary>
public sealed class OpenIdScopeRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "OpenIdScope";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
    [
        ("Description", RecipeStepSchemaBuilders.String()),
        ("DisplayName", RecipeStepSchemaBuilders.String()),
        ("ScopeName", RecipeStepSchemaBuilders.String()),
        ("Resources", RecipeStepSchemaBuilders.String()),
        ]);
}
