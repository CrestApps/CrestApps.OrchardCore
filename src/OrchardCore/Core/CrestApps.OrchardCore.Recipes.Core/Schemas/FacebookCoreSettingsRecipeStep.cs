using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public sealed class FacebookCoreSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "FacebookCoreSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
        [
            ("AppId", RecipeStepSchemaBuilders.String()),
            ("AppSecret", RecipeStepSchemaBuilders.String()),
            ("SdkJs", RecipeStepSchemaBuilders.String()),
            ("FBInit", RecipeStepSchemaBuilders.Boolean()),
            ("FBInitParams", RecipeStepSchemaBuilders.String()),
            ("Version", RecipeStepSchemaBuilders.String()),
        ]);
}
