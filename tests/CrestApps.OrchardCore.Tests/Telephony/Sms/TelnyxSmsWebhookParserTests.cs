using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class TelnyxSmsWebhookParserTests
{
    [Fact]
    public void TryParse_InboundReceived_ExtractsAddressesAndText()
    {
        var body = """
        {
          "data": {
            "event_type": "message.received",
            "payload": {
              "id": "msg-123",
              "direction": "inbound",
              "text": "Hello there",
              "from": { "phone_number": "+15551112222" },
              "to": [ { "phone_number": "+15553334444", "status": "webhook_delivered" } ]
            }
          }
        }
        """;

        Assert.True(TelnyxSmsWebhookParser.TryParse(body, out var result));
        Assert.True(result.IsInbound);
        Assert.Equal("+15551112222", result.From);
        Assert.Equal("+15553334444", result.To);
        Assert.Equal("Hello there", result.Text);
        Assert.Equal("msg-123", result.ProviderMessageId);
    }

    [Fact]
    public void TryParse_InboundMms_ExtractsMediaUrls()
    {
        var body = """
        {
          "data": {
            "event_type": "message.received",
            "payload": {
              "id": "msg-mms",
              "direction": "inbound",
              "text": "pic",
              "from": { "phone_number": "+15551112222" },
              "to": [ { "phone_number": "+15553334444" } ],
              "media": [ { "url": "https://telnyx.example/media/1.jpg", "content_type": "image/jpeg" } ]
            }
          }
        }
        """;

        Assert.True(TelnyxSmsWebhookParser.TryParse(body, out var result));
        Assert.Single(result.MediaUrls);
        Assert.Equal("https://telnyx.example/media/1.jpg", result.MediaUrls[0]);
    }

    [Theory]
    [InlineData("delivered", SmsDeliveryStatus.Delivered)]
    [InlineData("sent", SmsDeliveryStatus.Sent)]
    [InlineData("delivery_failed", SmsDeliveryStatus.Failed)]
    [InlineData("queued", SmsDeliveryStatus.Queued)]
    public void TryParse_OutboundReceipt_MapsStatus(string telnyxStatus, SmsDeliveryStatus expected)
    {
        var body = $$"""
        {
          "data": {
            "event_type": "message.finalized",
            "payload": {
              "id": "msg-out",
              "direction": "outbound",
              "from": { "phone_number": "+15553334444" },
              "to": [ { "phone_number": "+15551112222", "status": "{{telnyxStatus}}" } ]
            }
          }
        }
        """;

        Assert.True(TelnyxSmsWebhookParser.TryParse(body, out var result));
        Assert.False(result.IsInbound);
        Assert.Equal("+15553334444", result.From);
        Assert.Equal("+15551112222", result.To);
        Assert.Equal(expected, result.DeliveryStatus);
    }

    [Fact]
    public void TryParse_FailedReceipt_ExtractsErrorCode()
    {
        var body = """
        {
          "data": {
            "event_type": "message.finalized",
            "payload": {
              "id": "msg-out",
              "direction": "outbound",
              "from": { "phone_number": "+15553334444" },
              "to": [ { "phone_number": "+15551112222", "status": "delivery_failed" } ],
              "errors": [ { "code": "40010", "title": "Delivery failed" } ]
            }
          }
        }
        """;

        Assert.True(TelnyxSmsWebhookParser.TryParse(body, out var result));
        Assert.Equal(SmsDeliveryStatus.Failed, result.DeliveryStatus);
        Assert.Equal("40010", result.ErrorCode);
    }

    [Fact]
    public void TryParse_UnrecognizedEvent_ReturnsFalse()
    {
        var body = """
        { "data": { "event_type": "call.answered", "payload": { } } }
        """;

        Assert.False(TelnyxSmsWebhookParser.TryParse(body, out _));
    }

    [Fact]
    public void TryParse_MalformedBody_ReturnsFalse()
    {
        Assert.False(TelnyxSmsWebhookParser.TryParse("not json", out _));
        Assert.False(TelnyxSmsWebhookParser.TryParse("", out _));
    }
}
