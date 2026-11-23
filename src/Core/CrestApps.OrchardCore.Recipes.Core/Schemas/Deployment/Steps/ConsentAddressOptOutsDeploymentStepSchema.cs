using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the schema of the <c>ConsentAddressOptOutsDeploymentStep</c> of the E-mail Preferences feature.
/// </summary>
public sealed class ConsentAddressOptOutsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "ConsentAddressOptOutsDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Address opt-outs";

    /// <inheritdoc />
    protected override string Description => "Exports every opt-out for one purpose (such as marketing) recorded against an address, as a 'ConsentAddressOptOuts' recipe step. The opt-outs hold e-mail addresses and phone numbers, which are personal data.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
