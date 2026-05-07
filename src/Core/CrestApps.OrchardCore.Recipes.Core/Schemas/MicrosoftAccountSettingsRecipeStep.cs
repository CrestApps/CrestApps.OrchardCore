using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the microsoft account settings recipe step.
/// </summary>
public sealed class MicrosoftAccountSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "MicrosoftAccountSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("AppId", RecipeStepSchemaBuilders.String()),
                ("AppSecret", RecipeStepSchemaBuilders.String()),
                ("CallbackPath", RecipeStepSchemaBuilders.String()),
            ]);
}
