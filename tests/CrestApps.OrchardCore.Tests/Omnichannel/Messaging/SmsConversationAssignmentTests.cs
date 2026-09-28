using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public class SmsConversationAssignmentTests
{
    [Fact]
    public async Task ClaimAsync_AssignsPooledConversationToAgent()
    {
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = "queue-1",
            AssignmentStatus = ConversationAssignmentStatus.Pooled,
        };

        var (service, notifier) = CreateService(conversation);

        var result = await service.ClaimAsync("conv-1", "agent-9", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("agent-9", conversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Assigned, conversation.AssignmentStatus);
        Assert.Equal("queue-1", conversation.OwnerId); // queue stays the owner
        notifier.Verify(n => n.ConversationAssignedAsync(It.Is<MessagingAssignmentNotification>(a => a.AssignedAgentId == "agent-9"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimAsync_Fails_WhenAlreadyClaimedByAnotherAgent()
    {
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = "queue-1",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            AssignedAgentId = "agent-owner",
        };

        var (service, notifier) = CreateService(conversation);

        var result = await service.ClaimAsync("conv-1", "agent-intruder", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("agent-owner", conversation.AssignedAgentId);
        notifier.Verify(n => n.ConversationAssignedAsync(It.IsAny<MessagingAssignmentNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignAsync_AssignsPersonalConversationOwnerToAgent()
    {
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            OwnerType = ConversationOwnerType.Personal,
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
        };

        var (service, _) = CreateService(conversation);

        var result = await service.AssignAsync("conv-1", "agent-5", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("agent-5", conversation.AssignedAgentId);
        Assert.Equal("agent-5", conversation.OwnerId);
        Assert.Equal(ConversationAssignmentStatus.Assigned, conversation.AssignmentStatus);
    }

    private static (MessagingConversationService Service, Mock<IMessagingRealTimeNotifier> Notifier) CreateService(MessagingConversation conversation)
    {
        var store = new Mock<IMessagingConversationStore>();
        store.Setup(s => s.FindByIdAsync(conversation.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);
        store.Setup(s => s.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var notifier = new Mock<IMessagingRealTimeNotifier>();

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        var service = new MessagingConversationService(
            store.Object,
            MessagingTestChannels.Resolver(new Mock<ISmsDispatcher>().Object),
            new Mock<IContentManager>().Object,
            new Mock<IMessagingContactResolver>().Object,
            notifier.Object,
            Mock.Of<IMessagingConversationAuthorizationService>(),
            new Mock<ISession>().Object,
            new NoOpSmsFirstResponseSlaService(),
            clock.Object,
            NullLogger<MessagingConversationService>.Instance);

        return (service, notifier);
    }
}
