namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Defines feature-owned work that must stop accepting new operations and drain across Orchard feature reloads.
/// </summary>
public interface IContactCenterFeatureLifecycleParticipant
{
    /// <summary>
    /// Gets the Orchard feature that owns the participant.
    /// </summary>
    string FeatureId { get; }

    /// <summary>
    /// Stops the feature component from accepting new work before the owning feature is disabled.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task QuiesceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for admitted work to finish before the owning feature is disabled.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DrainAsync(CancellationToken cancellationToken = default);
}
