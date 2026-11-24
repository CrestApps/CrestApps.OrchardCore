using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Assessments.Deployments;

internal sealed class LearningGradeScalesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LearningGradeScalesDeploymentStep);

    protected override string DisplayText => "Learning grade scales";

    protected override string Description => "Exports every grade scale with its bands.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
