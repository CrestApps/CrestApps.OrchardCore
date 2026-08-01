namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Provides tenant-wide recording media cleanup for an <see cref="IRecordingMediaStore"/>.
/// </summary>
public interface ISupportsTenantMediaPurge
{
    /// <summary>
    /// Deletes every recording owned by the current tenant.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when all tenant media was removed; otherwise, <see langword="false"/>.</returns>
    Task<bool> TryPurgeAllAsync(CancellationToken cancellationToken = default);
}
