namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a provider webhook delivery in an HTTP-agnostic form so provider adapters can validate
/// signatures and normalize the payload without depending on the web stack.
/// </summary>
public sealed class ProviderVoiceWebhookRequest
{
    /// <summary>
    /// Gets or sets the technical name of the provider the webhook was delivered for.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the raw request body exactly as received, used for signature verification.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets the request headers, keyed by header name.
    /// </summary>
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the query string values, keyed by parameter name.
    /// </summary>
    public IDictionary<string, string> Query { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
