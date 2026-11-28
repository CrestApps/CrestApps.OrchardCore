using CrestApps.OrchardCore.Webhooks.Core.Models;

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// What the webhook tools return about a webhook and a delivery, from an explicit list of fields: never the signing
/// secret, protected or not, and never a delivery's payload.
/// </summary>
internal static class WebhookToolResults
{
    /// <summary>
    /// The sentence that sends a person to the webhook's page for its signing secret.
    /// </summary>
    public const string SecretNote = "The signing secret is never shown here; open the webhook's admin page (editUrl) to copy it into the receiver.";

    /// <summary>
    /// Describes a webhook.
    /// </summary>
    /// <param name="webhook">The webhook.</param>
    /// <param name="created">When it was created, in the user's time zone.</param>
    /// <param name="modified">When it was last changed, in the user's time zone.</param>
    /// <param name="editUrl">The path of its admin page.</param>
    /// <returns>The description.</returns>
    public static Dictionary<string, object> Describe(WebhookSubscription webhook, string created, string modified, string editUrl)
    {
        var events = webhook.Events ?? [];
        var allEvents = events.Contains(WebhookEvents.All);

        return new Dictionary<string, object>
        {
            ["id"] = webhook.ItemId,
            ["name"] = webhook.Name,
            ["url"] = webhook.Url,
            ["enabled"] = webhook.IsEnabled,
            ["allEvents"] = allEvents,
            ["events"] = allEvents ? new List<string>() : events.Where(entry => entry != WebhookEvents.All).ToList(),
            ["hasSecret"] = !string.IsNullOrEmpty(webhook.ProtectedSecret),
            ["created"] = created,
            ["modified"] = modified,
            ["editUrl"] = editUrl,
        };
    }

    /// <summary>
    /// Describes what an attempt at a delivery came to.
    /// </summary>
    /// <param name="delivery">The delivery.</param>
    /// <returns>The description.</returns>
    public static object DescribeAttempt(WebhookDelivery delivery)
        => new
        {
            deliveryId = delivery.ItemId,
            @event = delivery.EventName,
            delivered = delivery.Status == WebhookDeliveryStatus.Delivered,
            status = delivery.Status.ToString(),
            statusCode = delivery.LastStatusCode,
            error = delivery.LastError,
            attempts = delivery.Attempts,
            triedAgainLater = delivery.Status == WebhookDeliveryStatus.Pending ? true : (bool?)null,
        };
}
