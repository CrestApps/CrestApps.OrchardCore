using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Surveys.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Describes the SurveyInvitations recipe step.
/// </summary>
internal sealed class SurveyInvitationsRecipeStepSchema : RecipeStepSchemaBase
{
    /// <inheritdoc />
    public override string Name => SurveysRecipeSteps.Invitations;

    /// <inheritdoc />
    protected override JsonSchema CreateSchema()
    {
        var invitation = SurveyModelSchemaBuilder.Build(typeof(SurveyInvitation), [], nameof(SurveyInvitation.UserId), nameof(SurveyInvitation.SurveyId))
            .Properties(
                ("SurveyName", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The technical name of the survey.")),
                ("UserName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user name of the person invited; the user must be on the site.")),
                ("TokenHash", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The SHA-256 of the personal link's token; the token itself never travels.")))
            .Required("SurveyName");

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [(SurveysRecipeSteps.InvitationsSection, RecipeStepSchemaBuilders.Array(invitation).Description("The invitations, matched by id."))],
            [SurveysRecipeSteps.InvitationsSection]);
    }
}
