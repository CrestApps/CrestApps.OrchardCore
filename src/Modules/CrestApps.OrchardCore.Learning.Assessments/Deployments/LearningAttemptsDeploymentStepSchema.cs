using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Assessments.Deployments;

internal sealed class LearningAttemptsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LearningAttemptsDeploymentStep);

    protected override string DisplayText => "Learning quiz attempts (learner records)";

    protected override string Description => "Exports quiz attempts, quiz overrides and quiz access codes (as hashes), naming people by user name. Needs the Export learner records permission.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("QuizId", DeploymentSchemaBuilders.String("The content item id of the quiz to export. Empty for every quiz."));
        yield return ("IncludeOverrides", DeploymentSchemaBuilders.Boolean("When true, exports the quiz overrides too."));
    }
}
