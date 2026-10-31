using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Hears about certificates that are about to expire or have expired. The daily expiry check raises each reminder
/// offset once per expiry date, and the expiry once.
/// </summary>
public interface ICertificationExpiryObserver
{
    /// <summary>
    /// Called when a certificate reaches one of its certification's reminder offsets.
    /// </summary>
    /// <param name="record">The certificate.</param>
    /// <param name="certification">Its certification.</param>
    /// <param name="daysBefore">The reminder offset reached, in days before expiry.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task ExpiringAsync(CertificationRecord record, Certification certification, int daysBefore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called once when a certificate passes its expiry date and is marked expired.
    /// </summary>
    /// <param name="record">The certificate.</param>
    /// <param name="certification">Its certification.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task ExpiredAsync(CertificationRecord record, Certification certification, CancellationToken cancellationToken = default);
}
