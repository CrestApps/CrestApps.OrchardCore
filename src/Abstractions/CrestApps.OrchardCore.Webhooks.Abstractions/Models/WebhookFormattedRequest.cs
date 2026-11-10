namespace CrestApps.OrchardCore.Webhooks.Models;

/// <summary>
/// The headers a direct webhook delivery is sent with, or why it can't be sent.
/// </summary>
public sealed class WebhookFormattedRequest
{
    /// <summary>
    /// Gets or sets the headers to send.
    /// </summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets why the delivery can't be sent, such as a receiver that was removed; it then fails without retries.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Returns a result that says the delivery can't be sent.
    /// </summary>
    /// <param name="error">Why.</param>
    public static WebhookFormattedRequest Fail(string error)
        => new() { Error = error };
}
