namespace CrestApps.OrchardCore.Webhooks.Models;

/// <summary>
/// A webhook for one receiver that isn't a subscription.
/// </summary>
public sealed class WebhookDirectDelivery
{
    /// <summary>
    /// Gets or sets what the delivery is for, such as an agent platform's id; deliveries are listed by it.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the receiver's address.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the event's id, which a receiver de-duplicates by; one is made when empty.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the event's name.
    /// </summary>
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the JSON body, sent exactly as it is.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the formatter that adds the headers and signature.
    /// </summary>
    public string FormatterName { get; set; }

    /// <summary>
    /// Gets or sets what the formatter needs, such as the receiver's id. No secret goes here: the formatter reads
    /// secrets itself, when it sends.
    /// </summary>
    public IDictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
}
