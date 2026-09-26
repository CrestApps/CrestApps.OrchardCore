using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public class SmsRoutedReassignmentServiceTests
{
    [Fact]
    public async Task ReRoutesStaleConversation_ToAnotherAgent_WhenOneIsAvailable()
    {
        var now = DateTime.UtcNow;
        var stale = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv1",
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = "q1",
            AssignedAgentId = "a1",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            AssignedUtc = now - MessagingRoutedReassignmentService.PickupGraceWindow - TimeSpan.FromMinutes(1),
        };

        var harness = new Harness(now, stale) { NextAgentId = "a2" };

        var count = await harness.Service.ReassignStaleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal(ConversationAssignmentStatus.Assigned, stale.AssignmentStatus);
        Assert.Equal("a2", stale.AssignedAgentId);
        Assert.Equal(now, stale.AssignedUtc);
        // The one who did not pick it up is excluded from re-selection.
        harness.Strategy.Verify(s => s.SelectAgentAsync("q1", "a1", It.IsAny<CancellationToken>()), Times.Once);
        harness.Notifier.Verify(n => n.NewInboundMessageAsync(
            It.Is<MessagingInboundNotification>(x => x.ConversationId == "conv1" && x.AssignedAgentId == "a2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepoolsStaleRoutedConversation_WhenNoOtherAgentAvailable_AndNotifiesTheQueue()
    {
        var now = DateTime.UtcNow;
        var stale = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv1",
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = "q1",
            AssignedAgentId = "a1",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            AssignedUtc = now - MessagingRoutedReassignmentService.PickupGraceWindow - TimeSpan.FromMinutes(1),
        };

        var harness = new Harness(now, stale) { NextAgentId = null };

        var count = await harness.Service.ReassignStaleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal(ConversationAssignmentStatus.Pooled, stale.AssignmentStatus);
        Assert.Null(stale.AssignedAgentId);
        Assert.Null(stale.AssignedUtc);
        harness.Store.Verify(s => s.UpdateAsync(stale, It.IsAny<CancellationToken>()), Times.Once);
        harness.Notifier.Verify(n => n.NewInboundMessageAsync(
            It.Is<MessagingInboundNotification>(x => x.ConversationId == "conv1" && x.OwnerQueueId == "q1" && x.AssignedAgentId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Repools_WhenReassignmentAttemptsExhausted_EvenIfAnAgentIsAvailable()
    {
        var now = DateTime.UtcNow;
        var stale = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv1",
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = "q1",
            AssignedAgentId = "a1",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            AssignedUtc = now - MessagingRoutedReassignmentService.PickupGraceWindow - TimeSpan.FromMinutes(1),
            ReassignmentAttempts = MessagingRoutedReassignmentService.MaxReassignmentAttempts,
        };

        var harness = new Harness(now, stale) { NextAgentId = "a2" };

        var count = await harness.Service.ReassignStaleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal(ConversationAssignmentStatus.Pooled, stale.AssignmentStatus);
        Assert.Null(stale.AssignedAgentId);
        Assert.Equal(0, stale.ReassignmentAttempts);
        // The strategy is not even consulted once the attempts are exhausted.
        harness.Strategy.Verify(s => s.SelectAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LeavesFreshRoutedConversation_Untouched()
    {
        var now = DateTime.UtcNow;
        var fresh = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv1",
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = "q1",
            AssignedAgentId = "a1",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            AssignedUtc = now - TimeSpan.FromMinutes(1),
        };

        var harness = new Harness(now, fresh);

        var count = await harness.Service.ReassignStaleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, count);
        Assert.Equal(ConversationAssignmentStatus.Assigned, fresh.AssignmentStatus);
        Assert.Equal("a1", fresh.AssignedAgentId);
        harness.Store.Verify(s => s.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Harness
    {
        public Mock<IMessagingConversationStore> Store { get; } = new();

        public Mock<IMessagingRoutingStrategy> Strategy { get; } = new();

        public Mock<IMessagingRealTimeNotifier> Notifier { get; } = new();

        public string NextAgentId { get; init; }

        public MessagingRoutedReassignmentService Service { get; }

        public Harness(DateTime now, params MessagingConversation[] awaitingPickup)
        {
            Store.Setup(s => s.GetRoutedAwaitingPickupAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(awaitingPickup);
            Store.Setup(s => s.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            Strategy.Setup(s => s.SelectAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => NextAgentId);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(now);

            var router = new MessagingConversationRouter(
                [new ReassignmentRouter(Strategy.Object, clock.Object)],
                MessagingTestChannels.Resolver(MessagingTestChannels.AcceptingDispatcher().Object),
                NullLogger<MessagingConversationRouter>.Instance);

            Service = new MessagingRoutedReassignmentService(
                Store.Object,
                router,
                Notifier.Object,
                clock.Object,
                Microsoft.Extensions.Options.Options.Create(new MessagingRoutedDistributionOptions()),
                NullLogger<MessagingRoutedReassignmentService>.Instance);
        }
    }
}
