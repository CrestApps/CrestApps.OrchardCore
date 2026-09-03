using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routing;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class SmsRoutedReassignmentServiceTests
{
    [Fact]
    public async Task ReRoutesStaleConversation_ToAnotherAgent_WhenOneIsAvailable()
    {
        var now = DateTime.UtcNow;
        var stale = new SmsConversation
        {
            ItemId = "conv1",
            OwnerType = SmsConversationOwnerType.Queue,
            OwnerId = "q1",
            AssignedAgentId = "a1",
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
            AssignedUtc = now - SmsRoutedReassignmentService.PickupGraceWindow - TimeSpan.FromMinutes(1),
        };

        var harness = new Harness(now, stale) { NextAgentId = "a2" };

        var count = await harness.Service.ReassignStaleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal(SmsConversationAssignmentStatus.Assigned, stale.AssignmentStatus);
        Assert.Equal("a2", stale.AssignedAgentId);
        Assert.Equal(now, stale.AssignedUtc);
        // The one who did not pick it up is excluded from re-selection.
        harness.Strategy.Verify(s => s.SelectAgentAsync("q1", "a1", It.IsAny<CancellationToken>()), Times.Once);
        harness.Notifier.Verify(n => n.NewInboundMessageAsync(
            It.Is<SmsInboundNotification>(x => x.ConversationId == "conv1" && x.AssignedAgentId == "a2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepoolsStaleRoutedConversation_WhenNoOtherAgentAvailable_AndNotifiesTheQueue()
    {
        var now = DateTime.UtcNow;
        var stale = new SmsConversation
        {
            ItemId = "conv1",
            OwnerType = SmsConversationOwnerType.Queue,
            OwnerId = "q1",
            AssignedAgentId = "a1",
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
            AssignedUtc = now - SmsRoutedReassignmentService.PickupGraceWindow - TimeSpan.FromMinutes(1),
        };

        var harness = new Harness(now, stale) { NextAgentId = null };

        var count = await harness.Service.ReassignStaleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal(SmsConversationAssignmentStatus.Pooled, stale.AssignmentStatus);
        Assert.Null(stale.AssignedAgentId);
        Assert.Null(stale.AssignedUtc);
        harness.Store.Verify(s => s.UpdateAsync(stale, It.IsAny<CancellationToken>()), Times.Once);
        harness.Notifier.Verify(n => n.NewInboundMessageAsync(
            It.Is<SmsInboundNotification>(x => x.ConversationId == "conv1" && x.OwnerQueueId == "q1" && x.AssignedAgentId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Repools_WhenReassignmentAttemptsExhausted_EvenIfAnAgentIsAvailable()
    {
        var now = DateTime.UtcNow;
        var stale = new SmsConversation
        {
            ItemId = "conv1",
            OwnerType = SmsConversationOwnerType.Queue,
            OwnerId = "q1",
            AssignedAgentId = "a1",
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
            AssignedUtc = now - SmsRoutedReassignmentService.PickupGraceWindow - TimeSpan.FromMinutes(1),
            ReassignmentAttempts = SmsRoutedReassignmentService.MaxReassignmentAttempts,
        };

        var harness = new Harness(now, stale) { NextAgentId = "a2" };

        var count = await harness.Service.ReassignStaleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal(SmsConversationAssignmentStatus.Pooled, stale.AssignmentStatus);
        Assert.Null(stale.AssignedAgentId);
        Assert.Equal(0, stale.ReassignmentAttempts);
        // The strategy is not even consulted once the attempts are exhausted.
        harness.Strategy.Verify(s => s.SelectAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LeavesFreshRoutedConversation_Untouched()
    {
        var now = DateTime.UtcNow;
        var fresh = new SmsConversation
        {
            ItemId = "conv1",
            OwnerType = SmsConversationOwnerType.Queue,
            OwnerId = "q1",
            AssignedAgentId = "a1",
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
            AssignedUtc = now - TimeSpan.FromMinutes(1),
        };

        var harness = new Harness(now, fresh);

        var count = await harness.Service.ReassignStaleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, count);
        Assert.Equal(SmsConversationAssignmentStatus.Assigned, fresh.AssignmentStatus);
        Assert.Equal("a1", fresh.AssignedAgentId);
        harness.Store.Verify(s => s.UpdateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Harness
    {
        public Mock<ISmsConversationStore> Store { get; } = new();

        public Mock<ISmsRoutingStrategy> Strategy { get; } = new();

        public Mock<ISmsRealTimeNotifier> Notifier { get; } = new();

        public string NextAgentId { get; init; }

        public SmsRoutedReassignmentService Service { get; }

        public Harness(DateTime now, params SmsConversation[] awaitingPickup)
        {
            Store.Setup(s => s.GetRoutedAwaitingPickupAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(awaitingPickup);
            Store.Setup(s => s.UpdateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            Strategy.Setup(s => s.SelectAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => NextAgentId);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(now);

            Service = new SmsRoutedReassignmentService(
                Store.Object,
                Strategy.Object,
                Notifier.Object,
                clock.Object,
                Microsoft.Extensions.Options.Options.Create(new SmsRoutedDistributionOptions()),
                NullLogger<SmsRoutedReassignmentService>.Instance);
        }
    }
}
