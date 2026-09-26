using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Every call in the platform is driven by these webhooks and nothing covered the parser, so a change to the
/// envelope shape would have been discovered by a call that silently stopped progressing rather than by a test.
/// The cases here are the payload shapes Telnyx actually sends, including the ones that are easy to get wrong:
/// a caller-id object rather than a string, a hangup with the SIP cause the platform diagnoses incidents from,
/// and a body that is not an event at all.
/// </summary>
public sealed class TelnyxCallEventParserTests
{
    [Fact]
    public void Parse_ReadsTheEnvelopeAndTheCommonPayloadFields()
    {
        // Arrange
        const string Payload = """
        {
          "data": {
            "id": "evt-1",
            "event_type": "call.answered",
            "occurred_at": "2026-03-04T15:00:00.000Z",
            "payload": {
              "call_control_id": "ctrl-1",
              "call_leg_id": "leg-1",
              "call_session_id": "sess-1",
              "connection_id": "conn-1",
              "direction": "incoming",
              "to": "+16502530000",
              "state": "answered"
            }
          }
        }
        """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.True(parsed);
        Assert.Equal("call.answered", callEvent.EventType);
        Assert.Equal("evt-1", callEvent.EventId);
        Assert.Equal("ctrl-1", callEvent.CallControlId);
        Assert.Equal("leg-1", callEvent.CallLegId);
        Assert.Equal("sess-1", callEvent.CallSessionId);
        Assert.Equal("conn-1", callEvent.ConnectionId);
        Assert.Equal("incoming", callEvent.Direction);
        Assert.Equal("+16502530000", callEvent.To);
        Assert.Equal("answered", callEvent.State);
    }

    [Fact]
    public void Parse_ReadsAHangupWithItsCauses()
    {
        // Arrange
        // The SIP cause is what distinguishes "the agent was busy" from "nothing was registered at that
        // address"; an incident is diagnosed from it, so losing it in parsing turns a five-minute diagnosis
        // into guesswork.
        const string Payload = """
        {
          "data": {
            "event_type": "call.hangup",
            "payload": {
              "call_control_id": "ctrl-1",
              "hangup_cause": "call_rejected",
              "hangup_source": "callee",
              "sip_hangup_cause": "486"
            }
          }
        }
        """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.True(parsed);
        Assert.Equal("call_rejected", callEvent.HangupCause);
        Assert.Equal("callee", callEvent.HangupSource);
        Assert.Equal("486", callEvent.SipHangupCause);
    }

    [Fact]
    public void Parse_ReadsACallerIdSentAsAnObject()
    {
        // Arrange
        // Telnyx sends "from" as a bare string on some events and as an object on others. Reading only one shape
        // loses the caller's number on the other, and the platform matches contacts on it.
        const string Payload = """
        {
          "data": {
            "event_type": "call.initiated",
            "payload": {
              "call_control_id": "ctrl-1",
              "from": { "phone_number": "+16502530001" }
            }
          }
        }
        """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.True(parsed);
        Assert.Equal("+16502530001", callEvent.From);
    }

    [Fact]
    public void Parse_ReadsACallerIdSentAsAString()
    {
        // Arrange
        const string Payload = """
        {
          "data": {
            "event_type": "call.initiated",
            "payload": { "call_control_id": "ctrl-1", "from": "+16502530001" }
          }
        }
        """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.True(parsed);
        Assert.Equal("+16502530001", callEvent.From);
    }

    [Fact]
    public void Parse_RefusesAnEventWithNoCallToActOn()
    {
        // Arrange
        // Every consumer downstream keys on the call control id, so an event without one describes nothing the
        // pipeline can do anything with. Accepting it would put an event with empty fields on the bus and have
        // the projector look for a call that was never named.
        const string Payload = """
        {
          "data": { "event_type": "call.hangup" }
        }
        """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(Payload, out _);

        // Assert
        Assert.False(parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{ "data": "not an object" }""")]
    [InlineData("""{ "data": { "id": "evt-1" } }""")]
    public void Parse_RefusesABodyThatIsNotACallEvent(string payload)
    {
        // Assert
        // Refusing is the point: a malformed body must not produce an event object with empty fields that the
        // pipeline then acts on as though a real call had done something.
        Assert.False(TelnyxCallEventParser.TryParse(payload, out var callEvent));
        Assert.Null(callEvent);
    }

    [Fact]
    public void AGatherEvent_CarriesTheKeyTheCallerPressed()
    {
        // Arrange
        // Without this the menu hears every caller press nothing and sends them all to the fallback.
        var payload = """
            {
              "data": {
                "event_type": "call.gather.ended",
                "id": "event-1",
                "payload": { "call_control_id": "ctrl-1", "digits": "2" }
              }
            }
            """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(payload, out var callEvent);

        // Assert
        Assert.True(parsed);
        Assert.Equal("2", callEvent.Digits);
    }

    [Fact]
    public void AGatherEventWithNoKey_ParsesAsNothingPressed()
    {
        // Arrange
        // A caller who presses nothing before the menu times out has made a missed choice, not a selection, and
        // the flow decides what that means.
        var payload = """
            {
              "data": {
                "event_type": "call.gather.ended",
                "id": "event-1",
                "payload": { "call_control_id": "ctrl-1" }
              }
            }
            """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(payload, out var callEvent);

        // Assert
        Assert.True(parsed);
        Assert.True(string.IsNullOrEmpty(callEvent.Digits));
    }
}
