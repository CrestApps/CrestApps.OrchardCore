using System.Text;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailOutboundFormattingTests
{
    [Fact]
    public void ToHtml_EncodesWhatTheWriterTypedSoNothingIsTakenAsMarkup()
    {
        // Act
        var html = EmailBodyFormatter.ToHtml("Hi <script>alert(1)</script> & welcome", signature: null, unsubscribeUrl: null, unsubscribeText: null);

        // Assert
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&amp; welcome", html);
    }

    [Fact]
    public void ToHtml_KeepsParagraphsAndLineBreaksAndMakesLinksClickable()
    {
        // Act
        var html = EmailBodyFormatter.ToHtml("Hello Ann,\n\nTrack it at https://contoso.com/t/1042.\nThanks", signature: null, unsubscribeUrl: null, unsubscribeText: null);

        // Assert
        Assert.Contains("<p style=\"margin: 0 0 12px 0;\">Hello Ann,</p>", html);
        Assert.Contains("<a href=\"https://contoso.com/t/1042\">https://contoso.com/t/1042</a>.<br>Thanks", html);
    }

    [Fact]
    public void ToText_AddsTheSignatureAfterTheSeparatorAndTheUnsubscribeLinkLast()
    {
        // Act
        var text = EmailBodyFormatter.ToText("Hello", "Contoso Support", "https://contoso.com/u/abc", "Unsubscribe:");

        // Assert
        Assert.Equal("Hello\r\n\r\n-- \r\nContoso Support\r\n\r\nUnsubscribe: https://contoso.com/u/abc", text);
    }

    [Fact]
    public void Build_SetsTheThreadingAndMailTypeHeaders()
    {
        // Arrange
        var message = new EmailTransportMessage
        {
            FromAddress = "support@contoso.com",
            FromName = "Contoso Support",
            ToAddress = "ann@example.com",
            Subject = "Re: Order 1042",
            TextBody = "Hello",
            HtmlBody = "<p>Hello</p>",
            MessageId = "out-1@contoso.com",
            InReplyTo = "CAF1@mail.example.com",
            References = ["root@contoso.com", "CAF1@mail.example.com"],
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Auto-Submitted"] = "auto-replied",
                ["List-Unsubscribe"] = "<https://contoso.com/u/abc>",
            },
            Attachments =
            [
                new EmailTransportAttachment { FileName = "invoice.pdf", ContentType = "application/pdf", Content = Encoding.ASCII.GetBytes("%PDF-1.4") },
            ],
        };

        // Act
        var mime = EmailMimeBuilder.Build(message);

        // Assert
        Assert.Equal("out-1@contoso.com", mime.MessageId);
        Assert.Equal("CAF1@mail.example.com", mime.InReplyTo);
        Assert.Equal(["root@contoso.com", "CAF1@mail.example.com"], mime.References);
        Assert.Equal("auto-replied", mime.Headers["Auto-Submitted"]);
        Assert.Equal("<https://contoso.com/u/abc>", mime.Headers["List-Unsubscribe"]);
        Assert.Equal("Contoso Support", Assert.Single(mime.From.Mailboxes).Name);
        Assert.Equal("invoice.pdf", Assert.Single(mime.Attachments).ContentDisposition.FileName);
        Assert.Empty(mime.ReplyTo);
    }

    [Fact]
    public void Format_QuotesADisplayNameSoItStaysOneName()
    {
        // Act
        var formatted = EmailAddressFormatter.Format("support@contoso.com", "Contoso, Support");

        // Assert
        Assert.Equal("\"Contoso, Support\" <support@contoso.com>", formatted);
    }
}
