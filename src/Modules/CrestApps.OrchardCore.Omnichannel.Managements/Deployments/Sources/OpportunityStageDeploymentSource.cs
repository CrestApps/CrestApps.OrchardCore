using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>OpportunityStage</c> recipe step.
/// </summary>
internal sealed class OpportunityStageDeploymentSource : CatalogDeploymentSource<OpportunityStageDeploymentStep, OpportunityStage>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityStageDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the opportunity stages.</param>
    public OpportunityStageDeploymentSource(INamedCatalogManager<OpportunityStage> manager)
        : base("OpportunityStages", manager)
    {
    }
}
