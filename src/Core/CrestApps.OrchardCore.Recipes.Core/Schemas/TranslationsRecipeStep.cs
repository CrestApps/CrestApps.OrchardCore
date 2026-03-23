using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public sealed class TranslationsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "Translations";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("translations", RecipeStepSchemaBuilders.Array(
                    RecipeStepSchemaBuilders.Object(
                        [
                            ("culture", RecipeStepSchemaBuilders.String()),
                            ("context", RecipeStepSchemaBuilders.String()),
                            ("key", RecipeStepSchemaBuilders.String()),
                            ("value", RecipeStepSchemaBuilders.String()),
                        ],
                        ["culture", "key"]),
                    1)),
            ],
            ["translations"]);
}
