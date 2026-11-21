using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Surveys.Core.Schemas;
using CrestApps.OrchardCore.Surveys.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Describes the Surveys recipe step.
/// </summary>
internal sealed class SurveysRecipeStepSchema : IRecipeStep
{
    private readonly SurveyDefinitionSchemaBuilder _definitions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysRecipeStepSchema"/> class.
    /// </summary>
    /// <param name="definitions">The definition schema builder.</param>
    public SurveysRecipeStepSchema(SurveyDefinitionSchemaBuilder definitions)
    {
        _definitions = definitions;
    }

    /// <inheritdoc />
    public string Name => SurveysRecipeSteps.Surveys;

    /// <inheritdoc />
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var survey = SurveyModelSchemaBuilder.Build(
                typeof(Survey),
                [],
                nameof(Survey.AnonymitySecret),
                nameof(Survey.PublishedVersionId),
                nameof(Survey.CreatedUtc),
                nameof(Survey.ModifiedUtc),
                nameof(Survey.OwnerId),
                nameof(Survey.Author),
                nameof(Survey.Draft))
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A 26-character unique id; omit it to make one. Never a readable name.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The unique technical name: lowercase letters, digits and dashes. A survey on the site with the same name is updated.")),
                ("Draft", _definitions.BuildDefinition()),
                ("PublishedVersionNumber", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("The number of the published version, set once every version exists; 0 before the first publish.")),
                ("Versions", RecipeStepSchemaBuilders.Array(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A 26-character unique id; omit it to make one.")),
                        ("Number", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("The version number. A number the survey already has is skipped: versions never change.")),
                        ("Definition", _definitions.BuildDefinition()),
                        ("PublishedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.DateTime).Description("When it was published, in UTC.")),
                        ("PublishedByUserName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user name of who published it.")),
                        ("Notes", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The publish notes.")),
                        ("ContentHash", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The SHA-256 of the canonical definition.")))
                    .Required("Number")
                    .AdditionalProperties(true)).Description("The published versions.")),
                ("Collectors", RecipeStepSchemaBuilders.Array(SurveyModelSchemaBuilder.Build(typeof(SurveyCollector), [], nameof(SurveyCollector.SurveyId), nameof(SurveyCollector.CreatedUtc), nameof(SurveyCollector.ModifiedUtc), nameof(SurveyCollector.OwnerId), nameof(SurveyCollector.Author))
                    .Description("A link or other collector. Token and PasswordHash are exported only with secrets; without them a new token is made."))
                    .Description("The collectors.")))
            .Required("Name");

        return ValueTask.FromResult(RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [(SurveysRecipeSteps.SurveysSection, RecipeStepSchemaBuilders.Array(survey).Description("The surveys to create or update, matched by id, then by name."))],
            [SurveysRecipeSteps.SurveysSection]));
    }
}
