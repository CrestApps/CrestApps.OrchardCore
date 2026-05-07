using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the facebook login settings recipe step.
/// </summary>
public sealed class FacebookLoginSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "FacebookLoginSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("CallbackPath", RecipeStepSchemaBuilders.String()),
            ]);
}
