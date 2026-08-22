using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions;

namespace Json;

/// <summary>
/// Converts <see cref="BillingDurationKey"/> instances to and from their JSON object representation.
/// </summary>
public class BillingDurationKeyJsonConverter : JsonConverter<BillingDurationKey>
{
    /// <summary>
    /// Gets the shared converter instance.
    /// </summary>
    public static readonly BillingDurationKeyJsonConverter Instance = new();

    /// <summary>
    /// Reads a billing duration key from a JSON object that contains the duration type and duration count.
    /// </summary>
    /// <param name="reader">The JSON reader positioned at the billing duration key object.</param>
    /// <param name="typeToConvert">The billing duration key type to convert.</param>
    /// <param name="options">The serializer options used during conversion.</param>
    /// <returns>The billing duration key represented by the JSON object.</returns>
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

    /// <summary>
    /// Writes a billing duration key as a JSON object containing the duration type and duration count.
    /// </summary>
    /// <param name="writer">The JSON writer that receives the billing duration key object.</param>
    /// <param name="value">The billing duration key to write.</param>
    /// <param name="options">The serializer options used during conversion.</param>
    public override void Write(Utf8JsonWriter writer, BillingDurationKey value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(BillingDurationKey.Type), (int)value.Type);
        writer.WriteNumber(nameof(BillingDurationKey.Duration), value.Duration);
        writer.WriteEndObject();
    }
}
