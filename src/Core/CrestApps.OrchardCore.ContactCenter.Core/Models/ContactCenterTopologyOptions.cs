namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The operator-declared deployment topology for this tenant.
/// </summary>
/// <remarks>
/// Bound from the <c>CrestApps_ContactCenter:Topology</c> configuration section.
/// </remarks>
public sealed class ContactCenterTopologyOptions
{
    /// <summary>
    /// Gets or sets the identifier of the declared topology profile.
    /// </summary>
    /// <remarks>
    /// Must match a profile in <see cref="ContactCenterTopologyProfiles"/>. When left unset the deployment is
    /// treated as undeclared, which is tolerated outside a production host environment and rejected inside one:
    /// a production deployment that never states which topology it is running cannot be validated against any
    /// contract, so it must not be reported as ready.
    /// </remarks>
    public string ProfileId { get; set; }
}
