using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Reports.Designer.Deployments;

/// <summary>
/// Describes the designed reports deployment step's settings to CrestApps Recipes.
/// </summary>
internal sealed class ReportDesignsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(ReportDesignsDeploymentStep);

    protected override string DisplayText => "Designed Reports and Views";

    protected override string Description => "Exports designed reports, the views they read and their report groups. Share links are never exported.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("reports, views and report groups"));
        yield return ("ReportIds", DeploymentSchemaBuilders.StringArray("The itemIds of the reports to export when IncludeAll is false. Their groups and every view they read are exported too."));
        yield return ("ViewIds", DeploymentSchemaBuilders.StringArray("The itemIds of extra views to export when IncludeAll is false. The views they read are exported too."));
    }
}
