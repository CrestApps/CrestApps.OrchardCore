using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Surveys.Core.Schemas;
using CrestApps.OrchardCore.Surveys.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Describes the SurveyQuestionBanks recipe step.
/// </summary>
internal sealed class SurveyQuestionBanksRecipeStepSchema : IRecipeStep
{
    private readonly SurveyDefinitionSchemaBuilder _definitions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyQuestionBanksRecipeStepSchema"/> class.
    /// </summary>
    /// <param name="definitions">The definition schema builder.</param>
    public SurveyQuestionBanksRecipeStepSchema(SurveyDefinitionSchemaBuilder definitions)
    {
        _definitions = definitions;
    }

    /// <inheritdoc />
    public string Name => SurveysRecipeSteps.QuestionBanks;

    /// <inheritdoc />
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var question = SurveyModelSchemaBuilder.Build(typeof(BankQuestion), [], nameof(BankQuestion.BankId), nameof(BankQuestion.Question), nameof(BankQuestion.CreatedUtc), nameof(BankQuestion.ModifiedUtc), nameof(BankQuestion.OwnerId), nameof(BankQuestion.Author))
            .Properties(("Question", _definitions.BuildQuestion()))
            .Description("A question of the bank, matched by id, then by SourceIdentifier.");

        var bank = SurveyModelSchemaBuilder.Build(typeof(QuestionBank), [], nameof(QuestionBank.CreatedUtc), nameof(QuestionBank.ModifiedUtc), nameof(QuestionBank.OwnerId), nameof(QuestionBank.Author))
            .Properties(
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The unique technical name.")),
                ("Questions", RecipeStepSchemaBuilders.Array(question).Description("The questions.")))
            .Required("Name");

        return ValueTask.FromResult(RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [(SurveysRecipeSteps.QuestionBanksSection, RecipeStepSchemaBuilders.Array(bank).Description("The question banks to create or update, matched by id, then by name."))],
            [SurveysRecipeSteps.QuestionBanksSection]));
    }
}
