using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Schema for the "AI Personas" deployment step, which exports saved AI personas as an "AIPersonas" recipe step.
/// </summary>
public sealed class AIPersonaDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => "AIPersonaDeploymentStep";

    protected override string DisplayText => "AI Personas";

    protected override string Description => "Exports all or the selected saved AI personas. Persona files ship with the code and are not exported.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("saved AI personas"));
        yield return ("PersonaIds", DeploymentSchemaBuilders.StringArray("The item ids of the saved AI personas to export when IncludeAll is false."));
    }
}
