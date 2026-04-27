using System.Globalization;
using System.Text.Json;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

/// <summary>
/// Represents the chat interaction settings validator.
/// </summary>
public static class ChatInteractionSettingsValidator
{
    /// <summary>
    /// Validates the .
    /// </summary>
    /// <param name="settings">The settings.</param>
    public static string Validate(JsonElement settings)
    {
        if (IsOutsideRange(settings, "strictness", 1, 5))
        {
            return "strictness";
        }

        if (IsOutsideRange(settings, "topNDocuments", 3, 20))
        {
            return "topNDocuments";
        }

        if (IsOutsideRange(settings, "temperature", 0, 2))
        {
            return "temperature";
        }

        if (IsOutsideRange(settings, "topP", 0, 1))
        {
            return "topP";
        }

        if (IsOutsideRange(settings, "frequencyPenalty", 0, 2))
        {
            return "frequencyPenalty";
        }

        if (IsOutsideRange(settings, "presencePenalty", 0, 2))
        {
            return "presencePenalty";
        }

        if (IsOutsideRange(settings, "pastMessagesCount", 2, 50))
        {
            return "pastMessagesCount";
        }

        if (IsOutsideRange(settings, "maxTokens", 4, null))
        {
            return "maxTokens";
        }

        return null;
    }

    private static bool IsOutsideRange(JsonElement settings, string propertyName, double min, double? max)
    {
        if (!TryGetNumber(settings, propertyName, out var value))
        {
            return false;
        }

        if (value < min)
        {
            return true;
        }

        return max.HasValue && value > max.Value;
    }

    private static bool TryGetNumber(JsonElement settings, string propertyName, out double value)
    {
        value = default;

        if (!settings.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Null || property.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetDouble(out value);
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            var raw = property.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }

        return false;
    }
}
