using System.Text.Json;
using CrestApps.OrchardCore.DialPad.Services;

namespace CrestApps.OrchardCore.Tests.Modules.DialPad;

public sealed class DialPadJsonSerializerOptionsTests
{
    [Fact]
    public void Default_DeserializesSnakeCasePayloadWithoutJsonAttributes()
    {
        // Arrange
        const string json = """
            {
              "call_id": "call-1",
              "state": "connected",
              "direction": "inbound",
              "external_number": "+15551112222",
              "internal_number": "+15553334444",
              "target": "+15553334444",
              "contact_name": "Jane Doe",
              "event_timestamp": 1736510400000,
              "is_muted": true,
              "recording_state": "paused",
              "recording_id": "rec-1",
              "is_conference": true,
              "participant_count": 3
            }
            """;

        // Act
        var model = JsonSerializer.Deserialize<DialPadCallEvent>(json, DialPadJsonSerializerOptions.Default);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("call-1", model.CallId);
        Assert.Equal("connected", model.State);
        Assert.Equal("inbound", model.Direction);
        Assert.Equal("+15551112222", model.ExternalNumber);
        Assert.Equal("+15553334444", model.InternalNumber);
        Assert.Equal("+15553334444", model.Target);
        Assert.Equal("Jane Doe", model.ContactName);
        Assert.Equal(1736510400000, model.EventTimestamp);
        Assert.True(model.IsMuted);
        Assert.Equal("paused", model.RecordingState);
        Assert.Equal("rec-1", model.RecordingId);
        Assert.True(model.IsConference);
        Assert.Equal(3, model.ParticipantCount);
    }
}
