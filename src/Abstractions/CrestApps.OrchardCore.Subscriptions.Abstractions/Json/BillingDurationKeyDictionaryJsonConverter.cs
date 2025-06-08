using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Subscriptions;

namespace Json;

public class BillingDurationKeyDictionaryJsonConverter : JsonConverter<Dictionary<BillingDurationKey, double>>
{
    public static readonly BillingDurationKeyDictionaryJsonConverter Instance = new();

    public override Dictionary<BillingDurationKey, double> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<BillingDurationKey, double>();

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
                var value = reader.GetDouble();
                dictionary.Add(key, value);
            }
        }

        return dictionary;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<BillingDurationKey, double> dictionary, JsonSerializerOptions options)
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
