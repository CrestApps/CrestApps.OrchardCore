using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>SubjectAction</c> recipe step.
/// </summary>
internal sealed class OmnichannelSubjectActionDeploymentSource : CatalogDeploymentSource<OmnichannelSubjectActionDeploymentStep, SubjectAction>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectActionDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the subject actions.</param>
    public OmnichannelSubjectActionDeploymentSource(ISourceCatalogManager<SubjectAction> manager)
        : base("SubjectActions", manager)
    {
    }
}
