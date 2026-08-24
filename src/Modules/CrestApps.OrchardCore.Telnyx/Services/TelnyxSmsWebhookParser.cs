using System.Text.Json;
using CrestApps.OrchardCore.Sms.Workspace.Models;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The extracted, provider-normalized form of a Telnyx messaging webhook delivery.
/// </summary>
public sealed class TelnyxSmsWebhookEvent
{
    /// <summary>
    /// Gets the Telnyx event type (for example <c>message.received</c> or <c>message.finalized</c>).
    /// </summary>
    public string EventType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the message is inbound (customer → us).
    /// </summary>
    public bool IsInbound { get; init; }

    /// <summary>
    /// Gets the sender number (E.164). For inbound this is the customer; for outbound receipts this is our DID.
    /// </summary>
    public string From { get; init; }

    /// <summary>
    /// Gets the recipient number (E.164). For inbound this is our DID; for outbound receipts this is the customer.
    /// </summary>
    public string To { get; init; }

    /// <summary>
    /// Gets the message text.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// Gets the Telnyx message identifier.
    /// </summary>
    public string ProviderMessageId { get; init; }

    /// <summary>
    /// Gets the inbound media URLs, when present (MMS).
    /// </summary>
    public IReadOnlyList<string> MediaUrls { get; init; } = [];

    /// <summary>
    /// Gets the normalized delivery status for an outbound receipt.
    /// </summary>
    public SmsDeliveryStatus DeliveryStatus { get; init; }

    /// <summary>
    /// Gets the provider error code, when the outbound message failed.
    /// </summary>
    public string ErrorCode { get; init; }
}

/// <summary>
/// Parses Telnyx messaging (SMS/MMS) webhook payloads into a normalized <see cref="TelnyxSmsWebhookEvent"/>.
/// </summary>
public static class TelnyxSmsWebhookParser
{
    /// <summary>
    /// Attempts to parse a Telnyx messaging webhook body.
    /// </summary>
    /// <param name="body">The raw JSON body.</param>
    /// <param name="result">The parsed event, when successful.</param>
    /// <returns><see langword="true"/> when the body is a recognized messaging webhook.</returns>
    public static bool TryParse(string body, out TelnyxSmsWebhookEvent result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("event_type", out var eventTypeElement) ||
                !data.TryGetProperty("payload", out var payload))
            {
                return false;
            }

            var eventType = eventTypeElement.GetString();

            if (eventType != TelnyxConstants.SmsEvents.MessageReceived &&
                eventType != TelnyxConstants.SmsEvents.MessageSent &&
                eventType != TelnyxConstants.SmsEvents.MessageFinalized)
            {
                return false;
            }

            var direction = payload.TryGetProperty("direction", out var directionElement)
                ? directionElement.GetString()
                : null;

            var isInbound = string.Equals(direction, "inbound", StringComparison.OrdinalIgnoreCase);

            var providerMessageId = payload.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var fromNumber = ReadPhoneNumber(payload, "from");
            var (toNumber, toStatus) = ReadFirstDestination(payload);
            var text = payload.TryGetProperty("text", out var textElement) ? textElement.GetString() : null;

            result = new TelnyxSmsWebhookEvent
            {
                EventType = eventType,
                IsInbound = isInbound,
                From = fromNumber,
                To = toNumber,
                Text = text,
                ProviderMessageId = providerMessageId,
                MediaUrls = ReadMediaUrls(payload),
                DeliveryStatus = MapStatus(toStatus),
                ErrorCode = ReadFirstErrorCode(payload),
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ReadPhoneNumber(JsonElement payload, string propertyName)
    {
        if (payload.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("phone_number", out var phone))
        {
            return phone.GetString();
        }

        return null;
    }

    private static (string Number, string Status) ReadFirstDestination(JsonElement payload)
    {
        if (payload.TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.Array)
        {
            foreach (var destination in to.EnumerateArray())
            {
                var number = destination.TryGetProperty("phone_number", out var phone) ? phone.GetString() : null;
                var status = destination.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;

                return (number, status);
            }
        }

        return (null, null);
    }

    private static List<string> ReadMediaUrls(JsonElement payload)
    {
        if (!payload.TryGetProperty("media", out var media) || media.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var urls = new List<string>();

        foreach (var item in media.EnumerateArray())
        {
            if (item.TryGetProperty("url", out var url) && url.GetString() is { Length: > 0 } value)
            {
                urls.Add(value);
            }
        }

        return urls;
    }

    private static string ReadFirstErrorCode(JsonElement payload)
    {
        if (payload.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            foreach (var error in errors.EnumerateArray())
            {
                if (error.TryGetProperty("code", out var code))
                {
                    return code.ValueKind == JsonValueKind.Number ? code.GetRawText() : code.GetString();
                }
            }
        }

        return null;
    }

    private static SmsDeliveryStatus MapStatus(string status)
        => status?.ToLowerInvariant() switch
        {
            "queued" or "sending" or "gw_timeout" => SmsDeliveryStatus.Queued,
            "sent" or "delivery_unconfirmed" => SmsDeliveryStatus.Sent,
            "delivered" or "webhook_delivered" or "received" => SmsDeliveryStatus.Delivered,
            "delivery_failed" or "sending_failed" or "failed" => SmsDeliveryStatus.Failed,
            "expired" or "rejected" => SmsDeliveryStatus.Undelivered,
            _ => SmsDeliveryStatus.Sent,
        };
}
