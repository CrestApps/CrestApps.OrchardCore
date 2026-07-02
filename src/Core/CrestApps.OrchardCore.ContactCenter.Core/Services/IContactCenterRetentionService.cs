namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Enforces Contact Center data-governance retention by purging durable data older than a cutoff.
/// </summary>
public interface IContactCenterRetentionService
{
    /// <summary>
    /// Purges durable interaction events that occurred strictly before the supplied cutoff.
    /// </summary>
    /// <param name="cutoffUtc">The exclusive UTC cutoff; events older than this are deleted.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of events purged.</returns>
    Task<int> PurgeInteractionEventsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
