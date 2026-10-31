using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Skills.Deployments;

/// <summary>
/// Describes the Skills deployment step's settings to CrestApps Recipes.
/// </summary>
internal sealed class SkillsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(SkillsDeploymentStep);

    protected override string DisplayText => "Skills and certifications";

    protected override string Description => "Exports every skill and certification, and optionally the skills and certificates people hold.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAssignments", DeploymentSchemaBuilders.Boolean("When true, exports the skills people hold too, with the skill name so they import on another site."));
        yield return ("IncludeCertificationRecords", DeploymentSchemaBuilders.Boolean("When true, exports the certificates people hold too."));
    }
}
