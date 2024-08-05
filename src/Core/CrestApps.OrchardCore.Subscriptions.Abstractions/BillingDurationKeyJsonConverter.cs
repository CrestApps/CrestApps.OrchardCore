using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Subscriptions;

public class BillingDurationKeyJsonConverter : JsonConverter<BillingDurationKey>
{
    public static readonly BillingDurationKeyJsonConverter Instance = new();

    public override BillingDurationKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        var type = BillingDurationType.Year;
        var duration = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(BillingDurationKey.Type):
                        type = (BillingDurationType)reader.GetInt32();
                        break;
                    case nameof(BillingDurationKey.Duration):
                        duration = reader.GetInt32();
                        break;
                    default:
                        break;
                }
            }
        }

        return new BillingDurationKey(type, duration);
    }

    public override void Write(Utf8JsonWriter writer, BillingDurationKey value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(BillingDurationKey.Type), (int)value.Type);
        writer.WriteNumber(nameof(BillingDurationKey.Duration), value.Duration);
        writer.WriteEndObject();
    }
}
