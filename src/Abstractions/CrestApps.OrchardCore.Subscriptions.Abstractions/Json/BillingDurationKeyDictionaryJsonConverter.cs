using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Subscriptions;

namespace Json;

/// <summary>
/// Converts dictionaries keyed by <see cref="BillingDurationKey"/> to and from JSON objects.
/// </summary>
public class BillingDurationKeyDictionaryJsonConverter : JsonConverter<Dictionary<BillingDurationKey, decimal>>
{
    /// <summary>
    /// Gets the shared converter instance.
    /// </summary>
    public static readonly BillingDurationKeyDictionaryJsonConverter Instance = new();

    /// <summary>
    /// Reads a dictionary whose JSON property names contain serialized billing duration keys and whose values contain amounts.
    /// </summary>
    /// <param name="reader">The JSON reader positioned at the dictionary object.</param>
    /// <param name="typeToConvert">The dictionary type to convert.</param>
    /// <param name="options">The serializer options used while reading dictionary keys.</param>
    /// <returns>The dictionary represented by the JSON object.</returns>
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

    /// <summary>
    /// Writes a dictionary as a JSON object whose property names are serialized billing duration keys.
    /// </summary>
    /// <param name="writer">The JSON writer that receives the dictionary object.</param>
    /// <param name="dictionary">The dictionary to write.</param>
    /// <param name="options">The serializer options used while writing dictionary keys.</param>
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
