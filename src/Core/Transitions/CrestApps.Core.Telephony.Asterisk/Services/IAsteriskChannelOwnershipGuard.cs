namespace CrestApps.Core.Telephony.Asterisk.Services;

/// <summary>
/// Determines whether an Asterisk channel is owned by the current tenant.
/// </summary>
public interface IAsteriskChannelOwnershipGuard
{
    /// <summary>
    /// Checks whether a channel ownership binding exists in the current tenant store.
    /// </summary>
    /// <param name="channelId">The Asterisk channel identifier to check.</param>
    /// <returns><see langword="true"/> when the current tenant owns the channel; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsOwnedByCurrentTenantAsync(string channelId);
}
