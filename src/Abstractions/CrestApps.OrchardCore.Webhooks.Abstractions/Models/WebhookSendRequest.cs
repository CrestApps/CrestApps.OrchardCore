namespace CrestApps.OrchardCore.Webhooks.Models;

/// <summary>
/// One signed webhook POST.
/// </summary>
public sealed class WebhookSendRequest
{
    /// <summary>
    /// Gets or sets the absolute URL the body is posted to.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the secret the body is signed with.
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// Gets or sets the event's id, sent in <see cref="WebhookHeaders.Id"/>.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the event's name, sent in <see cref="WebhookHeaders.Event"/> when present.
    /// </summary>
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the JSON body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets more headers to send, such as a protocol's own signature.
    /// </summary>
    public IDictionary<string, string> Headers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to add the webhook signature headers with <see cref="Secret"/>. A direct
    /// delivery whose formatter signs it itself turns it off.
    /// </summary>
    public bool Sign { get; set; } = true;
}
