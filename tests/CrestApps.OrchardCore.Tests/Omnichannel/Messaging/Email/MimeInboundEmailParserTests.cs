using System.Text;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class MimeInboundEmailParserTests
{
    [Fact]
    public async Task ParseAsync_ReadsTheAddressesSubjectBodiesAndThreadingIdentifiers()
    {
        // Arrange
        var raw = """
            From: "Ann Lee" <Ann@Example.com>
            To: Contoso Support <Support@Contoso.com>
            Cc: bob@example.com
            Delivered-To: help@contoso.com
            Subject: Order 1042
            Message-ID: <CAF1@mail.example.com>
            In-Reply-To: <root@contoso.com>
            References: <first@contoso.com> <root@contoso.com>
            Date: Tue, 06 Oct 2026 10:01:00 +0000
            MIME-Version: 1.0
            Content-Type: text/plain; charset=utf-8

            Where is my order?
            """;

        // Act
        var email = await ParseAsync(raw);

        // Assert
        Assert.Equal("ann@example.com", email.From.Address);
        Assert.Equal("Ann Lee", email.From.Name);
        Assert.Equal("support@contoso.com", Assert.Single(email.To).Address);
        Assert.Equal("bob@example.com", Assert.Single(email.Cc).Address);
        Assert.Contains("help@contoso.com", email.DeliveredTo);
        Assert.Equal("Order 1042", email.Subject);
        Assert.Equal("CAF1@mail.example.com", email.MessageId);
        Assert.Equal("root@contoso.com", email.InReplyTo);
        Assert.Equal(["first@contoso.com", "root@contoso.com"], email.References);
        Assert.Contains("Where is my order?", email.TextBody);
        Assert.Null(email.DeliveryReport);
    }

    [Fact]
    public async Task ParseAsync_WhenTheEmailNamesAReplyToAddress_TheConversationIsWithThatAddress()
    {
        // Arrange
        // A web form or a notification service sends from its own address and names the customer in Reply-To.
        var raw = """
            From: Website Forms <no-reply@forms.example.net>
            Reply-To: Ann Lee <ann@example.com>
            To: support@contoso.com
            Subject: Contact form
            Content-Type: text/plain

            Please call me.
            """;

        // Act
        var email = await ParseAsync(raw);

        // Assert
        Assert.Equal("ann@example.com", email.From.Address);
        Assert.Equal("Ann Lee", email.From.Name);
    }

    [Fact]
    public async Task ParseAsync_KeepsAttachmentsAndMarksAnInlinePicture()
    {
        // Arrange
        var raw = """
            From: ann@example.com
            To: support@contoso.com
            Subject: Files
            MIME-Version: 1.0
            Content-Type: multipart/mixed; boundary="outer"

            --outer
            Content-Type: multipart/related; boundary="inner"

            --inner
            Content-Type: text/html; charset=utf-8

            <p>See the logo <img src="cid:logo1"></p>
            --inner
            Content-Type: image/png
            Content-ID: <logo1>
            Content-Disposition: inline; filename="logo.png"
            Content-Transfer-Encoding: base64

            iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==
            --inner--
            --outer
            Content-Type: application/pdf; name="invoice.pdf"
            Content-Disposition: attachment; filename="invoice.pdf"
            Content-Transfer-Encoding: base64

            JVBERi0xLjQKJcfsj6IKMSAwIG9iago8PC9UeXBlL0NhdGFsb2c+PgplbmRvYmoKdHJhaWxlcgo8PC9Sb290IDEgMCBSPj4KJSVFT0YK
            --outer--
            """;

        // Act
        var email = await ParseAsync(raw);

        // Assert
        Assert.Equal(2, email.Attachments.Count);

        var logo = Assert.Single(email.Attachments, attachment => attachment.FileName == "logo.png");
        Assert.Equal("logo1", logo.ContentId);
        Assert.True(logo.IsInline);

        var invoice = Assert.Single(email.Attachments, attachment => attachment.FileName == "invoice.pdf");
        Assert.Equal("application/pdf", invoice.ContentType);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(invoice.Content));
    }

    [Fact]
    public async Task ParseAsync_ADeliveryStatusNotification_IsReadAsABounceNamingTheEmailItBounced()
    {
        // Arrange
        var raw = """
            From: Mail Delivery Subsystem <mailer-daemon@contoso.com>
            To: support@contoso.com
            Subject: Undelivered Mail Returned to Sender
            MIME-Version: 1.0
            Content-Type: multipart/report; report-type=delivery-status; boundary="b1"

            --b1
            Content-Type: text/plain

            This message could not be delivered.
            --b1
            Content-Type: message/delivery-status

            Reporting-MTA: dns; mx.contoso.com

            Final-Recipient: rfc822; nobody@example.com
            Action: failed
            Status: 5.1.1
            Diagnostic-Code: smtp; 550 5.1.1 User unknown

            --b1
            Content-Type: text/rfc822-headers

            Message-ID: <sent-1@contoso.com>
            From: support@contoso.com
            To: nobody@example.com
            Subject: Your order

            --b1--
            """;

        // Act
        var email = await ParseAsync(raw);

        // Assert
        Assert.NotNull(email.DeliveryReport);
        Assert.Equal("sent-1@contoso.com", email.DeliveryReport.OriginalMessageId);
        Assert.Equal("nobody@example.com", email.DeliveryReport.Recipient);
        Assert.Equal("failed", email.DeliveryReport.Action);
        Assert.Equal("5.1.1", email.DeliveryReport.Status);
        Assert.True(email.DeliveryReport.IsPermanentFailure);
    }

    [Theory]
    [InlineData("<abc@example.com>", "abc@example.com")]
    [InlineData("  abc@example.com ", "abc@example.com")]
    [InlineData("<>", null)]
    [InlineData(null, null)]
    public void TrimMessageId_RemovesTheAngleBrackets(string value, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, MimeInboundEmailParser.TrimMessageId(value));
    }

    private static async Task<InboundEmail> ParseAsync(string raw)
    {
        var normalized = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(normalized));

        return await MimeInboundEmailParser.ParseAsync(stream, TestContext.Current.CancellationToken);
    }
}
