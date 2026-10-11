using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Checkout.Json;

/// <summary>
/// Serializes a dictionary keyed by <see cref="BillingDurationKey"/> by encoding the key as a JSON string.
/// </summary>
public sealed class BillingDurationKeyDictionaryJsonConverter : JsonConverter<Dictionary<BillingDurationKey, decimal>>
{
    /// <summary>
    /// A shared instance of the converter.
    /// </summary>
    public static readonly BillingDurationKeyDictionaryJsonConverter Instance = new();

    /// <inheritdoc/>
    public override Dictionary<BillingDurationKey, decimal> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<BillingDurationKey, decimal>();

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var keyString = reader.GetString();
                var key = JsonSerializer.Deserialize<BillingDurationKey>(keyString, options);
                reader.Read();
                var value = reader.GetDecimal();
                dictionary.Add(key, value);
            }
        }

        return dictionary;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Dictionary<BillingDurationKey, decimal> dictionary, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in dictionary)
        {
            writer.WritePropertyName(JsonSerializer.Serialize(kvp.Key, options));
            writer.WriteNumberValue(kvp.Value);
        }

        writer.WriteEndObject();
    }
}
