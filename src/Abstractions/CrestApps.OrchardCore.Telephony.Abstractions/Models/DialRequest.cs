namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a request to place an outbound call.
/// </summary>
public sealed class DialRequest
{
    /// <summary>
    /// Gets or sets the destination phone number or address to call.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets an optional caller identifier to present to the destination. When not provided
    /// the provider uses its configured default caller identifier.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets an optional collection of provider-specific metadata associated with the call.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; }
}
