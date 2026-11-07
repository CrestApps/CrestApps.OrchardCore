namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Event names the webhooks building block itself uses.
/// </summary>
public static class WebhookEvents
{
    /// <summary>
    /// The event name a subscription lists to take every event, those declared later included.
    /// </summary>
    public const string All = "*";

    /// <summary>
    /// The event an administrator sends from a subscription's page, to try the receiver.
    /// </summary>
    public const string Ping = "webhook.ping";
}
