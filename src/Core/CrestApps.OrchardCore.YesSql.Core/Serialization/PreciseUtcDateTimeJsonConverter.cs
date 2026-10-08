using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.YesSql.Core.Serialization;

/// <summary>
/// Writes a UTC timestamp with every tick it has, for the stored documents payroll and call durations are computed
/// from.
/// </summary>
/// <remarks>
/// Orchard's document serializer writes every <see cref="DateTime"/> at whole-second precision. A timestamp an
/// audit or a duration is read back from (the moment an event happened, a call was answered, a hold began, an offer
/// started ringing) would lose up to a second every time it was saved, which is a pay error on a timecard and a
/// wrong hold or ring time on a call. Applied to those properties, this converter keeps the full value; values
/// already stored at second precision still read as they are.
/// </remarks>
public sealed class PreciseUtcDateTimeJsonConverter : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert == typeof(DateTime) || typeToConvert == typeof(DateTime?);

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => typeToConvert == typeof(DateTime)
            ? new ValueConverter()
            : new NullableConverter();

    private static DateTime Read(ref Utf8JsonReader reader)
    {
        var text = reader.GetString();

        return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    }

    private static void Write(Utf8JsonWriter writer, DateTime value)
    {
        // A value that is not already UTC is taken as UTC, as the Orchard converter does, rather than shifted by
        // the server's offset.
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
    }

    private sealed class ValueConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => PreciseUtcDateTimeJsonConverter.Read(ref reader);

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => PreciseUtcDateTimeJsonConverter.Write(writer, value);
    }

    private sealed class NullableConverter : JsonConverter<DateTime?>
    {
        public override bool HandleNull => true;

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null ? null : PreciseUtcDateTimeJsonConverter.Read(ref reader);

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();

                return;
            }

            PreciseUtcDateTimeJsonConverter.Write(writer, value.Value);
        }
    }
}
