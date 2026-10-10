using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

/// <summary>
/// The workspace's send path on a channel whose messages carry a subject and embed their files, as email does: the
/// subject is kept on the message so the thread shows it and a retry resends it, and the channel is told which
/// conversation it replies on so it can thread the reply.
/// </summary>
public sealed class MessagingSubjectChannelTests
{
    [Fact]
    public async Task SendAsync_OnASubjectChannel_KeepsTheSubjectAndTellsTheChannelTheConversation()
    {
        // Arrange
        var channel = new RecordingChannel(supportsSubject: true);
        var (service, saved) = CreateService(channel, Conversation(channel.Name));

        // Act
        var result = await service.SendAsync(new MessagingSendRequest
        {
            ConversationId = "conv-1",
            Subject = "Re: Order 1042",
            Body = "It ships today.",
            ActingAgentId = "agent-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Re: Order 1042", Assert.Single(saved).GetSubject());

        var sent = Assert.Single(channel.Sent);
        Assert.Equal("Re: Order 1042", sent.Subject);
        Assert.Equal("conv-1", sent.ConversationId);
        Assert.Equal(MessagingOutboundPurpose.Reply, sent.Purpose);
    }

    [Fact]
    public async Task SendAsync_OnAChannelWithoutSubjects_DropsTheSubject()
    {
        // Arrange
        var channel = new RecordingChannel(supportsSubject: false);
        var (service, saved) = CreateService(channel, Conversation(channel.Name));

        // Act
        await service.SendAsync(new MessagingSendRequest { ConversationId = "conv-1", Subject = "Ignored", Body = "Hi" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(Assert.Single(saved).GetSubject());
        Assert.Null(Assert.Single(channel.Sent).Subject);
    }

    [Fact]
    public async Task SendAsync_OnAChannelThatEmbedsFiles_SendsThemWithoutNeedingAPublicLink()
    {
        // Arrange
        var channel = new RecordingChannel(supportsSubject: true);
        var (service, _) = CreateService(channel, Conversation(channel.Name), attachmentUrlProvider: new FakeAttachmentUrlProvider { HasPublicAddress = false });

        var attachment = new MessagingAttachment { Id = "file-1", ContentType = "application/pdf", FileName = "invoice.pdf", Length = 10 };

        // Act
        var result = await service.SendAsync(new MessagingSendRequest
        {
            ConversationId = "conv-1",
            Body = "Attached.",
            Attachments = [attachment],
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        var sent = Assert.Single(channel.Sent);
        Assert.Empty(sent.MediaUrls);
        Assert.Equal("file-1", Assert.Single(sent.Attachments).Id);
    }

    [Fact]
    public async Task SendDirectAsync_WithARequest_SendsTheSubjectAndThePurpose()
    {
        // Arrange
        var channel = new RecordingChannel(supportsSubject: true);
        var (service, saved) = CreateService(channel, conversation: null);

        // Act
        var result = await service.SendDirectAsync(new MessagingDirectSendRequest
        {
            Channel = channel.Name,
            ServiceAddress = "support@contoso.com",
            ContactAddress = "ann@example.com",
            Subject = "October news",
            Body = "Hello",
            Purpose = MessagingOutboundPurpose.Broadcast,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("October news", Assert.Single(saved).GetSubject());

        var sent = Assert.Single(channel.Sent);
        Assert.Equal("October news", sent.Subject);
        Assert.Equal(MessagingOutboundPurpose.Broadcast, sent.Purpose);
        Assert.True(Assert.Single(saved).TryGet<OutboundDeliveryState>(out var state) && state.Purpose == MessagingOutboundPurpose.Broadcast);
    }

    [Fact]
    public void CanRetry_IsFalse_WhenTheProviderRejectedTheRecipient()
    {
        // A hard bounce is refused on every attempt; retrying only spends the sending address's reputation.
        Assert.False(OutboundDeliveryState.CanRetry(1, OmnichannelConstants.MessagingErrorCodes.RecipientRejected));
    }

    private static MessagingConversation Conversation(string channel)
        => new()
        {
            ItemId = "conv-1",
            Channel = channel,
            ServiceAddress = "support@contoso.com",
            ContactAddress = "ann@example.com",
            OwnerType = ConversationOwnerType.Personal,
        };

    private static (MessagingConversationService Service, List<OmnichannelMessage> Saved) CreateService(
        RecordingChannel channel,
        MessagingConversation conversation,
        IMessagingAttachmentUrlProvider attachmentUrlProvider = null)
    {
        var saved = new List<OmnichannelMessage>();

        var store = new Mock<IMessagingConversationStore>();

        if (conversation is not null)
        {
            store.Setup(value => value.FindByIdAsync(conversation.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);
        }

        var session = new Mock<ISession>();
        session
            .Setup(value => value.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<object, bool, string, CancellationToken>((message, _, _, _) => saved.Add((OmnichannelMessage)message));

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));

        var service = new MessagingConversationService(
            store.Object,
            new MessagingChannelResolver([channel]),
            Mock.Of<IContentManager>(),
            Mock.Of<IMessagingContactResolver>(),
            Mock.Of<IMessagingRealTimeNotifier>(),
            Mock.Of<IAuthorizationService>(),
            session.Object,
            new NoOpSmsFirstResponseSlaService(),
            attachmentUrlProvider ?? new FakeAttachmentUrlProvider(),
            clock.Object,
            NullLogger<MessagingConversationService>.Instance);

        return (service, saved);
    }

    private sealed class RecordingChannel : IMessagingChannel
    {
        private readonly MessagingChannelCapabilities _capabilities;

        public RecordingChannel(bool supportsSubject)
        {
            _capabilities = new MessagingChannelCapabilities
            {
                SupportsSubject = supportsSubject,
                Attachments = new MessagingAttachmentCapabilities
                {
                    Formats = MessagingFileFormats.All,
                    DeliveredAsLinks = false,
                },
            };
        }

        public List<MessagingOutboundMessage> Sent { get; } = [];

        public string Name => "Letters";

        public LocalizedString DisplayName => new("Letters", "Letters");

        public string IconCssClass => "fa-solid fa-envelope";

        public int Order => 10;

        public MessagingChannelCapabilities Capabilities => _capabilities;

        public string NormalizeAddress(string address) => address?.Trim().ToLowerInvariant();

        public string FormatAddress(string address) => address;

        public bool IsValidAddress(string address) => !string.IsNullOrWhiteSpace(address);

        public Task<MessageDispatchResult> SendAsync(MessagingOutboundMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);

            return Task.FromResult(MessageDispatchResult.Success("m-1"));
        }

        public bool IsOptedOut(ContentItem contact) => false;

        public IReadOnlyList<string> GetContactAddresses(ContentItem contact) => [];

        public Task<IReadOnlyList<string>> FindContactIdsAsync(string address, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> SearchContactIdsByAddressAsync(string term, IReadOnlyCollection<string> contactTypes, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
