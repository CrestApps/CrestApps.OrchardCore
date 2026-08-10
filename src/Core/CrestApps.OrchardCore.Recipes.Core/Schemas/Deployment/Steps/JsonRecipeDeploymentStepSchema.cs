using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>JsonRecipeDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class JsonRecipeDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "JsonRecipeDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "JSON recipe";

    /// <inheritdoc />
    protected override string Description => "Adds the supplied raw JSON recipe steps to the deployment package.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("Json", DeploymentSchemaBuilders.String("The raw JSON, containing one or more recipe steps, added to the deployment package."));
    }
}
