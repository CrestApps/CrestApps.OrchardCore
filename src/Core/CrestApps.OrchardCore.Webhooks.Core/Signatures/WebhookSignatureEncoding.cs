namespace CrestApps.OrchardCore.Webhooks.Core.Signatures;

/// <summary>
/// How a signature digest is written.
/// </summary>
public enum WebhookSignatureEncoding
{
    /// <summary>
    /// Lowercase hexadecimal.
    /// </summary>
    Hex,

    /// <summary>
    /// Standard base64.
    /// </summary>
    Base64,
}
