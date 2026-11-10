namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// The error codes of <see cref="HttpSignatureVerification"/>, as UCP names them.
/// </summary>
public static class HttpSignatureErrorCodes
{
    /// <summary>
    /// The <c>Signature-Input</c> or <c>Signature</c> header is absent, cannot be parsed, or has no common label.
    /// </summary>
    public const string SignatureMissing = "signature_missing";

    /// <summary>
    /// The signature has no <c>keyid</c>, or no key is known for it.
    /// </summary>
    public const string KeyNotFound = "key_not_found";

    /// <summary>
    /// The signature does not verify, or a covered component is missing from the message.
    /// </summary>
    public const string SignatureInvalid = "signature_invalid";

    /// <summary>
    /// The key or the <c>alg</c> parameter names an algorithm this verifier does not support.
    /// </summary>
    public const string AlgorithmUnsupported = "algorithm_unsupported";
}
