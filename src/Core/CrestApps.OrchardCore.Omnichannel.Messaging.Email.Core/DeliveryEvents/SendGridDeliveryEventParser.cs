using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;

/// <summary>
/// Reads SendGrid's Event Webhook: a JSON array of events, each with <c>event</c>, <c>email</c>, <c>smtp-id</c> and
/// <c>sg_event_id</c>. A <c>bounce</c> of type <c>blocked</c> is a block; a <c>dropped</c> event names the list
/// SendGrid already holds the address on.
/// </summary>
public sealed class SendGridDeliveryEventParser : IEmailDeliveryEventParser
{
    public string Name => EmailChannelConstants.Providers.SendGrid;

    public async Task<EmailDeliveryEventParseResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var document = await DeliveryEventJson.ReadAsync(request.Body, cancellationToken);

        return document is null
            ? EmailDeliveryEventParseResult.Invalid("The SendGrid payload is not valid JSON.")
            : EmailDeliveryEventParseResult.Of(Parse(document.RootElement));
    }

    /// <summary>
    /// Reads the events of a payload.
    /// </summary>
    /// <param name="root">The payload.</param>
    /// <returns>The events SendGrid reported that the channel acts on.</returns>
    public static IList<EmailDeliveryEvent> Parse(JsonElement root)
    {
        var events = new List<EmailDeliveryEvent>();

        foreach (var item in DeliveryEventJson.Items(root))
        {
            var kind = Classify(item);

            if (kind is null)
            {
                continue;
            }

            events.Add(new EmailDeliveryEvent
            {
                Kind = kind.Value,
                Recipient = DeliveryEventJson.GetString(item, "email"),
                MessageId = DeliveryEventJson.GetString(item, "smtp-id", "sg_message_id"),
                EventId = DeliveryEventJson.GetString(item, "sg_event_id"),
                Status = DeliveryEventJson.GetString(item, "status"),
                Reason = DeliveryEventJson.GetString(item, "reason", "response"),
                OccurredUtc = DeliveryEventJson.GetTime(item, "timestamp"),
                Provider = EmailChannelConstants.Providers.SendGrid,
            });
        }

        return events;
    }

    private static EmailDeliveryEventKind? Classify(JsonElement item)
    {
        var name = DeliveryEventJson.GetString(item, "event")?.ToLowerInvariant();

        return name switch
        {
            "bounce" => string.Equals(DeliveryEventJson.GetString(item, "type"), "blocked", StringComparison.OrdinalIgnoreCase)
                ? EmailDeliveryEventKind.Blocked
                : EmailDeliveryEventKind.HardBounce,
            "deferred" => EmailDeliveryEventKind.Deferred,
            "spamreport" => EmailDeliveryEventKind.Complaint,
            "unsubscribe" or "group_unsubscribe" => EmailDeliveryEventKind.Unsubscribed,
            "delivered" => EmailDeliveryEventKind.Delivered,
            "dropped" => ClassifyDropped(DeliveryEventJson.GetString(item, "reason")),
            _ => null,
        };
    }

    // SendGrid drops mail to addresses on its own suppression lists; the reason says which list.
    private static EmailDeliveryEventKind? ClassifyDropped(string reason)
    {
        var text = reason?.ToLowerInvariant() ?? string.Empty;

        if (text.Contains("bounce", StringComparison.Ordinal) || text.Contains("invalid", StringComparison.Ordinal))
        {
            return EmailDeliveryEventKind.HardBounce;
        }

        if (text.Contains("spam", StringComparison.Ordinal))
        {
            return EmailDeliveryEventKind.Complaint;
        }

        return text.Contains("unsubscribe", StringComparison.Ordinal) ? EmailDeliveryEventKind.Unsubscribed : null;
    }
}
