namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Coordinates tenant-local feature work admission and bounded draining during Orchard shell replacement.
/// </summary>
public interface IContactCenterFeatureWorkManager
{
    /// <summary>
    /// Attempts to admit work owned by the specified feature.
    /// </summary>
    /// <param name="featureId">The owning Orchard feature identifier.</param>
    /// <returns>A lease that must be disposed when the work finishes, or <see langword="null"/> when the feature is quiescing.</returns>
    IContactCenterFeatureWorkLease TryEnter(string featureId);

    /// <summary>
    /// Stops admitting new work for the specified feature.
    /// </summary>
    /// <param name="featureId">The owning Orchard feature identifier.</param>
    void Quiesce(string featureId);

    /// <summary>
    /// Waits for admitted work to finish.
    /// </summary>
    /// <param name="featureId">The owning Orchard feature identifier.</param>
    /// <param name="timeout">The maximum amount of time to wait.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DrainAsync(string featureId, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens work admission for the specified feature after a failed disable or fresh-shell reconciliation.
    /// </summary>
    /// <param name="featureId">The owning Orchard feature identifier.</param>
    void Activate(string featureId);
}
