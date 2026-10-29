using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The custom SIP headers a PBX or carrier adds to an inbound call reach the AI conversation that answers it as
/// variables, so a forwarded caller is not asked again for what the system they came from already knew.
/// </summary>
public sealed class SipHeaderVariablesTests
{
    [Theory]
    [InlineData("X-Account-Id", "sip_account_id")]
    [InlineData("x-account-id", "sip_account_id")]
    [InlineData("X-Menu_Choice", "sip_menu_choice")]
    [InlineData("X-Customer.Tier", "sip_customer_tier")]
    [InlineData("X--Odd--Name-", "sip_odd_name")]
    [InlineData("  X-Lang  ", "sip_lang")]
    public void ToVariableName_TurnsACustomHeaderIntoAVariable(string header, string expected)
    {
        // Act
        var name = SipHeaderVariables.ToVariableName(header);

        // Assert
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("X-")]
    [InlineData("X---")]
    [InlineData("User-Agent")]
    [InlineData("P-Asserted-Identity")]
    [InlineData("X-Bad Name")]
    [InlineData("X-Bad:Name")]
    public void ToVariableName_RefusesWhatIsNotAUsableCustomHeader(string header)
    {
        // Act
        var name = SipHeaderVariables.ToVariableName(header);

        // Assert
        Assert.Null(name);
    }

    [Fact]
    public void AddToMetadata_KeepsOnlyCustomHeadersWithAValue_AndCleansThem()
    {
        // Arrange
        var metadata = new Dictionary<string, string> { ["telnyxEventType"] = "call.initiated" };

        // Act
        var added = SipHeaderVariables.AddToMetadata(metadata,
        [
            new("X-Account-Id", " 42 "),
            new("User-Agent", "PBX 1.0"),
            new("X-Empty", "  "),
            new("X-Note", "line one\r\nline two"),
            new("X-Long", new string('a', 300)),
        ]);

        // Assert
        Assert.Equal(3, added);
        Assert.Equal("42", metadata["sip_header:X-Account-Id"]);
        Assert.Equal("line oneline two", metadata["sip_header:X-Note"]);
        Assert.Equal(SipHeaderVariables.MaxValueLength, metadata["sip_header:X-Long"].Length);
        Assert.False(metadata.ContainsKey("sip_header:User-Agent"));
        Assert.False(metadata.ContainsKey("sip_header:X-Empty"));
        Assert.Equal("call.initiated", metadata["telnyxEventType"]);
    }

    [Fact]
    public void AddToMetadata_StopsAtTheLimit()
    {
        // Arrange
        var metadata = new Dictionary<string, string>();
        var headers = Enumerable.Range(1, 30).Select(index => new KeyValuePair<string, string>($"X-Field-{index}", "v"));

        // Act
        var added = SipHeaderVariables.AddToMetadata(metadata, headers);

        // Assert
        Assert.Equal(SipHeaderVariables.MaxHeaders, added);
        Assert.Equal(SipHeaderVariables.MaxHeaders, metadata.Count);
    }

    [Fact]
    public void ToVariables_ReadsTheHeadersBack_AndIgnoresTheRestOfTheMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            ["telnyxEventType"] = "call.initiated",
            ["sip_header:X-Account-Id"] = "42",
            ["sip_header:X-Menu-Choice"] = "billing",
            ["sip_header:User-Agent"] = "slipped in",
        };

        // Act
        var variables = SipHeaderVariables.ToVariables(metadata);

        // Assert
        Assert.Equal(2, variables.Count);
        Assert.Equal("42", variables["sip_account_id"]);
        Assert.Equal("billing", variables["sip_menu_choice"]);
    }

    [Fact]
    public void Parser_ReadsTheCustomHeadersOfAnInboundCall()
    {
        // Arrange
        const string Payload = """
            {
              "data": {
                "event_type": "call.initiated",
                "id": "evt-1",
                "payload": {
                  "call_control_id": "v3:inbound",
                  "direction": "incoming",
                  "from": "+15551234567",
                  "to": "+17025550100",
                  "custom_headers": [
                    { "name": "X-Account-Id", "value": "42" },
                    { "name": "X-Menu-Choice", "value": "billing" },
                    { "value": "nameless" }
                  ]
                }
              }
            }
            """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.True(parsed);
        Assert.Equal(2, callEvent.CustomHeaders.Count);
        Assert.Equal(new KeyValuePair<string, string>("X-Account-Id", "42"), callEvent.CustomHeaders[0]);
        Assert.Equal(new KeyValuePair<string, string>("X-Menu-Choice", "billing"), callEvent.CustomHeaders[1]);
    }

    [Fact]
    public void Parser_GivesNoHeaders_WhenTheCallCarriesNone()
    {
        // Arrange
        const string Payload = """{ "data": { "event_type": "call.hangup", "payload": { "call_control_id": "v3:leg" } } }""";

        // Act
        TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.Empty(callEvent.CustomHeaders);
    }

    [Fact]
    public async Task Router_HandsTheCustomHeadersToTheContactCenter()
    {
        // Arrange
        InboundVoiceEvent routed = null;
        var sink = new Mock<IInboundVoiceEventSink>();
        sink
            .Setup(value => value.RouteAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InboundVoiceEvent, CancellationToken>((inboundEvent, _) => routed = inboundEvent)
            .ReturnsAsync(new InboundVoiceRouteOutcome());
        var router = new ContactCenterTelnyxInboundCallRouter(sink.Object);
        var callEvent = new TelnyxCallEvent
        {
            CallControlId = "v3:inbound",
            From = "+15551234567",
            To = "+17025550100",
            CustomHeaders = [new("X-Account-Id", "42"), new("Diversion", "<sip:+17025550199@example.com>")],
        };

        // Act
        await router.RouteAsync(callEvent, new DateTime(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(routed);
        Assert.Equal("42", routed.Metadata["sip_header:X-Account-Id"]);
        Assert.Single(routed.Metadata);
        Assert.Equal("42", SipHeaderVariables.ToVariables(routed.Metadata)["sip_account_id"]);
    }
}
