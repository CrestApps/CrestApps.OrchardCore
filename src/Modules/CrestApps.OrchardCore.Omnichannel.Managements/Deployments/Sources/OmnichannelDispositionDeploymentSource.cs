using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>Disposition</c> recipe step.
/// </summary>
internal sealed class OmnichannelDispositionDeploymentSource : CatalogDeploymentSource<OmnichannelDispositionDeploymentStep, OmnichannelDisposition>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelDispositionDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the dispositions.</param>
    public OmnichannelDispositionDeploymentSource(INamedCatalogManager<OmnichannelDisposition> manager)
        : base("Dispositions", manager)
    {
    }
}
