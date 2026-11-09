using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.DncRegistry.Deployments;

/// <summary>
/// Describes the Local DNC lists deployment step to CrestApps Recipes.
/// </summary>
internal sealed class LocalDncListsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LocalDncListsDeploymentStep);

    protected override string DisplayText => "Local DNC lists";

    protected override string Description => "Exports every imported local do-not-call list with its phone numbers. Phone numbers are personal data.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
