namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// The outcome of verifying an HTTP message signature (RFC 9421).
/// </summary>
public sealed class HttpSignatureVerification
{
    /// <summary>
    /// Gets a value indicating whether the signature verified.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the error code when the signature did not verify; one of <see cref="HttpSignatureErrorCodes"/>.
    /// </summary>
    public string ErrorCode { get; init; }

    /// <summary>
    /// Gets a short description of the failure, for logs. It is never shown to the caller of the API.
    /// </summary>
    public string ErrorMessage { get; init; }

    /// <summary>
    /// Gets the <c>keyid</c> of the signature, when it was read.
    /// </summary>
    public string KeyId { get; init; }

    /// <summary>
    /// Gets the component identifiers the signature covers, when they were read.
    /// </summary>
    public IReadOnlyList<string> CoveredComponents { get; init; } = [];

    /// <summary>
    /// Gets the label of the verified signature, such as <c>sig1</c>.
    /// </summary>
    public string Label { get; init; }

    /// <summary>
    /// Gets when the signer says it signed (the <c>created</c> parameter), when it did.
    /// </summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>
    /// Gets when the signer says the signature stops being valid (the <c>expires</c> parameter), when it did.
    /// </summary>
    public DateTimeOffset? Expires { get; init; }

    /// <summary>
    /// Gets the signer's <c>nonce</c> parameter, when it sent one.
    /// </summary>
    public string Nonce { get; init; }

    internal static HttpSignatureVerification Fail(
        string errorCode,
        string errorMessage,
        string label = null,
        string keyId = null,
        IReadOnlyList<string> components = null)
    {
        return new HttpSignatureVerification
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Label = label,
            KeyId = keyId,
            CoveredComponents = components ?? [],
        };
    }
}
