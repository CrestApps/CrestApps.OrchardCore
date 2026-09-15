using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Orchestrates agent-assisted secure data capture. It lets an agent send a live customer to a dedicated secure
/// page to enter sensitive data, so the raw value is tokenized at submission and only a masked representation and
/// a token reference are retained. The agent, the supervisor, and the recording never see the raw value.
/// </summary>
public interface ISecureCaptureService
{
    /// <summary>
    /// Begins a secure capture on the caller's own live interaction, after verifying the tenant permits secure
    /// capture and the caller owns the interaction. It mints a one-time customer access token and, when
    /// configured, pauses recording for the duration of the capture.
    /// </summary>
    /// <param name="interactionId">The interaction to attach the capture to.</param>
    /// <param name="userId">The Orchard user identifier of the requesting agent.</param>
    /// <param name="principal">The authenticated principal of the requesting agent.</param>
    /// <param name="fields">The sensitive field kinds to ask the customer to provide.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome, carrying the raw access token exactly once on success.</returns>
    Task<SecureCaptureBeginResult> BeginAsync(
        string interactionId,
        string userId,
        ClaimsPrincipal principal,
        IReadOnlyCollection<SecureCaptureField> fields,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the collecting capture a customer access token authorizes, when the token matches an active,
    /// unexpired capture. It is the read the customer secure page performs before rendering its form.
    /// </summary>
    /// <param name="rawAccessToken">The raw access token presented by the customer page.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching collecting capture, or <see langword="null"/> when the token is invalid or expired.</returns>
    Task<SecureCaptureSession> GetForCustomerAsync(string rawAccessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tokenizes the sensitive values a customer submitted to a secure capture page and completes the capture.
    /// The raw values reach only the token sink and are never persisted, logged, or returned.
    /// </summary>
    /// <param name="rawAccessToken">The raw access token presented by the customer page.</param>
    /// <param name="values">The raw sensitive values keyed by field kind, as submitted by the customer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of the submission.</returns>
    Task<SecureCaptureActionResult> SubmitAsync(
        string rawAccessToken,
        IReadOnlyDictionary<SecureCaptureField, string> values,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a collecting capture on the caller's own interaction, after verifying the caller owns it, and
    /// resumes recording when the capture had paused it.
    /// </summary>
    /// <param name="sessionId">The identifier of the capture session to cancel.</param>
    /// <param name="userId">The Orchard user identifier of the requesting agent.</param>
    /// <param name="principal">The authenticated principal of the requesting agent.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of the cancellation.</returns>
    Task<SecureCaptureActionResult> CancelAsync(
        string sessionId,
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires collecting captures whose window has elapsed, resuming recording for any that had paused it. This
    /// is the safety net that settles captures a customer never completed.
    /// </summary>
    /// <param name="maxCount">The maximum number of captures to expire in one pass.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of captures expired.</returns>
    Task<int> ExpireDueAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries the recording resume for settled captures whose resume did not complete on the settlement path, so
    /// a resume that failed or was interrupted cannot leave recording suppressed indefinitely.
    /// </summary>
    /// <param name="maxCount">The maximum number of captures to recover in one pass.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of captures whose recording resume was confirmed.</returns>
    Task<int> RecoverRecordingResumesAsync(int maxCount, CancellationToken cancellationToken = default);
}
