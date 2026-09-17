namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Coordinates tenant-local capability work admission and bounded draining during Orchard shell replacement.
/// </summary>
public interface IContactCenterFeatureWorkManager
{
    /// <summary>
    /// Attempts to admit work owned by the specified capability.
    /// </summary>
    /// <param name="capability">The capability that owns the work. See <see cref="ContactCenterCapabilities"/>.</param>
    /// <returns>A lease that must be disposed when the work finishes, or <see langword="null"/> when the capability is quiescing.</returns>
    IContactCenterFeatureWorkLease TryEnter(string capability);

    /// <summary>
    /// Stops admitting new work for the specified capability.
    /// </summary>
    /// <param name="capability">The capability that owns the work. See <see cref="ContactCenterCapabilities"/>.</param>
    void Quiesce(string capability);

    /// <summary>
    /// Waits for admitted work to finish.
    /// </summary>
    /// <param name="capability">The capability that owns the work. See <see cref="ContactCenterCapabilities"/>.</param>
    /// <param name="timeout">The maximum amount of time to wait.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DrainAsync(string capability, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens work admission for the specified capability after a failed disable or fresh-shell reconciliation.
    /// </summary>
    /// <param name="capability">The capability that owns the work. See <see cref="ContactCenterCapabilities"/>.</param>
    void Activate(string capability);

    /// <summary>
    /// Determines whether work admission is currently closed for the specified capability. Maintenance procedures
    /// that must not race against live traffic use this to prove the tenant is quiesced before they run.
    /// </summary>
    /// <param name="capability">The capability that owns the work. See <see cref="ContactCenterCapabilities"/>.</param>
    /// <returns><see langword="true"/> when the capability is quiescing; otherwise, <see langword="false"/>.</returns>
    bool IsQuiescing(string capability);
}
