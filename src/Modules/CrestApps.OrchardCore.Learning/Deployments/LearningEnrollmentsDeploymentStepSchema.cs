using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Deployments;

/// <summary>
/// Describes the enrollments deployment step to CrestApps Recipes.
/// </summary>
internal sealed class LearningEnrollmentsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LearningEnrollmentsDeploymentStep);

    protected override string DisplayText => "Learning enrollments (learner records)";

    protected override string Description => "Exports enrollments with their progress, naming people by user name. Needs the Export learner records permission.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("CourseId", DeploymentSchemaBuilders.String("The content item id of the course to export. Empty for every course."));
        yield return ("FromUtc", DeploymentSchemaBuilders.String("The earliest enrollment date to export, an ISO 8601 UTC time."));
        yield return ("ToUtc", DeploymentSchemaBuilders.String("The latest enrollment date to export, an ISO 8601 UTC time."));
        yield return ("IncludeProgress", DeploymentSchemaBuilders.Boolean("When true, exports the progress on each course item too."));
    }
}
