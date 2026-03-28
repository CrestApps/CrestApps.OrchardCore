using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

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
