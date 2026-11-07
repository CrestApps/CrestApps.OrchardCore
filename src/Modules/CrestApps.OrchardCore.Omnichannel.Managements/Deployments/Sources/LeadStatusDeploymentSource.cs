using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>LeadStatus</c> recipe step.
/// </summary>
internal sealed class LeadStatusDeploymentSource : CatalogDeploymentSource<LeadStatusDeploymentStep, LeadStatus>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the lead statuses.</param>
    public LeadStatusDeploymentSource(INamedCatalogManager<LeadStatus> manager)
        : base("LeadStatuses", manager)
    {
    }
}
