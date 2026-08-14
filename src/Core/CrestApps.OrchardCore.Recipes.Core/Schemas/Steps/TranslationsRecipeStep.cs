using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the translations recipe step.
/// </summary>
public sealed class TranslationsRecipeStep : IRecipeStep
{
    private readonly IRecipeSchemaExampleService _exampleService;

    private JsonSchema _cached;

    /// <summary>
    /// Gets the recipe step name.
    /// </summary>
    public string Name => "Translations";

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationsRecipeStep"/> class.
    /// </summary>
    /// <param name="exampleService">The service that supplies live tenant example values.</param>
    public TranslationsRecipeStep(IRecipeSchemaExampleService exampleService)
    {
        _exampleService = exampleService;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var examples = await _exampleService.GetExamplesAsync(cancellationToken);

        var translationEntrySchema = RecipeStepSchemaBuilders.Object(
                [
                    ("culture", RecipeStepSchemaBuilders.String()
                        .WithSuggestions(examples.CultureNames)
                        .Description("Culture name, for example 'en-US' or 'fr'.")),
                    ("context", RecipeStepSchemaBuilders.String().Description("Optional translation context used to disambiguate the key.")),
                    ("key", RecipeStepSchemaBuilders.String().Description("Localization key to translate.")),
                    ("value", RecipeStepSchemaBuilders.String().Description("Localized value stored for the key.")),
                ],
                ["culture", "key"])
            .Description("Single localization entry.");

        _cached = RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("translations", RecipeStepSchemaBuilders.Array(translationEntrySchema, 1).Description("Translation entries to import.")),
            ],
            ["translations"]);

        return _cached;
    }
}
