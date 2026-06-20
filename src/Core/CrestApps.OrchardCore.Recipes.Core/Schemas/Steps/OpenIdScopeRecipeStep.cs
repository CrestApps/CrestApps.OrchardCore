using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the open id scope recipe step.
/// </summary>
public sealed class OpenIdScopeRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "OpenIdScope";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("Description", RecipeStepSchemaBuilders.String().Description("Human-readable explanation of what this scope grants.")),
                ("DisplayName", RecipeStepSchemaBuilders.String().Description("Display caption shown for the scope.")),
                ("ScopeName", RecipeStepSchemaBuilders.String().Description("Unique scope name exposed by the OpenID server.")),
                ("Resources", RecipeStepSchemaBuilders.String().Description("Space-separated resource names associated with this scope.")),
            ]);
}
