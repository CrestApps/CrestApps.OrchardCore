using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Orchard's document serializer writes every DateTime at whole-second precision, so a hold, a ring or an event time
/// read back from a stored document lost up to a second each save. These prove the audit timestamps keep every tick
/// even when the serializer options carry a whole-second converter like Orchard's, and that values already stored at
/// second precision still read.
/// </summary>
public sealed class PreciseUtcDateTimeJsonConverterTests
{
    private static readonly DateTime _precise = new DateTime(2026, 9, 24, 1, 4, 29, DateTimeKind.Utc).AddTicks(4_173_063);

    private static readonly JsonSerializerOptions _wholeSecondOptions = new()
    {
        Converters = { new WholeSecondConverter() },
    };

    [Fact]
    public void InteractionEvent_KeepsEveryTickOfWhenItHappenedAndWasRecorded()
    {
        // Arrange
        var interactionEvent = new InteractionEvent { OccurredUtc = _precise, RecordedUtc = _precise.AddTicks(1) };

        // Act
        var json = JsonSerializer.Serialize(interactionEvent, _wholeSecondOptions);
        var read = JsonSerializer.Deserialize<InteractionEvent>(json, _wholeSecondOptions);

        // Assert
        Assert.Equal(_precise, read.OccurredUtc);
        Assert.Equal(_precise.AddTicks(1), read.RecordedUtc);
        Assert.Equal(DateTimeKind.Utc, read.OccurredUtc.Kind);
    }

    [Fact]
    public void CallSession_KeepsEveryTickOfTheHoldStart_AndNullStaysNull()
    {
        // Arrange
        var session = new CallSession { HoldStartedUtc = _precise, EndedUtc = null };

        // Act
        var read = JsonSerializer.Deserialize<CallSession>(JsonSerializer.Serialize(session, _wholeSecondOptions), _wholeSecondOptions);

        // Assert
        Assert.Equal(_precise, read.HoldStartedUtc);
        Assert.Null(read.EndedUtc);
    }

    [Fact]
    public void A_ValueStoredAtWholeSeconds_StillReads()
    {
        // Arrange
        const string Stored = """{"OccurredUtc":"2026-09-24T01:04:29Z","RecordedUtc":"2026-09-24T01:04:29Z"}""";

        // Act
        var read = JsonSerializer.Deserialize<InteractionEvent>(Stored, _wholeSecondOptions);

        // Assert
        Assert.Equal(new DateTime(2026, 9, 24, 1, 4, 29, DateTimeKind.Utc), read.OccurredUtc);
    }

    // Stands in for Orchard's DateTimeJsonConverter, which writes the clock components at second precision.
    private sealed class WholeSecondConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => DateTime.Parse(reader.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
    }
}
