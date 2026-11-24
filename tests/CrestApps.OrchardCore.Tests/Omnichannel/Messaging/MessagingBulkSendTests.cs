using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
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

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Bulk mail (a broadcast, or outreach) opens no conversation, so a broadcast to ten thousand people does not leave ten
/// thousand open threads assigned to the agent who sent it; a conversation opens when a recipient replies. A message the
/// channel refuses for good to someone with no conversation opens none either.
/// </summary>
public sealed class MessagingBulkSendTests
{
    [Theory]
    [InlineData(MessagingOutboundPurpose.Broadcast)]
    [InlineData(MessagingOutboundPurpose.Outreach)]
    public async Task SendDirectAsync_BulkMailToAContactWithNoConversation_SendsAndKeepsTheMessage_WithoutOpeningOne(MessagingOutboundPurpose purpose)
    {
        // Arrange
        var channel = new RecordingChannel();
        var harness = new Harness(channel, existing: null);

        // Act
        var result = await harness.Service.SendDirectAsync(Request(purpose), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        harness.Store.Verify(store => store.CreateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Store.Verify(store => store.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);

        var message = Assert.Single(harness.Saved);
        Assert.Same(message, result.Message);
        Assert.Null(message.ConversationId);
        Assert.Equal("ann@example.com", message.CustomerAddress);
        Assert.Equal(MessageDeliveryStatus.Sent.ToString(), message.DeliveryStatus);
        Assert.Null(Assert.Single(channel.Sent).ConversationId);
    }

    [Fact]
    public async Task SendDirectAsync_BulkMailToAContactWithAConversation_JoinsIt_WithoutReopeningMarkingReadOrTakingIt()
    {
        // Arrange
        var existing = new MessagingConversation
        {
            ItemId = "conv-1",
            Channel = "Letters",
            ServiceAddress = "support@contoso.com",
            ContactAddress = "ann@example.com",
            Status = ConversationStatus.Closed,
            OwnerType = ConversationOwnerType.Personal,
            OwnerId = "agent-2",
            AssignedAgentId = "agent-2",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            IsRead = false,
            UnreadCount = 2,
        };

        var channel = new RecordingChannel();
        var harness = new Harness(channel, existing);

        // Act
        var result = await harness.Service.SendDirectAsync(Request(MessagingOutboundPurpose.Broadcast), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("conv-1", Assert.Single(harness.Saved).ConversationId);
        Assert.Equal("conv-1", Assert.Single(channel.Sent).ConversationId);
        Assert.Equal(
            (ConversationStatus.Closed, "agent-2", "agent-2", false, 2),
            (existing.Status, existing.OwnerId, existing.AssignedAgentId, existing.IsRead, existing.UnreadCount));

        harness.Store.Verify(store => store.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        harness.Store.Verify(store => store.CreateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendDirectAsync_ABroadcastMessageTheChannelHoldsBack_IsKeptQueued_WithItsBroadcast_WithoutAConversation()
    {
        // Arrange
        var retryAt = new DateTime(2026, 10, 9, 13, 0, 0, DateTimeKind.Utc);
        var channel = new RecordingChannel
        {
            NextResult = MessageDispatchResult.Deferred(retryAt, new LocalizedString("held", "The address is at its sending limit.")),
        };

        var harness = new Harness(channel, existing: null);

        // Act
        var result = await harness.Service.SendDirectAsync(Request(MessagingOutboundPurpose.Broadcast, broadcastId: "bc-1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);

        var message = Assert.Single(harness.Saved);
        Assert.Same(message, result.Message);
        Assert.Null(message.ConversationId);
        Assert.Equal(MessageDeliveryStatus.Queued.ToString(), message.DeliveryStatus);
        Assert.True(message.TryGet<MessagingBroadcastLink>(out var link));
        Assert.Equal("bc-1", link.BroadcastId);
        harness.Store.Verify(store => store.CreateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(MessagingOutboundPurpose.Reply)]
    [InlineData(MessagingOutboundPurpose.Broadcast)]
    public async Task SendDirectAsync_RefusedForGoodToAContactWithNoConversation_KeepsNothing_AndReturnsTheError(MessagingOutboundPurpose purpose)
    {
        // Arrange
        var channel = new RecordingChannel
        {
            NextResult = new MessageDispatchResult
            {
                Succeeded = false,
                ErrorCode = OmnichannelConstants.MessagingErrorCodes.RecipientRejected,
                Errors = [new LocalizedString("suppressed", "ann@example.com is on the suppression list.")],
            },
        };

        var harness = new Harness(channel, existing: null);

        // Act
        var result = await harness.Service.SendDirectAsync(Request(purpose), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.Message);
        Assert.Equal("ann@example.com is on the suppression list.", result.Error);
        Assert.Empty(harness.Saved);
        harness.Store.Verify(store => store.CreateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendDirectAsync_AReplyTheProviderCannotTakeYet_StillOpensTheConversation_WithTheMessageQueuedForARetry()
    {
        // Arrange
        var channel = new RecordingChannel
        {
            NextResult = MessageDispatchResult.Failed("The provider is unreachable."),
        };

        var harness = new Harness(channel, existing: null);

        // Act
        var result = await harness.Service.SendDirectAsync(Request(MessagingOutboundPurpose.Reply), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);

        var message = Assert.Single(harness.Saved);
        Assert.Equal(MessageDeliveryStatus.Queued.ToString(), message.DeliveryStatus);
        Assert.False(string.IsNullOrEmpty(message.ConversationId));
        harness.Store.Verify(
            store => store.CreateAsync(It.Is<MessagingConversation>(conversation => conversation.ItemId == message.ConversationId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static MessagingDirectSendRequest Request(MessagingOutboundPurpose purpose, string broadcastId = null)
        => new()
        {
            Channel = "Letters",
            ServiceAddress = "support@contoso.com",
            ContactAddress = "Ann@Example.com",
            Subject = "October news",
            Body = "Hello",
            ActingAgentId = "agent-1",
            Purpose = purpose,
            BroadcastId = broadcastId,
        };

    private sealed class Harness
    {
        public Harness(RecordingChannel channel, MessagingConversation existing)
        {
            Store
                .Setup(store => store.FindByContactAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

            var session = new Mock<ISession>();
            session
                .Setup(value => value.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<object, bool, string, CancellationToken>((message, _, _, _) => Saved.Add((OmnichannelMessage)message));

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));

            Service = new MessagingConversationService(
                Store.Object,
                new MessagingChannelResolver([channel]),
                Mock.Of<IContentManager>(),
                Mock.Of<IMessagingContactResolver>(),
                Mock.Of<IMessagingRealTimeNotifier>(),
                Mock.Of<IAuthorizationService>(),
                session.Object,
                new NoOpSmsFirstResponseSlaService(),
                new FakeAttachmentUrlProvider(),
                clock.Object,
                NullLogger<MessagingConversationService>.Instance);
        }

        public Mock<IMessagingConversationStore> Store { get; } = new();

        public List<OmnichannelMessage> Saved { get; } = [];

        public MessagingConversationService Service { get; }
    }

    private sealed class RecordingChannel : IMessagingChannel
    {
        private static readonly MessagingChannelCapabilities _capabilities = new()
        {
            SupportsSubject = true,
        };

        public List<MessagingOutboundMessage> Sent { get; } = [];

        public MessageDispatchResult NextResult { get; set; }

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

            return Task.FromResult(NextResult ?? MessageDispatchResult.Success("m-1"));
        }

        public Task<bool> IsOptedOutAsync(ContentItem contact, string address, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public IReadOnlyList<string> GetContactAddresses(ContentItem contact) => [];

        public Task<IReadOnlyList<string>> FindContactIdsAsync(string address, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> SearchContactIdsByAddressAsync(string term, IReadOnlyCollection<string> contactTypes, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
