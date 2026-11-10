namespace CrestApps.OrchardCore.Webhooks.Models;

/// <summary>
/// A direct webhook delivery about to be sent.
/// </summary>
public sealed class WebhookFormatContext
{
    /// <summary>
    /// Gets or sets the receiver's address.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the event's id.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the event's name.
    /// </summary>
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the JSON body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets when it is sent, in seconds since 1970 (UTC).
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets or sets what the formatter was given when the delivery was queued.
    /// </summary>
    public IReadOnlyDictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
}
