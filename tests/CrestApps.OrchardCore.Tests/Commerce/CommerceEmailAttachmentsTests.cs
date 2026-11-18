using System.Text;
using CrestApps.OrchardCore.Commerce.Core.Emails;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// A store's email can carry copies and files, such as a document as a PDF: they reach the mail message, the files'
/// streams are disposed whatever happens, and a send that fails says why.
/// </summary>
public sealed class CommerceEmailAttachmentsTests
{
    private const string Kind = "invoice";

    private readonly List<MailMessage> _sent = [];
    private readonly List<string> _attached = [];

    [Fact]
    public async Task AttachmentsAndCopies_ReachTheMailMessage()
    {
        // Arrange
        var stream = new TrackedStream("%PDF-1.7");
        var sender = CommerceEmailSenders.Create(EmailService(Result.Success()));
        var email = new CommerceEmail
        {
            To = "ap@fabrikam.test",
            Cc = ["buyer@fabrikam.test", " ", "Buyer@Fabrikam.test", "owner@fabrikam.test"],
            Bcc = ["archive@store.test"],
            Reference = "INV-0042",
        };

        // Act
        var result = await sender.SendWithResultAsync(email, _ => Task.FromResult(Content(Attachment("INV-0042.pdf", stream))), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(result.Error);

        var message = Assert.Single(_sent);
        Assert.Equal("ap@fabrikam.test", message.To);
        Assert.Equal("buyer@fabrikam.test,owner@fabrikam.test", message.Cc);
        Assert.Equal("archive@store.test", message.Bcc);

        var attachment = Assert.Single(message.Attachments);
        Assert.Equal("INV-0042.pdf", attachment.Filename);
        Assert.Equal(["%PDF-1.7"], _attached);
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public async Task AnEmailWithoutCopies_SendsNone()
    {
        var sender = CommerceEmailSenders.Create(EmailService(Result.Success()));

        await sender.SendAsync(new CommerceEmail { To = "ap@fabrikam.test" }, _ => Task.FromResult(Content()), TestContext.Current.CancellationToken);

        var message = Assert.Single(_sent);
        Assert.Null(message.Cc);
        Assert.Null(message.Bcc);
        Assert.Empty(message.Attachments);
    }

    [Fact]
    public async Task TheStreams_AreDisposed_WhenTheSendFails()
    {
        // Arrange
        var first = new TrackedStream("one");
        var second = new TrackedStream("two");
        var sender = CommerceEmailSenders.Create(EmailService(Result.Failed(new ResultError { Message = new LocalizedString("Mailbox", "The mailbox is full.") })));

        // Act
        var result = await sender.SendWithResultAsync(
            new CommerceEmail { To = "ap@fabrikam.test" },
            _ => Task.FromResult(Content(Attachment("a.pdf", first), Attachment("b.pdf", second))),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The mailbox is full.", result.Error);
        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
    }

    [Fact]
    public async Task TheStreams_AreDisposed_AndSendAsyncReturnsFalse_WhenTheProviderThrows()
    {
        // Arrange
        var stream = new TrackedStream("one");
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The mail server is down."));

        var sender = CommerceEmailSenders.Create(emailService.Object);

        // Act
        var sent = await sender.SendAsync(new CommerceEmail { To = "ap@fabrikam.test" }, _ => Task.FromResult(Content(Attachment("a.pdf", stream))), TestContext.Current.CancellationToken);
        var result = await sender.SendWithResultAsync(new CommerceEmail { To = "ap@fabrikam.test" }, _ => Task.FromResult(Content(Attachment("a.pdf", new TrackedStream("two")))), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(sent);
        Assert.True(stream.IsDisposed);
        Assert.False(result.Succeeded);
        // The exception's own text is for the log, not for whoever reads the result.
        Assert.DoesNotContain("The mail server is down.", result.Error);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public async Task AFileThatCannotBeOpened_StopsTheSend_AndTheOpenedStreamsAreDisposed()
    {
        // Arrange
        var opened = new TrackedStream("one");
        var sender = CommerceEmailSenders.Create(EmailService(Result.Success()));
        var failing = new CommerceEmailAttachment
        {
            FileName = "b.pdf",
            OpenAsync = _ => throw new IOException("The file is gone."),
        };

        // Act
        var sent = await sender.SendAsync(new CommerceEmail { To = "ap@fabrikam.test" }, _ => Task.FromResult(Content(Attachment("a.pdf", opened), failing)), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(sent);
        Assert.True(opened.IsDisposed);
        Assert.Empty(_sent);
    }

    [Theory]
    [InlineData("../secrets.pdf")]
    [InlineData("folder/INV-0042.pdf")]
    [InlineData("C:\\INV-0042.pdf")]
    [InlineData("..")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task AFileNameWithAPath_StopsTheSend_BeforeAnyFileIsOpened(string fileName)
    {
        // Arrange
        var opens = 0;
        var sender = CommerceEmailSenders.Create(EmailService(Result.Success()));
        var attachment = new CommerceEmailAttachment
        {
            FileName = fileName,
            OpenAsync = _ =>
            {
                opens++;

                return Task.FromResult<Stream>(new MemoryStream());
            },
        };

        // Act
        var result = await sender.SendWithResultAsync(new CommerceEmail { To = "ap@fabrikam.test" }, _ => Task.FromResult(Content(attachment)), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.Error));
        Assert.Equal(0, opens);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task SendWithResultAsync_SaysWhyNothingWasSent()
    {
        var withoutEmail = CommerceEmailSenders.Create(new ServiceCollection().BuildServiceProvider());
        var withEmail = CommerceEmailSenders.Create(EmailService(Result.Success()));

        var noService = await withoutEmail.SendWithResultAsync(new CommerceEmail { To = "ap@fabrikam.test" }, _ => Task.FromResult(Content()), TestContext.Current.CancellationToken);
        var noRecipient = await withEmail.SendWithResultAsync(new CommerceEmail { To = " " }, _ => Task.FromResult(Content()), TestContext.Current.CancellationToken);
        var nothingBuilt = await withEmail.SendWithResultAsync(new CommerceEmail { To = "ap@fabrikam.test" }, _ => Task.FromResult<CommerceEmailContent>(null), TestContext.Current.CancellationToken);

        Assert.All(new[] { noService, noRecipient, nothingBuilt }, result =>
        {
            Assert.False(result.Succeeded);
            Assert.False(string.IsNullOrEmpty(result.Error));
        });
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task TheStoresTemplate_StillApplies_ToAnEmailWithAttachments()
    {
        // Arrange
        var settings = new OrderEmailTemplateSettings();
        settings.Templates[Kind] = new CommerceEmailTemplate
        {
            IsEnabled = true,
            Subject = "{{ StoreName }}: {{ Subject }}",
            Body = "<h1>Ours</h1>{{ Content }}",
        };

        var stream = new TrackedStream("%PDF-1.7");
        var sender = CommerceEmailSenders.CreateWithTemplates(EmailService(Result.Success()), settings);

        // Act
        var sent = await sender.SendAsync(new CommerceEmail { Kind = Kind, To = "ap@fabrikam.test" }, _ => Task.FromResult(Content(Attachment("INV-0042.pdf", stream))), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(sent);

        var message = Assert.Single(_sent);
        Assert.Equal("Test store: Your invoice", message.Subject);
        Assert.Contains("<h1>Ours</h1>", message.HtmlBody);
        Assert.Equal("INV-0042.pdf", Assert.Single(message.Attachments).Filename);
        Assert.True(stream.IsDisposed);
    }

    private static CommerceEmailAttachment Attachment(string fileName, Stream stream)
        => new()
        {
            FileName = fileName,
            ContentType = "application/pdf",
            OpenAsync = _ => Task.FromResult(stream),
        };

    private static CommerceEmailContent Content(params CommerceEmailAttachment[] attachments)
        => new()
        {
            Subject = "Your invoice",
            Text = "Your invoice is attached.",
            Html = "<p>Your invoice is attached.</p>",
            Attachments = attachments,
        };

    private IEmailService EmailService(Result result)
    {
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, string, CancellationToken>((message, _, _) =>
            {
                _sent.Add(message);

                // The provider reads the files while it sends; they are disposed after.
                foreach (var attachment in message.Attachments ?? [])
                {
                    using var reader = new StreamReader(attachment.Stream, Encoding.UTF8, leaveOpen: true);
                    _attached.Add(reader.ReadToEnd());
                }
            })
            .ReturnsAsync(result);

        return emailService.Object;
    }

    private sealed class TrackedStream : MemoryStream
    {
        public TrackedStream(string text)
            : base(Encoding.UTF8.GetBytes(text))
        {
        }

        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
