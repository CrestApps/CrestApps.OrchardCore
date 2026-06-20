using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the translations recipe step.
/// </summary>
public sealed class TranslationsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "Translations";

    protected override JsonSchema CreateSchema()
    {
        var translationEntrySchema = RecipeStepSchemaBuilders.Object(
                [
                    ("culture", RecipeStepSchemaBuilders.String().Description("Culture name, for example 'en-US' or 'fr'.")),
                    ("context", RecipeStepSchemaBuilders.String().Description("Optional translation context used to disambiguate the key.")),
                    ("key", RecipeStepSchemaBuilders.String().Description("Localization key to translate.")),
                    ("value", RecipeStepSchemaBuilders.String().Description("Localized value stored for the key.")),
                ],
                ["culture", "key"])
            .Description("Single localization entry.");

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("translations", RecipeStepSchemaBuilders.Array(translationEntrySchema, 1).Description("Translation entries to import.")),
            ],
            ["translations"]);
    }
}
