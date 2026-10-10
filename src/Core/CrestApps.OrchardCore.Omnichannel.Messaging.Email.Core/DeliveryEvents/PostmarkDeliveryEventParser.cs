using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;

/// <summary>
/// Reads Postmark's bounce, spam complaint, delivery and subscription change webhooks, told apart by
/// <c>RecordType</c>; a bounce's <c>Type</c> says what kind it is.
/// </summary>
public sealed class PostmarkDeliveryEventParser : IEmailDeliveryEventParser
{
    public string Name => EmailChannelConstants.Providers.Postmark;

    public async Task<EmailDeliveryEventParseResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var document = await DeliveryEventJson.ReadAsync(request.Body, cancellationToken);

        if (document is null)
        {
            return EmailDeliveryEventParseResult.Invalid("The Postmark payload is not valid JSON.");
        }

        var events = DeliveryEventJson.Items(document.RootElement).Select(Parse).Where(deliveryEvent => deliveryEvent is not null).ToList();

        return events.Count == 0
            ? EmailDeliveryEventParseResult.Handled("The Postmark event is not one the channel acts on.")
            : EmailDeliveryEventParseResult.Of(events);
    }

    /// <summary>
    /// Reads one record.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The event, or <see langword="null"/> when the channel does not act on it.</returns>
    public static EmailDeliveryEvent Parse(JsonElement record)
    {
        var recordType = DeliveryEventJson.GetString(record, "RecordType");

        EmailDeliveryEventKind? kind = recordType switch
        {
            "Bounce" => ClassifyBounce(DeliveryEventJson.GetString(record, "Type")),
            "SpamComplaint" => EmailDeliveryEventKind.Complaint,
            "Delivery" => EmailDeliveryEventKind.Delivered,
            "SubscriptionChange" when DeliveryEventJson.GetBool(record, "SuppressSending") => ClassifySuppression(DeliveryEventJson.GetString(record, "SuppressionReason")),
            _ => null,
        };

        if (kind is null)
        {
            return null;
        }

        return new EmailDeliveryEvent
        {
            Kind = kind.Value,
            Recipient = DeliveryEventJson.GetString(record, "Email", "Recipient"),
            MessageId = DeliveryEventJson.GetString(record, "MessageID"),
            EventId = DeliveryEventJson.GetString(record, "ID") is { } id ? $"{recordType}:{id}" : null,
            Reason = DeliveryEventJson.GetString(record, "Details", "Description"),
            OccurredUtc = DeliveryEventJson.GetTime(record, "BouncedAt", "DeliveredAt", "ChangedAt"),
            Provider = EmailChannelConstants.Providers.Postmark,
        };
    }

    private static EmailDeliveryEventKind? ClassifyBounce(string type)
        => type switch
        {
            "HardBounce" or "BadEmailAddress" or "ManuallyDeactivated" => EmailDeliveryEventKind.HardBounce,
            "Transient" or "SoftBounce" or "DnsError" => EmailDeliveryEventKind.SoftBounce,
            "Blocked" or "DMARCPolicy" => EmailDeliveryEventKind.Blocked,
            "SpamComplaint" or "SpamNotification" => EmailDeliveryEventKind.Complaint,
            "Unsubscribe" => EmailDeliveryEventKind.Unsubscribed,
            _ => null,
        };

    private static EmailDeliveryEventKind? ClassifySuppression(string reason)
        => reason switch
        {
            "HardBounce" => EmailDeliveryEventKind.HardBounce,
            "SpamComplaint" => EmailDeliveryEventKind.Complaint,
            "ManualSuppression" => EmailDeliveryEventKind.Unsubscribed,
            _ => null,
        };
}
