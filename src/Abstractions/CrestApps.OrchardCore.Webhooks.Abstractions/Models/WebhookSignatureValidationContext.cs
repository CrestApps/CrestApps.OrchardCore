namespace CrestApps.OrchardCore.Webhooks.Models;

/// <summary>
/// What a webhook signature is checked against.
/// </summary>
public sealed class WebhookSignatureValidationContext
{
    /// <summary>
    /// Gets or sets the signature the sender sent, as it arrived in its header.
    /// </summary>
    public string Signature { get; set; }

    /// <summary>
    /// Gets or sets the shared secret the signature is keyed with.
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// Gets or sets the content the sender signed: the raw body, or the string a scheme builds from the request.
    /// </summary>
    public string SignedContent { get; set; }

    /// <summary>
    /// Gets or sets the timestamp the sender signed with, for the schemes that sign one.
    /// </summary>
    public string Timestamp { get; set; }
}
