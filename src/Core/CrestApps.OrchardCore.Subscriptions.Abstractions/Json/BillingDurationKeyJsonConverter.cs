using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions;

namespace Json;

public class BillingDurationKeyJsonConverter : JsonConverter<BillingDurationKey>
{
    public static readonly BillingDurationKeyJsonConverter Instance = new();

    public override BillingDurationKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject token but got {reader.TokenType}.");
        }

        var type = DurationType.Year;
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
                        if (reader.TokenType != JsonTokenType.Number || !Enum.IsDefined(typeof(DurationType), reader.GetInt32()))
                        {
                            throw new JsonException($"Invalid value for {nameof(BillingDurationKey.Type)}: {reader.GetString()}");
                        }
                        type = (DurationType)reader.GetInt32();
                        break;
                    case nameof(BillingDurationKey.Duration):
                        if (reader.TokenType != JsonTokenType.Number)
                        {
                            throw new JsonException($"Invalid value for {nameof(BillingDurationKey.Duration)}: {reader.GetString()}");
                        }
                        duration = reader.GetInt32();
                        break;
                    default:
                        throw new JsonException($"Unexpected property: {propertyName}");
                }
            }
            else
            {
                throw new JsonException($"Unexpected token: {reader.TokenType}");
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
