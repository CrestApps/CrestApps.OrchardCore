using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the schema of the <c>DataPipelinesDeploymentStep</c> of the Orchard Core Data Pipelines module.
/// </summary>
public sealed class DataPipelinesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "DataPipelinesDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Data Pipelines";

    /// <inheritdoc />
    protected override string Description => "Exports the data pipelines with their published definition, or their draft when they were never published. The secrets of the steps are not exported.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("data pipelines, including the ones created after the step was added to the plan,"));
        yield return ("PipelineIds", DeploymentSchemaBuilders.StringArray("The identifiers of the data pipelines to export when 'IncludeAll' is false."));
    }
}
