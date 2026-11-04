using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>Cadence</c> recipe step.
/// </summary>
internal sealed class CadenceDeploymentSource : CatalogDeploymentSource<CadenceDeploymentStep, Cadence>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the cadences.</param>
    public CadenceDeploymentSource(ICatalogManager<Cadence> manager)
        : base("Cadences", manager)
    {
    }
}
