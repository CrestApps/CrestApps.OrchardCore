using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Web push for the portal: the site's public key, and the devices workers turn push on for.
/// </summary>
public interface IWebPushService
{
    /// <summary>
    /// Returns the site's public VAPID key, making the key pair the first time.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The public key, base64url-encoded.</returns>
    Task<string> GetPublicKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a device for a worker, or updates it when its endpoint is known.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="endpoint">The push endpoint.</param>
    /// <param name="p256dh">The device's public key.</param>
    /// <param name="auth">The device's secret.</param>
    /// <param name="userAgent">The browser.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The subscription.</returns>
    Task<WebPushSubscription> SubscribeAsync(string workerId, string endpoint, string p256dh, string auth, string userAgent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a worker's device.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="endpoint">The push endpoint.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether a device was removed.</returns>
    Task<bool> UnsubscribeAsync(string workerId, string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a worker's devices.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The devices.</returns>
    Task<IReadOnlyList<WebPushSubscription>> ListAsync(string workerId, CancellationToken cancellationToken = default);
}
