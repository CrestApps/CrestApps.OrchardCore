using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Lti.Deployments;

/// <summary>
/// Describes the External tools deployment step to CrestApps Recipes.
/// </summary>
internal sealed class LearningExternalToolsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LearningExternalToolsDeploymentStep);

    protected override string DisplayText => "External tools";

    protected override string Description => "Exports Learning's settings of every external tool (LTI 1.3). The tools' OpenID applications move with the OpenID steps.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
