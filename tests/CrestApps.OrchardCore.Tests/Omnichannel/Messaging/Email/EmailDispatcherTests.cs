using System.Text;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailDispatcherTests
{
    [Fact]
    public async Task SendAsync_AReply_ThreadsUnderTheCustomersEmail()
    {
        // Arrange
        var transport = new RecordingTransport();
        var dispatcher = CreateDispatcher(transport, Address());

        var inbound = new OmnichannelMessage { IsInbound = true, ProviderMessageId = "CAF1@mail.example.com" };
        inbound.SetSubject("Order 1042");
        inbound.Put(new EmailMessageMetadata { MessageId = "CAF1@mail.example.com", References = ["root@contoso.com"] });

        // Act
        var result = await dispatcher.SendAsync(new MessagingOutboundMessage
        {
            ServiceAddress = "Support@Contoso.com",
            ContactAddress = "Ann@Example.com",
            ReplyTo = inbound,
            Purpose = MessagingOutboundPurpose.Reply,
            Body = "It ships today.",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        var sent = Assert.Single(transport.Sent);
        Assert.Equal("support@contoso.com", sent.FromAddress);
        Assert.Equal("Contoso Support", sent.FromName);
        Assert.Equal("ann@example.com", sent.ToAddress);
        Assert.Equal("Re: Order 1042", sent.Subject);
        Assert.Equal("CAF1@mail.example.com", sent.InReplyTo);
        Assert.Equal(["root@contoso.com", "CAF1@mail.example.com"], sent.References);
        Assert.EndsWith("@contoso.com", sent.MessageId);
        Assert.Contains("It ships today.", sent.TextBody);
        Assert.Contains("-- \r\nThe Contoso team", sent.TextBody);
        Assert.False(sent.Headers.ContainsKey("List-Unsubscribe"));
        Assert.False(sent.Headers.ContainsKey("Auto-Submitted"));
    }

    [Fact]
    public async Task SendAsync_AnAutomatedReply_IsMarkedAutoRepliedSoTheCustomersAutoResponderStaysSilent()
    {
        // Arrange
        var transport = new RecordingTransport();
        var dispatcher = CreateDispatcher(transport, Address());

        // Act
        await dispatcher.SendAsync(Message(MessagingOutboundPurpose.Automation), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("auto-replied", Assert.Single(transport.Sent).Headers["Auto-Submitted"]);
    }

    [Fact]
    public async Task SendAsync_ABroadcast_CarriesTheOneClickUnsubscribeAndStartsItsOwnThread()
    {
        // Arrange
        var transport = new RecordingTransport();
        var links = new Mock<IEmailUnsubscribeLinks>();
        links
            .Setup(value => value.CreateUrlAsync("ann@example.com", "support@contoso.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://contoso.com/omnichannel/email/unsubscribe/t1");

        var dispatcher = CreateDispatcher(transport, Address(), unsubscribeLinks: links.Object);

        var inbound = new OmnichannelMessage { IsInbound = true };
        inbound.Put(new EmailMessageMetadata { MessageId = "old@example.com" });

        var message = Message(MessagingOutboundPurpose.Broadcast);
        message.ReplyTo = inbound;
        message.Subject = "October news";

        // Act
        await dispatcher.SendAsync(message, TestContext.Current.CancellationToken);

        // Assert
        var sent = Assert.Single(transport.Sent);
        Assert.Equal("October news", sent.Subject);
        Assert.Null(sent.InReplyTo);
        Assert.Equal("<https://contoso.com/omnichannel/email/unsubscribe/t1>", sent.Headers["List-Unsubscribe"]);
        Assert.Equal("List-Unsubscribe=One-Click", sent.Headers["List-Unsubscribe-Post"]);
        Assert.Equal("bulk", sent.Headers["Precedence"]);
        Assert.Contains("https://contoso.com/omnichannel/email/unsubscribe/t1", sent.HtmlBody);
    }

    [Fact]
    public async Task SendAsync_WhenTheAddressTurnsUnsubscribeLinksOff_SendsNone()
    {
        // Arrange
        var transport = new RecordingTransport();
        var links = new Mock<IEmailUnsubscribeLinks>();
        var dispatcher = CreateDispatcher(transport, Address(settings => settings.IncludeUnsubscribeLink = false), unsubscribeLinks: links.Object);

        // Act
        await dispatcher.SendAsync(Message(MessagingOutboundPurpose.Outreach), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Assert.Single(transport.Sent).Headers.ContainsKey("List-Unsubscribe"));
        links.Verify(value => value.CreateUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_ANewEmailWithNoSubject_UsesTheAddressDefaultSubject()
    {
        // Arrange
        var transport = new RecordingTransport();
        var dispatcher = CreateDispatcher(transport, Address(settings => settings.DefaultSubject = "A message from Contoso"));

        // Act
        await dispatcher.SendAsync(Message(MessagingOutboundPurpose.Reply), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("A message from Contoso", Assert.Single(transport.Sent).Subject);
    }

    [Fact]
    public async Task SendAsync_AttachesTheFilesFromTheAttachmentStore()
    {
        // Arrange
        var transport = new RecordingTransport();
        var store = new Mock<IMessagingAttachmentStore>();
        store.Setup(value => value.ReadAsync("file-1", It.IsAny<CancellationToken>())).ReturnsAsync(Encoding.ASCII.GetBytes("%PDF-1.4"));

        var dispatcher = CreateDispatcher(transport, Address(), attachmentStore: store.Object);

        var message = Message(MessagingOutboundPurpose.Reply);
        message.Attachments = [new MessagingAttachment { Id = "file-1", ContentType = "application/pdf", FileName = "invoice.pdf", Length = 8 }];

        // Act
        await dispatcher.SendAsync(message, TestContext.Current.CancellationToken);

        // Assert
        var attachment = Assert.Single(Assert.Single(transport.Sent).Attachments);
        Assert.Equal("invoice.pdf", attachment.FileName);
        Assert.Equal("%PDF-1.4", Encoding.ASCII.GetString(attachment.Content));
    }

    [Fact]
    public async Task SendAsync_FromAnAddressThatIsNotUsedForEmail_IsRefusedNotThrown()
    {
        // Arrange
        var transport = new RecordingTransport();
        var dispatcher = CreateDispatcher(transport, address: null);

        // Act
        var result = await dispatcher.SendAsync(Message(MessagingOutboundPurpose.Reply), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task SendAsync_ThroughATransportNoFeatureProvides_IsRefused()
    {
        // Arrange
        var transport = new RecordingTransport();
        var dispatcher = CreateDispatcher(transport, Address(settings => settings.TransportName = "SendGridApi"));

        // Act
        var result = await dispatcher.SendAsync(Message(MessagingOutboundPurpose.Reply), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(transport.Sent);
    }

    private static MessagingOutboundMessage Message(MessagingOutboundPurpose purpose)
        => new()
        {
            ServiceAddress = "support@contoso.com",
            ContactAddress = "ann@example.com",
            Purpose = purpose,
            Body = "Hello",
        };

    private static OmnichannelChannelEndpoint Address(Action<EmailAddressSettings> configure = null)
    {
        var address = new OmnichannelChannelEndpoint
        {
            ItemId = "address-1",
            DisplayText = "Support",
            Value = "support@contoso.com",
            AddressType = OmnichannelAddressTypes.EmailAddress,
            Capabilities = [OmnichannelConstants.Channels.Email],
        };

        var settings = new EmailAddressSettings
        {
            SenderName = "Contoso Support",
            Signature = "The Contoso team",
            TransportName = RecordingTransport.TransportName,
        };

        configure?.Invoke(settings);
        address.Put(settings);

        return address;
    }

    private static EmailDispatcher CreateDispatcher(
        RecordingTransport transport,
        OmnichannelChannelEndpoint address,
        IEmailUnsubscribeLinks unsubscribeLinks = null,
        IMessagingAttachmentStore attachmentStore = null,
        IEmailSendingGovernor governor = null)
    {
        var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
        endpointManager
            .Setup(manager => manager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Email, "support@contoso.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        return new EmailDispatcher(
            endpointManager.Object,
            [transport],
            attachmentStore ?? Mock.Of<IMessagingAttachmentStore>(),
            unsubscribeLinks ?? Mock.Of<IEmailUnsubscribeLinks>(),
            governor ?? AllowingGovernor(),
            Mock.Of<ISession>(),
            NullLogger<EmailDispatcher>.Instance,
            new PassThroughStringLocalizer<EmailDispatcher>());
    }

    private static IEmailSendingGovernor AllowingGovernor()
    {
        var governor = new Mock<IEmailSendingGovernor>();
        governor
            .Setup(value => value.EvaluateAsync(It.IsAny<OmnichannelChannelEndpoint>(), It.IsAny<EmailAddressSettings>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailSendDecision.Allow());

        return governor.Object;
    }

    private sealed class RecordingTransport : IEmailTransport
    {
        public const string TransportName = "Recording";

        public List<EmailTransportMessage> Sent { get; } = [];

        public string Name => TransportName;

        public LocalizedString DisplayName => new(TransportName, TransportName);

        public bool SupportsHeaders => true;

        public Task<MessageDispatchResult> SendAsync(EmailTransportMessage message, EmailAddressSettings settings, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);

            return Task.FromResult(MessageDispatchResult.Success(message.MessageId));
        }
    }
}
