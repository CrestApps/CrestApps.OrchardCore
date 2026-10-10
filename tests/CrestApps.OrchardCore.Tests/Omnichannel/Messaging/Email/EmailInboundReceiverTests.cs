using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
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
        IMessagingAttachmentStore attachmentStore = null)
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
            Mock.Of<IMessagingConversationService>(),
            Mock.Of<ISession>(),
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
