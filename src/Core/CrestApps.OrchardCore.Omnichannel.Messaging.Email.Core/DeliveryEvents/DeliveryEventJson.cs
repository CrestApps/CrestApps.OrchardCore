using System.Globalization;
using System.Text.Json;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;

/// <summary>
/// Reads the loosely typed JSON the providers' event webhooks send: a value can be a string or a number, a time a
/// Unix timestamp or an ISO date, and the same field goes by different names.
/// </summary>
internal static class DeliveryEventJson
{
    public static string GetString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    public static JsonElement GetObject(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    public static IEnumerable<JsonElement> GetArray(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    public static bool GetBool(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value) &&
            (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed));

    public static DateTime? GetTime(JsonElement element, params string[] names)
    {
        var text = GetString(element, names);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            // Millisecond timestamps are larger than any second timestamp of this century.
            var milliseconds = seconds > 100_000_000_000 ? seconds : seconds * 1000;

            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    public static async Task<JsonDocument> ReadAsync(Stream body, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // A provider sends one event or an array of them.
    public static IEnumerable<JsonElement> Items(JsonElement root)
        => root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : [root];
}
