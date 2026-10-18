namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Builds the live dashboard's dialer banner: stalled, idle for want of an agent, held back, dialing, or nothing to call.
/// </summary>
public interface IDialerPacingBannerService
{
    /// <summary>
    /// Builds the banner over the paced campaign queues the viewer may see.
    /// </summary>
    /// <param name="canSeeQueue">Decides whether the viewer may see a campaign queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The banner.</returns>
    Task<DialerPacingBanner> GetAsync(Func<string, Task<bool>> canSeeQueue, CancellationToken cancellationToken = default);
}
