namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of an agent request to begin a secure data capture. On success it carries the raw
/// one-time access token exactly once, so the agent desktop can build the customer link; the token is never
/// persisted in raw form and cannot be retrieved again.
/// </summary>
public sealed class SecureCaptureBeginResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the capture was started.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the created capture session, on success.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the raw one-time access token that authorizes the customer capture page, returned exactly
    /// once on success. It is never persisted in raw form.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the capture window expires, on success.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets a client-safe explanation of the outcome, on failure.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="sessionId">The identifier of the created capture session.</param>
    /// <param name="accessToken">The raw one-time access token for the customer link.</param>
    /// <param name="expiresUtc">The UTC time the capture window expires.</param>
    /// <returns>A successful <see cref="SecureCaptureBeginResult"/>.</returns>
    public static SecureCaptureBeginResult Success(string sessionId, string accessToken, DateTime expiresUtc)
        => new()
        {
            Succeeded = true,
            SessionId = sessionId,
            AccessToken = accessToken,
            ExpiresUtc = expiresUtc,
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="reason">The client-safe failure reason.</param>
    /// <returns>A failed <see cref="SecureCaptureBeginResult"/>.</returns>
    public static SecureCaptureBeginResult Failure(string reason)
        => new() { Succeeded = false, Reason = reason };
}
