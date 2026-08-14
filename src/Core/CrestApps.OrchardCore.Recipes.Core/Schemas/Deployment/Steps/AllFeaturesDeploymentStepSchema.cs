using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>AllFeaturesDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class AllFeaturesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "AllFeaturesDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "All features";

    /// <inheritdoc />
    protected override string Description => "Exports the list of enabled features.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IgnoreDisabledFeatures", DeploymentSchemaBuilders.Boolean("When true, the exported recipe only enables features and does not disable any that are currently off."));
    }
}
