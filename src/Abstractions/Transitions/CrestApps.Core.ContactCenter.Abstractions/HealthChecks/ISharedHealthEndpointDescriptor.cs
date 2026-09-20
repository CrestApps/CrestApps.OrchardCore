namespace CrestApps.Core.ContactCenter.HealthChecks;

/// <summary>
/// Describes the host's own shared aggregate health endpoint, so the Contact Center can tell whether a
/// liveness probe has been pointed at one.
/// </summary>
/// <remarks>
/// Which configuration key names that endpoint, what route it falls back to, and where an operator records
/// that they accept the hazard are all the host's knowledge. This is how a host hands over the two answers the
/// check needs, without the check reaching into a container to find them.
/// </remarks>
public interface ISharedHealthEndpointDescriptor
{
    /// <summary>
    /// Gets the route the host's shared health endpoint is served on, or <see langword="null"/> when the host
    /// has none.
    /// </summary>
    string Route { get; }

    /// <summary>
    /// Gets a value indicating whether an operator has accepted the hazard of a liveness probe pointing at
    /// that endpoint.
    /// </summary>
    bool IsAcknowledged { get; }
}
