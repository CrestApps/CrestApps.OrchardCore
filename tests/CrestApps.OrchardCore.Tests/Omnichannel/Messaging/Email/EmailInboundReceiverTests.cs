using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailInboundReceiverTests
{
    private static readonly OmnichannelChannelEndpoint _support = new()
    {
        ItemId = "address-support",
        Value = "support@contoso.com",
        Capabilities = [OmnichannelConstants.Channels.Email],
    };

    [Fact]
    public async Task ReceiveAsync_CommitsTheEmailToTheInboxUnderItsMessageId_WithTheReplySeparatedFromTheQuote()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox);

        var email = Email();
        email.TextBody = "Yes please.\n\nOn Mon, Contoso wrote:\n> Shall we reship it?";

        // Act
        var result = await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailInboundStatus.Accepted, result.Status);
        Assert.Equal("address-support", result.AddressId);

        var delivery = Assert.Single(inbox.Accepted);
        Assert.Equal(EmailInboundReceiver.InboxProviderName, delivery.ProviderName);
        Assert.Equal("CAF1@mail.example.com", delivery.DeliveryId);
        Assert.Equal(EmailChannelConstants.InboxHandlerName, delivery.HandlerName);
        Assert.Single(inbox.Dispatched);

        var message = JsonSerializer.Deserialize<OmnichannelMessage>(delivery.Payload);
        Assert.Equal(OmnichannelConstants.Channels.Email, message.Channel);
        Assert.Equal("ann@example.com", message.CustomerAddress);
        Assert.Equal("support@contoso.com", message.ServiceAddress);
        Assert.True(message.IsInbound);
        Assert.Equal("Yes please.", message.Content);
        Assert.Contains("Shall we reship it?", message.GetQuotedText());
        Assert.Equal("Order 1042", message.GetSubject());
        Assert.Equal("Ann Lee", message.GetEmailMetadata().FromName);
        Assert.Equal("root@contoso.com", message.GetEmailMetadata().InReplyTo);
    }

    [Fact]
    public async Task ReceiveAsync_WhenAskedNotToDispatch_OnlyCommitsTheEmail()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox);

        // Act
        var result = await receiver.ReceiveAsync(Email(), "sendgrid", dispatch: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailInboundStatus.Accepted, result.Status);
        Assert.Equal("inbox-1", result.InboxMessageId);
        Assert.Empty(inbox.Dispatched);
    }

    [Fact]
    public async Task ReceiveAsync_ARedeliveredEmail_IsReportedAsADuplicateAndNotDispatchedAgain()
    {
        // Arrange
        var inbox = new RecordingInbox { NextStatus = ProviderWebhookInboxAcceptanceStatus.Duplicate };
        var receiver = CreateReceiver(inbox);

        // Act
        var result = await receiver.ReceiveAsync(Email(), "mailgun", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailInboundStatus.Duplicate, result.Status);
        Assert.Empty(inbox.Dispatched);
    }

    [Fact]
    public async Task ReceiveAsync_FindsTheAddressThroughTheDeliveryHeaderForABccOrAnAlias()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox);

        var email = Email();
        email.To = [new InboundEmailAddress("someone-else@example.com")];
        email.DeliveredTo = ["Support@Contoso.com"];

        // Act
        var result = await receiver.ReceiveAsync(email, "imap", dispatch: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailInboundStatus.Accepted, result.Status);
        Assert.Equal("address-support", result.AddressId);
    }

    [Fact]
    public async Task ReceiveAsync_WhenNoRecipientIsOneOfTheBusinessAddresses_ReceivesNothing()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox);

        var email = Email();
        email.To = [new InboundEmailAddress("nobody@elsewhere.com")];

        // Act
        var result = await receiver.ReceiveAsync(email, "json", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailInboundStatus.UnknownAddress, result.Status);
        Assert.Empty(inbox.Accepted);
    }

    [Fact]
    public async Task ReceiveAsync_MailFromOneOfTheBusinessesOwnAddresses_IsIgnoredSoItCannotLoop()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox, ownAddresses: ["sales@contoso.com"]);

        var email = Email();
        email.From = new InboundEmailAddress("sales@contoso.com", "Sales");

        // Act
        var result = await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailInboundStatus.Ignored, result.Status);
        Assert.Empty(inbox.Accepted);
    }

    [Fact]
    public async Task ReceiveAsync_ABounce_IsNeverReceivedAsAMessage()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox);

        var email = Email();
        email.DeliveryReport = new InboundEmailDeliveryReport { Action = "delayed" };

        // Act
        var result = await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailInboundStatus.Ignored, result.Status);
        Assert.Empty(inbox.Accepted);
    }

    [Fact]
    public async Task ReceiveAsync_KeepsTheFilesButNotTheInlinePictureTheHtmlShows()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var store = new Mock<IMessagingAttachmentStore>();
        var receiver = CreateReceiver(inbox, attachmentStore: store.Object);

        var email = Email();
        email.HtmlBody = "<p>Logo: <img src=\"cid:logo1\"></p>";
        email.Attachments =
        [
            new InboundEmailAttachment { FileName = "logo.png", ContentType = "image/png", ContentId = "logo1", IsInline = true, Content = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00] },
            new InboundEmailAttachment { FileName = "invoice.pdf", ContentType = "application/pdf", Content = Encoding.ASCII.GetBytes("%PDF-1.4 test") },
            new InboundEmailAttachment { FileName = "virus.exe", ContentType = "application/octet-stream", Content = [0x4D, 0x5A, 0x90] },
        ];

        // Act
        await receiver.ReceiveAsync(email, "imap", dispatch: false, TestContext.Current.CancellationToken);

        // Assert
        var message = JsonSerializer.Deserialize<OmnichannelMessage>(Assert.Single(inbox.Accepted).Payload);
        var attachment = Assert.Single(message.GetAttachments());

        Assert.Equal("invoice.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(1, message.GetSkippedAttachmentCount());
        store.Verify(value => value.StoreAsync(attachment.Id, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveAsync_AnEmailWithoutAMessageId_IsGivenAStableIdentitySoARedeliveryIsStillRecognised()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox);

        var first = Email();
        first.MessageId = null;
        var second = Email();
        second.MessageId = null;

        // Act
        await receiver.ReceiveAsync(first, "imap", dispatch: false, TestContext.Current.CancellationToken);
        await receiver.ReceiveAsync(second, "imap", dispatch: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, inbox.Accepted.Count);
        Assert.Equal(inbox.Accepted[0].DeliveryId, inbox.Accepted[1].DeliveryId);
    }

    [Fact]
    public async Task ReceiveAsync_ADeliveryReportAboutAnEmailThisSiteNeverSent_IsIgnored()
    {
        // Arrange
        var events = new Mock<IEmailDeliveryEventProcessor>();
        var receiver = CreateReceiver(new RecordingInbox(), deliveryEvents: events.Object);

        var email = Email();
        email.DeliveryReport = new InboundEmailDeliveryReport { OriginalMessageId = "forged@evil.example", Recipient = "victim@example.com", Action = "failed", Status = "5.1.1" };

        // Act
        await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert: nobody can suppress an address by mailing a report about an email the site did not send.
        events.Verify(processor => processor.ProcessAsync(It.IsAny<IEnumerable<EmailDeliveryEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveAsync_ADeliveryReportNamingAnotherRecipientThanTheEmailWentTo_IsIgnored()
    {
        // Arrange
        var events = new Mock<IEmailDeliveryEventProcessor>();
        var sent = new Mock<IEmailSentMessageFinder>();
        sent.Setup(finder => finder.FindAsync("sent-1@contoso.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelMessage { CustomerAddress = "ann@example.com" });
        var receiver = CreateReceiver(new RecordingInbox(), deliveryEvents: events.Object, sentMessages: sent.Object);

        var email = Email();
        email.DeliveryReport = new InboundEmailDeliveryReport { OriginalMessageId = "<sent-1@contoso.com>", Recipient = "victim@example.com", Action = "failed", Status = "5.1.1" };

        // Act
        await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        events.Verify(processor => processor.ProcessAsync(It.IsAny<IEnumerable<EmailDeliveryEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveAsync_ADeliveryReportAboutAnEmailThisSiteSentToThatRecipient_IsApplied()
    {
        // Arrange
        var events = new Mock<IEmailDeliveryEventProcessor>();
        var sent = new Mock<IEmailSentMessageFinder>();
        sent.Setup(finder => finder.FindAsync("sent-1@contoso.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelMessage { CustomerAddress = "ann@example.com" });
        var receiver = CreateReceiver(new RecordingInbox(), deliveryEvents: events.Object, sentMessages: sent.Object);

        var email = Email();
        email.DeliveryReport = new InboundEmailDeliveryReport { OriginalMessageId = "<sent-1@contoso.com>", Recipient = "Ann@Example.com", Action = "failed", Status = "5.1.1" };

        // Act
        await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        events.Verify(processor => processor.ProcessAsync(It.IsAny<IEnumerable<EmailDeliveryEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveAsync_ADeliveryReportAboutAnEmailSentWithoutAMessageIdOfOurs_IsApplied()
    {
        // Arrange: Orchard Core's email service cannot set the Message-ID, so its emails are logged without one, and no
        // workspace message carries the id the receiving server reports.
        var events = new Mock<IEmailDeliveryEventProcessor>();
        var log = new InMemoryEmailDeliveryLog();
        log.AddSent("address-support", new DateTime(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc), recipient: "ann@example.com", messageId: null);
        var receiver = CreateReceiver(new RecordingInbox(), deliveryEvents: events.Object, deliveryLog: log);

        var email = Email();
        email.DeliveryReport = new InboundEmailDeliveryReport { OriginalMessageId = "<MKX1@mailkit.local>", Recipient = "Ann@Example.com", Action = "failed", Status = "5.1.1" };

        // Act
        await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert: the bounce suppresses the dead address instead of being dropped as a forgery.
        events.Verify(processor => processor.ProcessAsync(It.IsAny<IEnumerable<EmailDeliveryEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveAsync_ReportsTiedToOneSendByTheRecipient_CountAsOneEventWhateverTheirMessageIds()
    {
        // Arrange: one email to Ann was logged without a Message-ID, and a forger sends the same report twice with
        // different made-up Message-IDs, hoping each counts as another bounce.
        var events = new List<EmailDeliveryEvent>();
        var log = new InMemoryEmailDeliveryLog();
        log.AddSent("address-support", new DateTime(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc), recipient: "ann@example.com", messageId: null);
        var receiver = CreateReceiver(new RecordingInbox(), deliveryEvents: RecordingProcessor(events), deliveryLog: log);

        var first = Email();
        first.DeliveryReport = new InboundEmailDeliveryReport { OriginalMessageId = "<made-up-1@evil.example>", Recipient = "ann@example.com", Action = "failed", Status = "5.1.1" };
        var second = Email();
        second.DeliveryReport = new InboundEmailDeliveryReport { OriginalMessageId = "<made-up-2@evil.example>", Recipient = "ann@example.com", Action = "failed", Status = "5.1.1" };

        // Act
        await receiver.ReceiveAsync(first, "imap", dispatch: true, TestContext.Current.CancellationToken);
        await receiver.ReceiveAsync(second, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert: both name the same logged send, so the delivery event processor recognises the second as a repeat.
        Assert.Equal(2, events.Count);
        Assert.Equal(events[0].EventId, events[1].EventId);
    }

    [Fact]
    public async Task ReceiveAsync_ADeliveryReportAboutAnAIEmailOnlyTheLogHolds_IsApplied()
    {
        // Arrange: the AI's emails go straight to the channel and are logged with their Message-ID, but are not
        // workspace messages.
        var events = new Mock<IEmailDeliveryEventProcessor>();
        var log = new InMemoryEmailDeliveryLog();
        log.AddSent("address-support", new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc), recipient: "ann@example.com", messageId: "ai-1@contoso.com");
        var receiver = CreateReceiver(new RecordingInbox(), deliveryEvents: events.Object, deliveryLog: log);

        var email = Email();
        email.DeliveryReport = new InboundEmailDeliveryReport { OriginalMessageId = "<ai-1@contoso.com>", Recipient = "ann@example.com", Action = "failed", Status = "5.1.1" };

        // Act
        await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        events.Verify(processor => processor.ProcessAsync(It.IsAny<IEnumerable<EmailDeliveryEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveAsync_ADeliveryReportNamingALoggedEmailButAnotherRecipient_IsIgnored()
    {
        // Arrange: the id is one of ours, but that email went to Ann, and an email without an id went to the victim.
        var events = new Mock<IEmailDeliveryEventProcessor>();
        var log = new InMemoryEmailDeliveryLog();
        log.AddSent("address-support", new DateTime(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc), recipient: "ann@example.com", messageId: "ai-1@contoso.com");
        log.AddSent("address-support", new DateTime(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc), recipient: "victim@example.com", messageId: null);
        var receiver = CreateReceiver(new RecordingInbox(), deliveryEvents: events.Object, deliveryLog: log);

        var email = Email();
        email.DeliveryReport = new InboundEmailDeliveryReport { OriginalMessageId = "<ai-1@contoso.com>", Recipient = "victim@example.com", Action = "failed", Status = "5.1.1" };

        // Act
        await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        events.Verify(processor => processor.ProcessAsync(It.IsAny<IEnumerable<EmailDeliveryEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveAsync_ADeliveryReportAboutAnOldEmailWithoutAMessageId_IsIgnored()
    {
        // Arrange: the only email to the recipient is weeks older than any real bounce of it could be.
        var events = new Mock<IEmailDeliveryEventProcessor>();
        var log = new InMemoryEmailDeliveryLog();
        log.AddSent("address-support", new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc), recipient: "ann@example.com", messageId: null);
        var receiver = CreateReceiver(new RecordingInbox(), deliveryEvents: events.Object, deliveryLog: log);

        var email = Email();
        email.DeliveryReport = new InboundEmailDeliveryReport { OriginalMessageId = "<MKX1@mailkit.local>", Recipient = "ann@example.com", Action = "failed", Status = "5.1.1" };

        // Act
        await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        events.Verify(processor => processor.ProcessAsync(It.IsAny<IEnumerable<EmailDeliveryEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("failed", "5.1.1", EmailDeliveryEventKind.HardBounce)]
    [InlineData("failed", "4.2.2", EmailDeliveryEventKind.SoftBounce)]
    [InlineData("delayed", "4.4.1", EmailDeliveryEventKind.Deferred)]
    public async Task ReceiveAsync_AFailedOrDelayedDeliveryReport_CountsAsTheEventItsActionAndStatusSay(string action, string status, EmailDeliveryEventKind expected)
    {
        // Arrange
        var events = new List<EmailDeliveryEvent>();
        var receiver = CreateReceiver(new RecordingInbox(), deliveryEvents: RecordingProcessor(events), sentMessages: SentToAnn());

        var email = Email();
        email.DeliveryReport = new InboundEmailDeliveryReport { ReportType = "delivery-status", OriginalMessageId = "sent-1@contoso.com", Recipient = "ann@example.com", Action = action, Status = status };

        // Act
        await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, Assert.Single(events).Kind);
    }

    [Theory]
    [InlineData("delivery-status", "delivered", "2.0.0")]
    [InlineData("delivery-status", "relayed", "2.0.0")]
    [InlineData("delivery-status", "expanded", "2.0.0")]
    [InlineData("delivery-status", null, "5.1.1")]
    [InlineData("disposition-notification", "failed", "5.1.1")]
    public async Task ReceiveAsync_AReportThatIsNotAFailedOrDelayedDelivery_CountsForNothingAndIsNotReceived(string reportType, string action, string status)
    {
        // Arrange
        var inbox = new RecordingInbox();
        var events = new List<EmailDeliveryEvent>();
        var receiver = CreateReceiver(inbox, deliveryEvents: RecordingProcessor(events), sentMessages: SentToAnn());

        var email = Email();
        email.DeliveryReport = new InboundEmailDeliveryReport { ReportType = reportType, OriginalMessageId = "sent-1@contoso.com", Recipient = "ann@example.com", Action = action, Status = status };

        // Act
        var result = await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert: a success notice or a read receipt is neither a bounce nor a message from the customer.
        Assert.Equal(EmailInboundStatus.Ignored, result.Status);
        Assert.Empty(events);
        Assert.Empty(inbox.Accepted);
    }

    [Fact]
    public async Task ReceiveAsync_AnotherEmailReusingAMessageId_NeverStoresItsFilesOverTheFirstEmailsFiles()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var store = new Mock<IMessagingAttachmentStore>();
        var receiver = CreateReceiver(inbox, attachmentStore: store.Object);

        var first = Email();
        first.Attachments = [new InboundEmailAttachment { FileName = "invoice.pdf", ContentType = "application/pdf", Content = Encoding.ASCII.GetBytes("%PDF-1.4 original") }];
        var second = Email();
        second.Attachments = [new InboundEmailAttachment { FileName = "invoice.pdf", ContentType = "application/pdf", Content = Encoding.ASCII.GetBytes("%PDF-1.4 replaced") }];

        // Act
        await receiver.ReceiveAsync(first, "imap", dispatch: false, TestContext.Current.CancellationToken);
        await receiver.ReceiveAsync(second, "imap", dispatch: false, TestContext.Current.CancellationToken);

        // Assert: the files are stored before the inbox tells the second email is a duplicate, so they must not share
        // an identifier, or the second would replace the first's file.
        var firstId = Assert.Single(JsonSerializer.Deserialize<OmnichannelMessage>(inbox.Accepted[0].Payload).GetAttachments()).Id;
        var secondId = Assert.Single(JsonSerializer.Deserialize<OmnichannelMessage>(inbox.Accepted[1].Payload).GetAttachments()).Id;

        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public async Task ReceiveAsync_ARedeliveryOfTheSameEmail_KeepsTheSameFileIdentifiers()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox, attachmentStore: Mock.Of<IMessagingAttachmentStore>());

        var first = Email();
        first.Attachments = [new InboundEmailAttachment { FileName = "invoice.pdf", ContentType = "application/pdf", Content = Encoding.ASCII.GetBytes("%PDF-1.4 test") }];
        var second = Email();
        second.Attachments = [new InboundEmailAttachment { FileName = "invoice.pdf", ContentType = "application/pdf", Content = Encoding.ASCII.GetBytes("%PDF-1.4 test") }];

        // Act
        await receiver.ReceiveAsync(first, "imap", dispatch: false, TestContext.Current.CancellationToken);
        await receiver.ReceiveAsync(second, "imap", dispatch: false, TestContext.Current.CancellationToken);

        // Assert
        var firstId = Assert.Single(JsonSerializer.Deserialize<OmnichannelMessage>(inbox.Accepted[0].Payload).GetAttachments()).Id;
        var secondId = Assert.Single(JsonSerializer.Deserialize<OmnichannelMessage>(inbox.Accepted[1].Payload).GetAttachments()).Id;

        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task ReceiveAsync_AReplyToNamingSomebodyElse_NeverDecidesWhoTheCustomerIs()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox);

        var email = Email();
        email.From = new InboundEmailAddress("attacker@evil.example", "Mallory");
        email.ReplyTo = new InboundEmailAddress("ann@example.com", "Ann Lee");

        // Act
        await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert: the conversation is with the sender; the Reply-To is only kept for the record.
        var message = JsonSerializer.Deserialize<OmnichannelMessage>(Assert.Single(inbox.Accepted).Payload);
        Assert.Equal("attacker@evil.example", message.CustomerAddress);
        Assert.Equal("ann@example.com", message.GetEmailMetadata().ReplyTo);
    }

    [Fact]
    public async Task ReceiveAsync_AFormOnTheBusinessOwnSite_IsWithTheVisitorNamedInReplyTo()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox, ownAddresses: ["forms@contoso.com"]);

        var email = Email();
        email.From = new InboundEmailAddress("forms@contoso.com", "Website");
        email.ReplyTo = new InboundEmailAddress("Ann@Example.com", "Ann Lee");

        // Act
        var result = await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailInboundStatus.Accepted, result.Status);
        var message = JsonSerializer.Deserialize<OmnichannelMessage>(Assert.Single(inbox.Accepted).Payload);
        Assert.Equal("ann@example.com", message.CustomerAddress);
        Assert.Null(message.GetEmailMetadata().ReplyTo);
    }

    [Fact]
    public async Task ReceiveAsync_AnOutOfOfficeReply_IsMarkedAsAnAutomaticMessage()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox);

        var email = Email();
        email.Headers["Auto-Submitted"] = "auto-replied";

        // Act
        var result = await receiver.ReceiveAsync(email, "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailInboundStatus.Accepted, result.Status);
        var message = JsonSerializer.Deserialize<OmnichannelMessage>(Assert.Single(inbox.Accepted).Payload);
        Assert.True(message.IsAutomatic());
    }

    [Fact]
    public async Task ReceiveAsync_AnEmailAPersonWrote_IsNotMarkedAsAnAutomaticMessage()
    {
        // Arrange
        var inbox = new RecordingInbox();
        var receiver = CreateReceiver(inbox);

        // Act
        await receiver.ReceiveAsync(Email(), "imap", dispatch: true, TestContext.Current.CancellationToken);

        // Assert
        var message = JsonSerializer.Deserialize<OmnichannelMessage>(Assert.Single(inbox.Accepted).Payload);
        Assert.False(message.IsAutomatic());
    }

    private static IEmailDeliveryEventProcessor RecordingProcessor(List<EmailDeliveryEvent> events)
    {
        var processor = new Mock<IEmailDeliveryEventProcessor>();
        processor
            .Setup(value => value.ProcessAsync(It.IsAny<IEnumerable<EmailDeliveryEvent>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<EmailDeliveryEvent> batch, CancellationToken _) => events.AddRange(batch))
            .ReturnsAsync(1);

        return processor.Object;
    }

    private static IEmailSentMessageFinder SentToAnn()
    {
        var sent = new Mock<IEmailSentMessageFinder>();
        sent.Setup(finder => finder.FindAsync("sent-1@contoso.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelMessage { CustomerAddress = "ann@example.com" });

        return sent.Object;
    }

    private static InboundEmail Email()
        => new()
        {
            From = new InboundEmailAddress("Ann@Example.com", "Ann Lee"),
            To = [new InboundEmailAddress("support@contoso.com")],
            Subject = "Order 1042",
            TextBody = "Where is my order?",
            MessageId = "CAF1@mail.example.com",
            InReplyTo = "root@contoso.com",
        };

    private static EmailInboundReceiver CreateReceiver(
        RecordingInbox inbox,
        IReadOnlyCollection<string> ownAddresses = null,
        IMessagingAttachmentStore attachmentStore = null,
        IEmailDeliveryEventProcessor deliveryEvents = null,
        IEmailSentMessageFinder sentMessages = null,
        IEmailDeliveryLog deliveryLog = null)
    {
        var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
        endpointManager
            .Setup(manager => manager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string address, CancellationToken _) =>
                string.Equals(address, "support@contoso.com", StringComparison.OrdinalIgnoreCase)
                    ? _support
                    : ownAddresses?.Contains(address) == true
                        ? new OmnichannelChannelEndpoint { ItemId = "address-own", Value = address, Capabilities = [OmnichannelConstants.Channels.Email] }
                        : null);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));

        return new EmailInboundReceiver(
            endpointManager.Object,
            attachmentStore ?? Mock.Of<IMessagingAttachmentStore>(),
            [inbox],
            [],
            deliveryEvents ?? Mock.Of<IEmailDeliveryEventProcessor>(),
            sentMessages ?? Mock.Of<IEmailSentMessageFinder>(),
            deliveryLog ?? new InMemoryEmailDeliveryLog(),
            clock.Object,
            NullLogger<EmailInboundReceiver>.Instance);
    }

    private sealed class RecordingInbox : IProviderWebhookInbox
    {
        public List<ProviderWebhookInboxDelivery> Accepted { get; } = [];

        public List<string> Dispatched { get; } = [];

        public ProviderWebhookInboxAcceptanceStatus NextStatus { get; set; } = ProviderWebhookInboxAcceptanceStatus.Accepted;

        public Task<ProviderWebhookInboxAcceptanceResult> AcceptAsync(ProviderWebhookInboxDelivery delivery, CancellationToken cancellationToken = default)
        {
            Accepted.Add(delivery);

            return Task.FromResult(new ProviderWebhookInboxAcceptanceResult { Status = NextStatus, MessageId = $"inbox-{Accepted.Count}" });
        }

        public Task<bool> DispatchAsync(string messageId, CancellationToken cancellationToken = default)
        {
            Dispatched.Add(messageId);

            return Task.FromResult(true);
        }

        public Task DispatchHandlerAsync(string handlerName, string payload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> SettleClaimAsync(string messageId, string ownerToken, long fenceToken, bool succeeded, string errorType, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
