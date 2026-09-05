using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.Notifications;
using CrestApps.OrchardCore.Sms.Workspace.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class SmsConversationAssignmentTests
{
    [Fact]
    public async Task ClaimAsync_AssignsPooledConversationToAgent()
    {
        var conversation = new SmsConversation
        {
            ItemId = "conv-1",
            OwnerType = SmsConversationOwnerType.Queue,
            OwnerId = "queue-1",
            AssignmentStatus = SmsConversationAssignmentStatus.Pooled,
        };

        var (service, notifier) = CreateService(conversation);

        var result = await service.ClaimAsync("conv-1", "agent-9", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("agent-9", conversation.AssignedAgentId);
        Assert.Equal(SmsConversationAssignmentStatus.Assigned, conversation.AssignmentStatus);
        Assert.Equal("queue-1", conversation.OwnerId); // queue stays the owner
        notifier.Verify(n => n.ConversationAssignedAsync(It.Is<SmsAssignmentNotification>(a => a.AssignedAgentId == "agent-9"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimAsync_Fails_WhenAlreadyClaimedByAnotherAgent()
    {
        var conversation = new SmsConversation
        {
            ItemId = "conv-1",
            OwnerType = SmsConversationOwnerType.Queue,
            OwnerId = "queue-1",
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
            AssignedAgentId = "agent-owner",
        };

        var (service, notifier) = CreateService(conversation);

        var result = await service.ClaimAsync("conv-1", "agent-intruder", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("agent-owner", conversation.AssignedAgentId);
        notifier.Verify(n => n.ConversationAssignedAsync(It.IsAny<SmsAssignmentNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignAsync_AssignsPersonalConversationOwnerToAgent()
    {
        var conversation = new SmsConversation
        {
            ItemId = "conv-1",
            OwnerType = SmsConversationOwnerType.Personal,
            AssignmentStatus = SmsConversationAssignmentStatus.Unassigned,
        };

        var (service, _) = CreateService(conversation);

        var result = await service.AssignAsync("conv-1", "agent-5", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("agent-5", conversation.AssignedAgentId);
        Assert.Equal("agent-5", conversation.OwnerId);
        Assert.Equal(SmsConversationAssignmentStatus.Assigned, conversation.AssignmentStatus);
    }

    private static (SmsConversationService Service, Mock<ISmsRealTimeNotifier> Notifier) CreateService(SmsConversation conversation)
    {
        var store = new Mock<ISmsConversationStore>();
        store.Setup(s => s.FindByIdAsync(conversation.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);
        store.Setup(s => s.UpdateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var notifier = new Mock<ISmsRealTimeNotifier>();

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        var service = new SmsConversationService(
            store.Object,
            new Mock<ISmsDispatcher>().Object,
            new Mock<IContentManager>().Object,
            new Mock<ISmsContactResolver>().Object,
            notifier.Object,
            new Mock<ISession>().Object,
            clock.Object,
            RedactorProviderFactory.Create(),
            NullLogger<SmsConversationService>.Instance);

        return (service, notifier);
    }
}
