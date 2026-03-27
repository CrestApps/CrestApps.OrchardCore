using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public sealed class DynamicDataTranslationsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "DynamicDataTranslations";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Translations", RecipeStepSchemaBuilders.Array(
                    RecipeStepSchemaBuilders.Object(
                        [
                            ("Name", RecipeStepSchemaBuilders.String()),
                            ("Translation", RecipeStepSchemaBuilders.Object(
                                [
                                    ("Context", RecipeStepSchemaBuilders.String()),
                                    ("Key", RecipeStepSchemaBuilders.String()),
                                    ("Value", RecipeStepSchemaBuilders.String()),
                                ],
                                ["Key"])),
                        ],
                        ["Name", "Translation"]),
                    1)),
            ],
            ["Translations"]);
}
