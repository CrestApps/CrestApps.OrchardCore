using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for secure capture sessions.
/// </summary>
public interface ISecureCaptureSessionManager : ICatalogManager<SecureCaptureSession>
{
    /// <summary>
    /// Finds the secure capture session that a one-time access token hash authorizes.
    /// </summary>
    /// <param name="accessTokenHash">The SHA-256 hash of the raw access token presented by the customer page.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching capture session, or <see langword="null"/> when none matches.</returns>
    Task<SecureCaptureSession> FindByAccessTokenHashAsync(string accessTokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists collecting captures whose window has expired at or before the supplied UTC instant.
    /// </summary>
    /// <param name="utcNow">The current UTC instant.</param>
    /// <param name="maxCount">The maximum number of captures to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The expired collecting captures bounded by <paramref name="maxCount"/>.</returns>
    Task<IReadOnlyCollection<SecureCaptureSession>> ListExpiredAsync(DateTime utcNow, int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the active, still-collecting capture for an interaction, so a second capture is not started while
    /// one is already in progress for the same interaction.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The active capture, or <see langword="null"/> when none is collecting for the interaction.</returns>
    Task<SecureCaptureSession> FindActiveByInteractionAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists settled captures that engaged a recording pause that has not yet been confirmed as resumed, so a
    /// resume that failed on the settlement path can be retried rather than leaving recording suppressed.
    /// </summary>
    /// <param name="maxCount">The maximum number of captures to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The settled captures awaiting a recording resume, bounded by <paramref name="maxCount"/>.</returns>
    Task<IReadOnlyCollection<SecureCaptureSession>> ListPendingRecordingResumeAsync(int maxCount, CancellationToken cancellationToken = default);
}
