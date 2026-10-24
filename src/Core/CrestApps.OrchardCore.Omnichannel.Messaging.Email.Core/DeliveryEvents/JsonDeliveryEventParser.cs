using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;

/// <summary>
/// Reads delivery events in a plain JSON shape, for providers and automation platforms without a format of their own:
/// one object or an array of objects with <c>type</c> (<c>bounce</c>, <c>soft_bounce</c>, <c>blocked</c>,
/// <c>complaint</c>, <c>unsubscribe</c>, <c>deferred</c>, <c>delivered</c>), <c>email</c>, and optionally
/// <c>permanent</c>, <c>status</c>, <c>reason</c>, <c>messageId</c>, <c>id</c> and <c>timestamp</c>.
/// </summary>
public sealed class JsonDeliveryEventParser : IEmailDeliveryEventParser
{
    public string Name => EmailChannelConstants.Providers.Json;

    public async Task<EmailDeliveryEventParseResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var document = await DeliveryEventJson.ReadAsync(request.Body, cancellationToken);

        if (document is null)
        {
            return EmailDeliveryEventParseResult.Invalid("The payload is not valid JSON.");
        }

        var events = DeliveryEventJson.Items(document.RootElement).Select(Parse).Where(deliveryEvent => deliveryEvent is not null).ToList();

        return events.Count == 0
            ? EmailDeliveryEventParseResult.Invalid("The payload names no event the channel knows. Give each event a type and an email.")
            : EmailDeliveryEventParseResult.Of(events);
    }

    /// <summary>
    /// Reads one event.
    /// </summary>
    /// <param name="item">The event object.</param>
    /// <returns>The event, or <see langword="null"/> when it names no known type or no recipient.</returns>
    public static EmailDeliveryEvent Parse(JsonElement item)
    {
        var recipient = DeliveryEventJson.GetString(item, "email", "recipient", "to");
        var type = DeliveryEventJson.GetString(item, "type", "event")?.Trim().ToLowerInvariant().Replace('-', '_');

        if (string.IsNullOrWhiteSpace(recipient) || string.IsNullOrEmpty(type))
        {
            return null;
        }

        EmailDeliveryEventKind? kind = type switch
        {
            "bounce" or "bounced" or "failed" => DeliveryEventJson.GetString(item, "permanent") is "false"
                ? EmailDeliveryEventKind.SoftBounce
                : EmailDeliveryEventKind.HardBounce,
            "hard_bounce" => EmailDeliveryEventKind.HardBounce,
            "soft_bounce" => EmailDeliveryEventKind.SoftBounce,
            "blocked" or "block" => EmailDeliveryEventKind.Blocked,
            "complaint" or "spam" or "spamreport" or "spam_complaint" => EmailDeliveryEventKind.Complaint,
            "unsubscribe" or "unsubscribed" => EmailDeliveryEventKind.Unsubscribed,
            "deferred" or "delayed" => EmailDeliveryEventKind.Deferred,
            "delivered" or "delivery" => EmailDeliveryEventKind.Delivered,
            _ => null,
        };

        return kind is null
            ? null
            : new EmailDeliveryEvent
            {
                Kind = kind.Value,
                Recipient = recipient,
                MessageId = DeliveryEventJson.GetString(item, "messageId", "message_id"),
                EventId = DeliveryEventJson.GetString(item, "id", "eventId"),
                Status = DeliveryEventJson.GetString(item, "status"),
                Reason = DeliveryEventJson.GetString(item, "reason", "description"),
                OccurredUtc = DeliveryEventJson.GetTime(item, "timestamp", "date"),
                Provider = EmailChannelConstants.Providers.Json,
            };
    }
}
