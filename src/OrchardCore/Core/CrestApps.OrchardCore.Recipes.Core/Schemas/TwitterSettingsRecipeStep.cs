using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public sealed class TwitterSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "TwitterSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
        [
            ("ConsumerKey", RecipeStepSchemaBuilders.String()),
            ("ConsumerSecret", RecipeStepSchemaBuilders.String()),
            ("AccessToken", RecipeStepSchemaBuilders.String()),
            ("AccessTokenSecret", RecipeStepSchemaBuilders.String()),
        ]);
}
