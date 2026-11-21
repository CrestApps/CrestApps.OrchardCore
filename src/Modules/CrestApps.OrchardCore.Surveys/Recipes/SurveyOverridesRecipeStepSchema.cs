using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Surveys.Core.Schemas;
using CrestApps.OrchardCore.Surveys.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Describes the SurveyOverrides recipe step.
/// </summary>
internal sealed class SurveyOverridesRecipeStepSchema : RecipeStepSchemaBase
{
    /// <inheritdoc />
    public override string Name => SurveysRecipeSteps.Overrides;

    /// <inheritdoc />
    protected override JsonSchema CreateSchema()
    {
        var entry = SurveyModelSchemaBuilder.Build(typeof(SurveyOverride), [], nameof(SurveyOverride.UserId), nameof(SurveyOverride.Actor), nameof(SurveyOverride.SurveyId))
            .Properties(
                ("SurveyName", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The technical name of the survey.")),
                ("UserName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user name of the respondent; the user must be on the site.")),
                ("ActorUserName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user name of who granted it.")))
            .Required("SurveyName");

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [(SurveysRecipeSteps.OverridesSection, RecipeStepSchemaBuilders.Array(entry).Description("The accommodations, matched by id."))],
            [SurveysRecipeSteps.OverridesSection]);
    }
}
