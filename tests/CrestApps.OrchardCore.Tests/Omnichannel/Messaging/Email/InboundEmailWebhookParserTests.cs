using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class InboundEmailWebhookParserTests
{
    private static readonly DateTime _now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Mailgun_IsSignatureValid_AcceptsTheHmacOfTheTimestampAndToken()
    {
        // Arrange
        var timestamp = new DateTimeOffset(_now).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = Sign("key-1", timestamp, "token-1");

        // Act & Assert
        Assert.True(MailgunInboundEmailParser.IsSignatureValid("key-1", timestamp, "token-1", signature, _now));
    }

    [Fact]
    public void Mailgun_IsSignatureValid_RefusesAWrongKey()
    {
        // Arrange
        var timestamp = new DateTimeOffset(_now).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = Sign("other-key", timestamp, "token-1");

        // Act & Assert
        Assert.False(MailgunInboundEmailParser.IsSignatureValid("key-1", timestamp, "token-1", signature, _now));
    }

    [Fact]
    public void Mailgun_IsSignatureValid_RefusesAReplayedOldCall()
    {
        // Arrange
        var timestamp = new DateTimeOffset(_now.AddHours(-1)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = Sign("key-1", timestamp, "token-1");

        // Act & Assert
        Assert.False(MailgunInboundEmailParser.IsSignatureValid("key-1", timestamp, "token-1", signature, _now));
    }

    [Theory]
    [InlineData("https://sns.us-east-1.amazonaws.com/SimpleNotificationService-abc.pem", true)]
    [InlineData("https://sns.cn-north-1.amazonaws.com.cn/cert.pem", true)]
    [InlineData("http://sns.us-east-1.amazonaws.com/cert.pem", false)]
    [InlineData("https://evil.example.com/sns.us-east-1.amazonaws.com/cert.pem", false)]
    [InlineData("https://sns.us-east-1.amazonaws.com.evil.example/cert.pem", false)]
    [InlineData("https://s3.amazonaws.com/cert.pem", false)]
    public void AmazonSns_IsAmazonSnsUrl_OnlyTrustsAmazonSnsHostsOverHttps(string url, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, AmazonSnsMessageVerifier.IsAmazonSnsUrl(url));
    }

    [Fact]
    public void AmazonSns_BuildStringToSign_ForANotification_FollowsTheSnsCanonicalForm()
    {
        // Arrange
        using var document = JsonDocument.Parse("""
            {
              "Type": "Notification",
              "MessageId": "m-1",
              "TopicArn": "arn:aws:sns:us-east-1:1:inbound",
              "Message": "hello",
              "Timestamp": "2026-10-09T12:00:00.000Z"
            }
            """);

        // Act
        var stringToSign = AmazonSnsMessageVerifier.BuildStringToSign(document.RootElement);

        // Assert
        Assert.Equal("Message\nhello\nMessageId\nm-1\nTimestamp\n2026-10-09T12:00:00.000Z\nTopicArn\narn:aws:sns:us-east-1:1:inbound\nType\nNotification\n", stringToSign);
    }

    [Fact]
    public void Postmark_Parse_ReadsTheFieldsTheHeadersAndTheAttachments()
    {
        // Arrange
        using var document = JsonDocument.Parse("""
            {
              "FromFull": { "Email": "Ann@Example.com", "Name": "Ann Lee" },
              "ToFull": [ { "Email": "support@contoso.com", "Name": "" } ],
              "CcFull": [],
              "OriginalRecipient": "help@contoso.com",
              "Subject": "Order 1042",
              "TextBody": "Where is it?\n\nOn Tue, Contoso wrote:\n> Shipped.",
              "StrippedTextReply": "Where is it?",
              "Headers": [
                { "Name": "Message-ID", "Value": "<CAF1@mail.example.com>" },
                { "Name": "In-Reply-To", "Value": "<root@contoso.com>" }
              ],
              "Attachments": [
                { "Name": "note.txt", "Content": "aGVsbG8=", "ContentType": "text/plain" }
              ]
            }
            """);

        // Act
        var email = PostmarkInboundEmailParser.Parse(document.RootElement);

        // Assert
        Assert.Equal("ann@example.com", email.From.Address);
        Assert.Equal("Ann Lee", email.From.Name);
        Assert.Contains("help@contoso.com", email.DeliveredTo);
        Assert.Equal("CAF1@mail.example.com", email.MessageId);
        Assert.Equal("root@contoso.com", email.InReplyTo);
        Assert.Equal("Where is it?", email.StrippedReply);
        Assert.Equal("hello", Encoding.UTF8.GetString(Assert.Single(email.Attachments).Content));
    }

    [Fact]
    public async Task Json_ParseAsync_ReadsTheChannelsOwnShape()
    {
        // Arrange
        using var document = JsonDocument.Parse("""
            {
              "from": "Ann Lee <ann@example.com>",
              "to": ["support@contoso.com"],
              "subject": "Hello",
              "text": "Hi there",
              "messageId": "<pa-1@example.com>",
              "headers": { "Auto-Submitted": "auto-replied" },
              "attachments": [ { "fileName": "a.txt", "contentType": "text/plain", "content": "YQ==" } ]
            }
            """);

        // Act
        var email = await JsonInboundEmailWebhookParser.ParseAsync(document.RootElement, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ann@example.com", email.From.Address);
        Assert.Equal("support@contoso.com", Assert.Single(email.To).Address);
        Assert.Equal("pa-1@example.com", email.MessageId);
        Assert.Equal("auto-replied", email.Headers["Auto-Submitted"]);
        Assert.Single(email.Attachments);
    }

    [Fact]
    public async Task SendGrid_ParseAsync_ReadsTheParsedFieldsAndTheEnvelope()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.ContentType = "multipart/form-data; boundary=x";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["from"] = "Ann Lee <ann@example.com>",
            ["to"] = "Contoso <support@contoso.com>",
            ["subject"] = "Order 1042",
            ["text"] = "Where is my order?",
            ["headers"] = "Message-ID: <sg-1@example.com>\nIn-Reply-To: <root@contoso.com>\n",
            ["envelope"] = "{\"to\":[\"help@contoso.com\"],\"from\":\"ann@example.com\"}",
        });

        var parser = new SendGridInboundEmailParser();

        // Act
        var result = await parser.ParseAsync(context.Request, new EmailInboundSettings(), TestContext.Current.CancellationToken);

        // Assert
        var email = Assert.Single(result.Emails);

        Assert.Equal("ann@example.com", email.From.Address);
        Assert.Equal("Order 1042", email.Subject);
        Assert.Equal("sg-1@example.com", email.MessageId);
        Assert.Equal("root@contoso.com", email.InReplyTo);
        Assert.Contains("help@contoso.com", email.DeliveredTo);
    }

    private static string Sign(string key, string timestamp, string token)
        => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(timestamp + token)));
}
