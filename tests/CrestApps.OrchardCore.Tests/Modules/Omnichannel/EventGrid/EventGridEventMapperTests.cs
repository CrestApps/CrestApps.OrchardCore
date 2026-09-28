using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.EventGrid;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Models;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.EventGrid;

/// <summary>
/// Bug: an Azure Communication Services text delivered through Event Grid was stored on the "Unknown" channel under its
/// raw event type, so neither SMS automation nor the Messaging workspace ever saw it. The mapper now raises it as the
/// platform's own SMS received event on the SMS channel, in the shape the Twilio and Telnyx webhooks produce. Confirmed
/// live; these pin the mapping.
/// </summary>
public sealed class EventGridEventMapperTests
{
    private const string SmsReceivedData = """
        {
            "messageId": "Outgoing_20260928_msg-1",
            "from": "+15550001111",
            "to": "+18880002222",
            "message": "Is my appointment still on?",
            "receivedTimestamp": "2026-09-28T10:15:30Z"
        }
        """;

    [Fact]
    public void Map_SmsReceived_RaisesTheSmsReceivedEventOnTheSmsChannel()
    {
        // Act
        var mapping = EventGridEventMapper.Map(EventGridEventTypes.AcsSmsReceived, SmsReceivedData);

        // Assert
        Assert.Equal(EventGridEventMappingKind.SmsReceived, mapping.Kind);
        Assert.Equal(OmnichannelConstants.Channels.Sms, mapping.Channel);
        Assert.Equal(OmnichannelConstants.Events.SmsReceived, mapping.EventName);
        Assert.Equal("Outgoing_20260928_msg-1", mapping.ProviderMessageId);
        Assert.Equal("+15550001111", mapping.From);
        Assert.Equal("+18880002222", mapping.To);
        Assert.Equal("Is my appointment still on?", mapping.Content);
        Assert.Equal(new DateTime(2026, 9, 28, 10, 15, 30, DateTimeKind.Utc), mapping.ReceivedUtc);
        Assert.Equal(DateTimeKind.Utc, mapping.ReceivedUtc.Value.Kind);
        Assert.Null(mapping.Reason);
    }

    // A plain DateTime parse of an offset timestamp converts it to the server's local time, which put a text in the
    // wrong place in the thread on any server not running on UTC. The offset must be converted to UTC.
    [Theory]
    [InlineData("2026-09-28T03:15:30-07:00")]
    [InlineData("2026-09-28T12:15:30+02:00")]
    [InlineData("2026-09-28T10:15:30+00:00")]
    public void Map_SmsReceived_ConvertsATimestampWithAnOffsetToUtc(string receivedTimestamp)
    {
        // Arrange
        var data = $$"""{ "from": "+15550001111", "to": "+18880002222", "message": "hi", "receivedTimestamp": "{{receivedTimestamp}}" }""";

        // Act
        var mapping = EventGridEventMapper.Map(EventGridEventTypes.AcsSmsReceived, data);

        // Assert
        Assert.Equal(new DateTime(2026, 9, 28, 10, 15, 30, DateTimeKind.Utc), mapping.ReceivedUtc);
        Assert.Equal(DateTimeKind.Utc, mapping.ReceivedUtc.Value.Kind);
    }

    // A timestamp without an offset is the provider's UTC time, not the server's local time.
    [Fact]
    public void Map_SmsReceived_TreatsATimestampWithoutAnOffsetAsUtc()
    {
        // Arrange
        const string data = """{ "from": "+15550001111", "to": "+18880002222", "receivedTimestamp": "2026-09-28T10:15:30" }""";

        // Act
        var mapping = EventGridEventMapper.Map(EventGridEventTypes.AcsSmsReceived, data);

        // Assert
        Assert.Equal(new DateTime(2026, 9, 28, 10, 15, 30, DateTimeKind.Utc), mapping.ReceivedUtc);
    }

    [Theory]
    [InlineData("""{ "from": "+15550001111", "to": "+18880002222" }""")]
    [InlineData("""{ "from": "+15550001111", "to": "+18880002222", "receivedTimestamp": "not a time" }""")]
    public void Map_SmsReceived_WithoutAReadableTimestamp_LeavesTheReceiveTimeUnset(string data)
    {
        // Act
        var mapping = EventGridEventMapper.Map(EventGridEventTypes.AcsSmsReceived, data);

        // Assert
        Assert.Equal(EventGridEventMappingKind.SmsReceived, mapping.Kind);
        Assert.Null(mapping.ReceivedUtc);
    }

    [Fact]
    public void Map_SmsReceived_WithoutAMessage_MapsEmptyContent()
    {
        // Act
        var mapping = EventGridEventMapper.Map(EventGridEventTypes.AcsSmsReceived, """{ "from": "+15550001111", "to": "+18880002222" }""");

        // Assert
        Assert.Equal(string.Empty, mapping.Content);
    }

    // Without both numbers the text cannot be matched to an endpoint or a contact; routing it would only surface as a
    // handler warning far from the cause, so it is reported as malformed at the mapping.
    [Theory]
    [InlineData("""{ "to": "+18880002222", "message": "hi" }""")]
    [InlineData("""{ "from": "+15550001111", "message": "hi" }""")]
    [InlineData("""{ "from": "  ", "to": "+18880002222", "message": "hi" }""")]
    [InlineData("""{ "from": "+15550001111", "to": "", "message": "hi" }""")]
    [InlineData("""{ "from": 15550001111, "to": "+18880002222", "message": "hi" }""")]
    public void Map_SmsReceived_WithoutBothNumbers_IsMalformed(string data)
    {
        // Act
        var mapping = EventGridEventMapper.Map(EventGridEventTypes.AcsSmsReceived, data);

        // Assert
        Assert.Equal(EventGridEventMappingKind.Malformed, mapping.Kind);
        Assert.Contains("'from' or 'to'", mapping.Reason);
        Assert.Null(mapping.Channel);
        Assert.Null(mapping.EventName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("\"a text\"")]
    [InlineData("42")]
    [InlineData("{ not json")]
    public void Map_SmsReceived_WhenTheDataIsNotAJsonObject_IsMalformed(string data)
    {
        // Act
        var mapping = EventGridEventMapper.Map(EventGridEventTypes.AcsSmsReceived, data);

        // Assert
        Assert.Equal(EventGridEventMappingKind.Malformed, mapping.Kind);
        Assert.Contains("not a JSON object", mapping.Reason);
    }

    [Fact]
    public void Map_DeliveryReport_IsRecognizedWithItsStatusButNotRouted()
    {
        // Arrange
        const string data = """
            {
                "messageId": "Outgoing_20260928_msg-2",
                "from": "+18880002222",
                "to": "+15550001111",
                "deliveryStatus": "Delivered",
                "deliveryStatusDetails": "No error.",
                "receivedTimestamp": "2026-09-28T03:15:30-07:00"
            }
            """;

        // Act
        var mapping = EventGridEventMapper.Map(EventGridEventTypes.AcsSmsDeliveryReportReceived, data);

        // Assert
        Assert.Equal(EventGridEventMappingKind.SmsDeliveryReport, mapping.Kind);
        Assert.Equal(OmnichannelConstants.Channels.Sms, mapping.Channel);
        Assert.Null(mapping.EventName);
        Assert.Equal("Outgoing_20260928_msg-2", mapping.ProviderMessageId);
        Assert.Equal("+18880002222", mapping.From);
        Assert.Equal("+15550001111", mapping.To);
        Assert.Equal("Delivered", mapping.DeliveryStatus);
        Assert.Equal("No error.", mapping.DeliveryStatusDetails);
        Assert.Equal(new DateTime(2026, 9, 28, 10, 15, 30, DateTimeKind.Utc), mapping.ReceivedUtc);
    }

    [Fact]
    public void Map_DeliveryReport_WhenTheDataIsNotAJsonObject_IsMalformed()
    {
        // Act
        var mapping = EventGridEventMapper.Map(EventGridEventTypes.AcsSmsDeliveryReportReceived, "[]");

        // Assert
        Assert.Equal(EventGridEventMappingKind.Malformed, mapping.Kind);
    }

    [Theory]
    [InlineData("Microsoft.Storage.BlobCreated")]
    [InlineData("Contoso.Items.ItemReceived")]
    [InlineData("")]
    [InlineData(null)]
    public void Map_AnUnknownEventType_IsUnmapped(string eventType)
    {
        // Act
        var mapping = EventGridEventMapper.Map(eventType, SmsReceivedData);

        // Assert
        Assert.Equal(EventGridEventMappingKind.Unmapped, mapping.Kind);
        Assert.Null(mapping.Channel);
        Assert.Null(mapping.EventName);
    }

    // Event Grid type names are not case-sensitive, so a subscription that reports the type in another case must still
    // reach the SMS channel.
    [Theory]
    [InlineData("microsoft.communication.smsreceived", EventGridEventMappingKind.SmsReceived)]
    [InlineData("MICROSOFT.COMMUNICATION.SMSRECEIVED", EventGridEventMappingKind.SmsReceived)]
    [InlineData("microsoft.communication.smsdeliveryreportreceived", EventGridEventMappingKind.SmsDeliveryReport)]
    public void Map_MatchesTheEventTypeIgnoringCase(string eventType, EventGridEventMappingKind expected)
    {
        // Act
        var mapping = EventGridEventMapper.Map(eventType, SmsReceivedData);

        // Assert
        Assert.Equal(expected, mapping.Kind);
    }

    [Fact]
    public void Map_SmsReceived_ReadsPropertyNamesIgnoringCase()
    {
        // Act
        var mapping = EventGridEventMapper.Map(
            EventGridEventTypes.AcsSmsReceived,
            """{ "From": "+15550001111", "TO": "+18880002222", "Message": "hi", "MessageId": "m-1" }""");

        // Assert
        Assert.Equal(EventGridEventMappingKind.SmsReceived, mapping.Kind);
        Assert.Equal("+15550001111", mapping.From);
        Assert.Equal("+18880002222", mapping.To);
        Assert.Equal("hi", mapping.Content);
        Assert.Equal("m-1", mapping.ProviderMessageId);
    }
}
