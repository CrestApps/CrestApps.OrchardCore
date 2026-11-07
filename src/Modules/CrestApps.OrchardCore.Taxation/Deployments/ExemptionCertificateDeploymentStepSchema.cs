using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Deployments;

/// <summary>
/// Describes the exemption certificates deployment step to CrestApps Recipes.
/// </summary>
internal sealed class ExemptionCertificateDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(ExemptionCertificateDeploymentStep);

    protected override string DisplayText => "Exemption certificates";

    protected override string Description => "Exports customers' tax exemption certificates with their types, jurisdictions and dates.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
