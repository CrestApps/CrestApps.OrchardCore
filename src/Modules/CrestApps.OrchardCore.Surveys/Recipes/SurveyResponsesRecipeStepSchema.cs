using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Surveys.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Describes the SurveyResponses recipe step.
/// </summary>
internal sealed class SurveyResponsesRecipeStepSchema : RecipeStepSchemaBase
{
    /// <inheritdoc />
    public override string Name => SurveysRecipeSteps.Responses;

    /// <inheritdoc />
    protected override JsonSchema CreateSchema()
    {
        var response = SurveyModelSchemaBuilder.Build(typeof(SurveyResponse), [], nameof(SurveyResponse.UserId), nameof(SurveyResponse.GradedBy), nameof(SurveyResponse.SurveyId), nameof(SurveyResponse.SurveyVersionId), nameof(SurveyResponse.ResumeTokenHash))
            .Properties(
                ("SurveyName", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The technical name of the survey.")),
                ("VersionNumber", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("The version the response is on; the site must have it.")),
                ("UserName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user name of an identified respondent; the user must be on the site. Left out for anonymous responses.")),
                ("GradedByUserName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user name of the grader.")))
            .Required("SurveyName", "VersionNumber");

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [(SurveysRecipeSteps.ResponsesSection, RecipeStepSchemaBuilders.Array(response).Description("The responses, matched by id."))],
            [SurveysRecipeSteps.ResponsesSection]);
    }
}
