using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Models;

namespace CrestApps.OrchardCore.Omnichannel.EventGrid.Services;

/// <summary>
/// Maps provider events delivered through Azure Event Grid onto the Omnichannel pipeline, so a text received
/// through Event Grid reaches the same handlers, in the same shape, as one received through a provider's own
/// webhook.
/// </summary>
public static class EventGridEventMapper
{
    /// <summary>
    /// Maps one Event Grid event.
    /// </summary>
    /// <param name="eventType">The Event Grid event type.</param>
    /// <param name="data">The event data as JSON.</param>
    /// <returns>The mapping, whose kind is <see cref="EventGridEventMappingKind.Unmapped"/> for an event type with no mapping.</returns>
    public static EventGridEventMapping Map(string eventType, string data)
    {
        if (string.Equals(eventType, EventGridEventTypes.AcsSmsReceived, StringComparison.OrdinalIgnoreCase))
        {
            return MapAcsSmsReceived(data);
        }

        if (string.Equals(eventType, EventGridEventTypes.AcsSmsDeliveryReportReceived, StringComparison.OrdinalIgnoreCase))
        {
            return MapAcsSmsDeliveryReport(data);
        }

        return new EventGridEventMapping
        {
            Kind = EventGridEventMappingKind.Unmapped,
        };
    }

    private static EventGridEventMapping MapAcsSmsReceived(string data)
    {
        if (!TryReadProperties(data, out var properties))
        {
            return Malformed("the event data is not a JSON object");
        }

        var from = GetString(properties, "from");
        var to = GetString(properties, "to");

        // Without both numbers the text cannot be matched to a channel endpoint or a contact, so routing it would
        // only produce a handler warning further down; report it here, where the cause is visible.
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return Malformed("the event data has no 'from' or 'to' number");
        }

        return new EventGridEventMapping
        {
            Kind = EventGridEventMappingKind.SmsReceived,
            Channel = OmnichannelConstants.Channels.Sms,
            EventName = OmnichannelConstants.Events.SmsReceived,
            ProviderMessageId = GetString(properties, "messageId"),
            From = from,
            To = to,
            Content = GetString(properties, "message") ?? string.Empty,
            ReceivedUtc = GetUtcDateTime(properties, "receivedTimestamp"),
        };
    }

    private static EventGridEventMapping MapAcsSmsDeliveryReport(string data)
    {
        if (!TryReadProperties(data, out var properties))
        {
            return Malformed("the event data is not a JSON object");
        }

        return new EventGridEventMapping
        {
            Kind = EventGridEventMappingKind.SmsDeliveryReport,
            Channel = OmnichannelConstants.Channels.Sms,
            ProviderMessageId = GetString(properties, "messageId"),
            From = GetString(properties, "from"),
            To = GetString(properties, "to"),
            DeliveryStatus = GetString(properties, "deliveryStatus"),
            DeliveryStatusDetails = GetString(properties, "deliveryStatusDetails"),
            ReceivedUtc = GetUtcDateTime(properties, "receivedTimestamp"),
        };
    }

    private static EventGridEventMapping Malformed(string reason)
        => new()
        {
            Kind = EventGridEventMappingKind.Malformed,
            Reason = reason,
        };

    private static bool TryReadProperties(string data, out Dictionary<string, string> properties)
    {
        properties = null;

        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(data);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    properties[property.Name] = property.Value.GetString();
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string GetString(Dictionary<string, string> properties, string name)
        => properties.TryGetValue(name, out var value) ? value : null;

    private static DateTime? GetUtcDateTime(Dictionary<string, string> properties, string name)
    {
        // Parse as an offset so a timestamp that carries one is converted to UTC rather than to the server's local
        // time, which is what a plain DateTime parse of an offset value does.
        if (properties.TryGetValue(name, out var value) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            return timestamp.UtcDateTime;
        }

        return null;
    }
}
