using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Deployments.Steps;

namespace CrestApps.OrchardCore.Telephony.Deployments.Sources;

/// <summary>
/// Exports every stored entry of the telephony extensions catalog, whole, so a member added to the record later travels without a
/// change here.
/// </summary>
internal sealed class TelephonyExtensionDeploymentSource : CatalogDeploymentSource<TelephonyExtensionDeploymentStep, TelephonyExtension>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the telephony extensions.</param>
    public TelephonyExtensionDeploymentSource(ITelephonyExtensionManager manager)
        : base("Extensions", manager)
    {
    }
}
