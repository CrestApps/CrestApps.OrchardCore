using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the dynamic data translations recipe step.
/// </summary>
public sealed class DynamicDataTranslationsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "DynamicDataTranslations";

    protected override JsonSchema CreateSchema()
    {
        var translationSchema = RecipeStepSchemaBuilders.Object(
                [
                    ("Context", RecipeStepSchemaBuilders.String().Description("Optional localization context used to disambiguate the translation key.")),
                    ("Key", RecipeStepSchemaBuilders.String().Description("Translation key to replace or add.")),
                    ("Value", RecipeStepSchemaBuilders.String().Description("Localized value to store for the key.")),
                ],
                ["Key"])
            .Description("Translation entry payload.");

        var translationEntrySchema = RecipeStepSchemaBuilders.Object(
                [
                    ("Name", RecipeStepSchemaBuilders.String().Description("Logical translation set name.")),
                    ("Translation", translationSchema),
                ],
                ["Name", "Translation"])
            .Description("A named dynamic-data translation entry.");

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Translations", RecipeStepSchemaBuilders.Array(translationEntrySchema, 1).Description("Dynamic data translation entries to import.")),
            ],
            ["Translations"]);
    }
}
