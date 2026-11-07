using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

public sealed class EmailDeliveryEventParserTests
{
    [Fact]
    public void SendGrid_ReadsBouncesBlocksComplaintsDeferralsAndDrops()
    {
        // Arrange
        var payload = """
            [
              { "email": "gone@example.com", "event": "bounce", "type": "bounce", "status": "5.1.1", "reason": "550 5.1.1 user unknown", "smtp-id": "<m-1@contoso.com>", "sg_event_id": "e1", "timestamp": 1760011200 },
              { "email": "ann@example.com", "event": "bounce", "type": "blocked", "status": "5.7.1", "reason": "554 blocked", "sg_event_id": "e2", "timestamp": 1760011200 },
              { "email": "bob@example.com", "event": "spamreport", "sg_event_id": "e3", "timestamp": 1760011200 },
              { "email": "cy@gmail.com", "event": "deferred", "response": "421 4.7.0 try later", "sg_event_id": "e4", "timestamp": 1760011200 },
              { "email": "old@example.com", "event": "dropped", "reason": "Bounced Address", "sg_event_id": "e5", "timestamp": 1760011200 },
              { "email": "dee@example.com", "event": "open", "sg_event_id": "e6", "timestamp": 1760011200 }
            ]
            """;

        // Act
        var events = SendGridDeliveryEventParser.Parse(Json(payload));

        // Assert
        Assert.Equal(
            [EmailDeliveryEventKind.HardBounce, EmailDeliveryEventKind.Blocked, EmailDeliveryEventKind.Complaint, EmailDeliveryEventKind.Deferred, EmailDeliveryEventKind.HardBounce],
            events.Select(deliveryEvent => deliveryEvent.Kind));

        Assert.Equal("<m-1@contoso.com>", events[0].MessageId);
        Assert.Equal("5.1.1", events[0].Status);
        Assert.Equal("e1", events[0].EventId);
        Assert.Equal(new DateTime(2025, 10, 9, 12, 0, 0, DateTimeKind.Utc), events[0].OccurredUtc);
        Assert.Equal("421 4.7.0 try later", events[3].Reason);
    }

    [Theory]
    [InlineData("failed", "permanent", "bounce", EmailDeliveryEventKind.HardBounce)]
    [InlineData("failed", "temporary", "generic", EmailDeliveryEventKind.SoftBounce)]
    [InlineData("failed", "permanent", "espblock", EmailDeliveryEventKind.Blocked)]
    [InlineData("complained", null, null, EmailDeliveryEventKind.Complaint)]
    [InlineData("unsubscribed", null, null, EmailDeliveryEventKind.Unsubscribed)]
    public void Mailgun_ReadsTheEventAndItsSeverity(string name, string severity, string reason, EmailDeliveryEventKind expected)
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["event"] = name,
            ["id"] = "evt-1",
            ["recipient"] = "ann@example.com",
            ["timestamp"] = 1760011200.5,
            ["delivery-status"] = new Dictionary<string, object> { ["code"] = 550, ["description"] = "No such user" },
            ["message"] = new Dictionary<string, object> { ["headers"] = new Dictionary<string, object> { ["message-id"] = "m-1@contoso.com" } },
        };

        if (severity is not null)
        {
            data["severity"] = severity;
        }

        if (reason is not null)
        {
            data["reason"] = reason;
        }

        // Act
        var deliveryEvent = MailgunDeliveryEventParser.Parse(Json(JsonSerializer.Serialize(data)));

        // Assert
        Assert.Equal(expected, deliveryEvent.Kind);
        Assert.Equal("ann@example.com", deliveryEvent.Recipient);
        Assert.Equal("m-1@contoso.com", deliveryEvent.MessageId);
        Assert.Equal("evt-1", deliveryEvent.EventId);
    }

    [Fact]
    public void Mailgun_IgnoresEventsTheChannelDoesNotActOn()
    {
        Assert.Null(MailgunDeliveryEventParser.Parse(Json("""{ "event": "opened", "recipient": "ann@example.com" }""")));
    }

    [Theory]
    [InlineData("""{ "RecordType": "Bounce", "ID": 42, "Type": "HardBounce", "Email": "gone@example.com", "MessageID": "pm-1", "Details": "smtp;550 5.1.1", "BouncedAt": "2026-10-09T12:00:00Z" }""", EmailDeliveryEventKind.HardBounce, "Bounce:42")]
    [InlineData("""{ "RecordType": "Bounce", "ID": 43, "Type": "SoftBounce", "Email": "full@example.com" }""", EmailDeliveryEventKind.SoftBounce, "Bounce:43")]
    [InlineData("""{ "RecordType": "Bounce", "ID": 44, "Type": "DMARCPolicy", "Email": "ann@example.com" }""", EmailDeliveryEventKind.Blocked, "Bounce:44")]
    [InlineData("""{ "RecordType": "SpamComplaint", "ID": 45, "Type": "SpamComplaint", "Email": "bob@example.com" }""", EmailDeliveryEventKind.Complaint, "SpamComplaint:45")]
    [InlineData("""{ "RecordType": "SubscriptionChange", "Recipient": "cy@example.com", "SuppressSending": true, "SuppressionReason": "ManualSuppression" }""", EmailDeliveryEventKind.Unsubscribed, null)]
    public void Postmark_ReadsTheRecordTypeAndBounceType(string payload, EmailDeliveryEventKind expected, string eventId)
    {
        // Act
        var deliveryEvent = PostmarkDeliveryEventParser.Parse(Json(payload));

        // Assert
        Assert.Equal(expected, deliveryEvent.Kind);
        Assert.Equal(eventId, deliveryEvent.EventId);
        Assert.False(string.IsNullOrEmpty(deliveryEvent.Recipient));
    }

    [Fact]
    public void Postmark_IgnoresAnAutoResponder()
    {
        Assert.Null(PostmarkDeliveryEventParser.Parse(Json("""{ "RecordType": "Bounce", "Type": "AutoResponder", "Email": "ann@example.com" }""")));
    }

    [Fact]
    public void AmazonSes_APermanentBounce_IsOneHardBouncePerRecipient()
    {
        // Arrange
        var notification = """
            {
              "notificationType": "Bounce",
              "bounce": {
                "bounceType": "Permanent",
                "bounceSubType": "General",
                "feedbackId": "fb-1",
                "timestamp": "2026-10-09T12:00:00.000Z",
                "bouncedRecipients": [
                  { "emailAddress": "gone@example.com", "status": "5.1.1", "diagnosticCode": "smtp; 550 5.1.1 user unknown" },
                  { "emailAddress": "also-gone@example.com", "status": "5.1.1" }
                ]
              },
              "mail": { "messageId": "ses-1", "commonHeaders": { "messageId": "<m-1@contoso.com>" } }
            }
            """;

        // Act
        var events = AmazonSesDeliveryEventParser.Parse(Json(notification));

        // Assert
        Assert.Equal(2, events.Count);
        Assert.All(events, deliveryEvent => Assert.Equal(EmailDeliveryEventKind.HardBounce, deliveryEvent.Kind));
        Assert.Equal("<m-1@contoso.com>", events[0].MessageId);
        Assert.Equal(["fb-1:0", "fb-1:1"], events.Select(deliveryEvent => deliveryEvent.EventId));
    }

    [Fact]
    public void AmazonSes_ReadsComplaintsTransientBouncesAndConfigurationSetEvents()
    {
        var complaint = AmazonSesDeliveryEventParser.Parse(Json("""
            { "notificationType": "Complaint", "complaint": { "feedbackId": "fb-2", "complaintFeedbackType": "abuse", "complainedRecipients": [ { "emailAddress": "bob@example.com" } ] }, "mail": { "messageId": "ses-2" } }
            """));

        var transient = AmazonSesDeliveryEventParser.Parse(Json("""
            { "eventType": "Bounce", "bounce": { "bounceType": "Transient", "bouncedRecipients": [ { "emailAddress": "full@example.com" } ] }, "mail": { "messageId": "ses-3" } }
            """));

        Assert.Equal(EmailDeliveryEventKind.Complaint, Assert.Single(complaint).Kind);
        Assert.Equal("ses-2", complaint[0].MessageId);
        Assert.Equal(EmailDeliveryEventKind.SoftBounce, Assert.Single(transient).Kind);
    }

    [Fact]
    public async Task Json_ReadsOneEventOrMany()
    {
        // Arrange
        var payload = """
            [
              { "type": "bounce", "email": "gone@example.com", "status": "5.1.1" },
              { "type": "bounce", "email": "full@example.com", "permanent": false },
              { "type": "spam", "email": "bob@example.com", "id": "x-1" },
              { "type": "something-else", "email": "cy@example.com" }
            ]
            """;

        // Act
        var result = await new JsonDeliveryEventParser().ParseAsync(Request(payload), settings: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [EmailDeliveryEventKind.HardBounce, EmailDeliveryEventKind.SoftBounce, EmailDeliveryEventKind.Complaint],
            result.Events.Select(deliveryEvent => deliveryEvent.Kind));
    }

    [Fact]
    public async Task Json_ACallWithNoKnownEvent_IsInvalid()
    {
        var result = await new JsonDeliveryEventParser().ParseAsync(Request("""{ "hello": "world" }"""), settings: null, TestContext.Current.CancellationToken);

        Assert.True(result.IsInvalid);
    }

    private static JsonElement Json(string payload)
        => JsonDocument.Parse(payload).RootElement.Clone();

    private static HttpRequest Request(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/json";

        return context.Request;
    }
}
