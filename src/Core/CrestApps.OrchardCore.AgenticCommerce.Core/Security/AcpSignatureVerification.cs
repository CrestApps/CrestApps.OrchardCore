namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// The outcome of verifying an ACP request signature.
/// </summary>
public enum AcpSignatureVerification
{
    /// <summary>
    /// The signature matches and the timestamp is fresh.
    /// </summary>
    Valid,

    /// <summary>
    /// The <c>Signature</c> or the <c>Timestamp</c> header is missing.
    /// </summary>
    MissingHeaders,

    /// <summary>
    /// The timestamp isn't an RFC 3339 date-time.
    /// </summary>
    InvalidTimestamp,

    /// <summary>
    /// The timestamp is outside the allowed clock skew.
    /// </summary>
    StaleTimestamp,

    /// <summary>
    /// The signature doesn't match the request with the platform's key.
    /// </summary>
    InvalidSignature,
}
