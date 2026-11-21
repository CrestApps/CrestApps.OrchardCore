using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Deployments;

/// <summary>
/// Exports every external tool's settings under <c>Tools</c>, its subject salt included, so the ids each tool knows
/// people by stay the same on the importing site.
/// </summary>
internal sealed class LearningExternalToolsDeploymentSource : CatalogDeploymentSource<LearningExternalToolsDeploymentStep, ExternalTool>
{
    public LearningExternalToolsDeploymentSource(IExternalToolManager manager)
        : base("Tools", manager)
    {
    }
}
