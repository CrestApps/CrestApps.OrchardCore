using CrestApps.OrchardCore.Asterisk.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Centralizes the ownership validation that every Asterisk ARI operation path must perform before it
/// sends a request that names a Stasis application, ensuring a single tenant owns each (BaseUrl,
/// ApplicationName) pair on this node. The gate combines the host-default collision check with an atomic
/// ownership claim reference-counted by this shell generation's token, so the listener, the ARI client,
/// the media provider, and the generic telephony provider all enforce the same rule and fail closed when
/// another tenant already owns the application.
/// </summary>
internal interface IAsteriskAriApplicationGate
{
    /// <summary>
    /// Confirms that the current tenant may operate the resolved ARI application, claiming ownership for
    /// this shell generation if it is not already owned by another tenant. Returns <see langword="true"/>
    /// when the caller may proceed (the pair is unconfigured, or now owned by this tenant); returns
    /// <see langword="false"/> when the application collides with the host default connection on a
    /// non-default shell or is already owned by a different tenant, in which case the caller must fail
    /// closed without performing any ARI side effect.
    /// </summary>
    /// <param name="settings">The resolved Asterisk settings whose application ownership is being acquired.</param>
    bool TryAcquire(AsteriskResolvedSettings settings);

    /// <summary>
    /// Performs a read-only availability observation for the resolved ARI application without claiming
    /// ownership. Returns <see langword="false"/> when the application collides with the host default
    /// connection on a non-default shell or is already owned by a different tenant; otherwise returns
    /// <see langword="true"/>. This is advisory only: authoritative enforcement happens through
    /// <see cref="TryAcquire"/> on every ARI operation path.
    /// </summary>
    /// <param name="settings">The resolved Asterisk settings whose availability is being observed.</param>
    bool IsAvailable(AsteriskResolvedSettings settings);

    /// <summary>
    /// Releases every ownership claim held by this shell generation. Called when the tenant is terminating
    /// so another tenant may reclaim the application after a reconfiguration, without disturbing a newer
    /// generation of the same tenant that is still running.
    /// </summary>
    void ReleaseGeneration();
}
