using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Every minute the routing rules try again the conversations whose contact is still waiting for a person, so one that
/// found no agent available is assigned once somebody is.
/// </summary>
public sealed class MessagingRoutingRulesSweepTests
{
    private static readonly DateTime _now = new(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task WithoutAWaitingRule_NoConversationIsRead()
    {
        var harness = new Harness();
        harness.Rules.Add(Rule(MessagingRoutingRuleTriggers.ContactReplied));

        var assigned = await harness.Sweep.RouteWaitingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, assigned);
        harness.Store.Verify(store => store.GetUnownedAwaitingReplyAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ADisabledWaitingRule_IsNotSwept()
    {
        var harness = new Harness();
        var rule = Rule(MessagingRoutingRuleTriggers.Waiting);
        rule.Enabled = false;
        harness.Rules.Add(rule);

        Assert.Equal(0, await harness.Sweep.RouteWaitingAsync(TestContext.Current.CancellationToken));
        harness.Store.Verify(store => store.GetUnownedAwaitingReplyAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheShortestWait_DecidesWhichConversationsAreRead()
    {
        var harness = new Harness();
        harness.Rules.Add(Rule(MessagingRoutingRuleTriggers.Waiting, waitingMinutes: 15));
        harness.Rules.Add(Rule(MessagingRoutingRuleTriggers.Waiting, waitingMinutes: 5));

        await harness.Sweep.RouteWaitingAsync(TestContext.Current.CancellationToken);

        harness.Store.Verify(store => store.GetUnownedAwaitingReplyAsync(_now.AddMinutes(-5), MessagingRoutingRulesSweep.BatchSize, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AWaitingConversation_IsAssigned_Saved_AndTheAgentIsTold()
    {
        var harness = new Harness();
        var rule = Rule(MessagingRoutingRuleTriggers.Waiting);
        harness.Rules.Add(rule);
        var conversation = harness.Waiting(Conversation("c1", queueId: "sales"));
        harness.Assigns(rule, "agent-1");

        var assigned = await harness.Sweep.RouteWaitingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, assigned);
        Assert.Equal(MessagingRoutingRuleTriggers.Waiting, harness.LastRequest.Trigger);
        Assert.Equal("sales", harness.LastRequest.QueueId);
        harness.Store.Verify(store => store.UpdateAsync(conversation, It.IsAny<CancellationToken>()), Times.Once);
        harness.Notifier.Verify(notifier => notifier.NewInboundMessageAsync(
            It.Is<MessagingInboundNotification>(notification => notification.ConversationId == "c1" && notification.AssignedAgentId == "agent-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AConversationAnAgentClaimedMeanwhile_IsLeftAlone()
    {
        var harness = new Harness();
        harness.Rules.Add(Rule(MessagingRoutingRuleTriggers.Waiting));
        var listed = Conversation("c1");
        harness.Waiting(listed);

        // Read again under the lock, it now belongs to an agent.
        var claimed = Conversation("c1");
        claimed.AssignedAgentId = "agent-9";
        claimed.AssignmentStatus = ConversationAssignmentStatus.Assigned;
        harness.Store.Setup(store => store.FindByIdAsync("c1", It.IsAny<CancellationToken>())).ReturnsAsync(claimed);

        Assert.Equal(0, await harness.Sweep.RouteWaitingAsync(TestContext.Current.CancellationToken));
        harness.Evaluator.Verify(evaluator => evaluator.AssignAsync(It.IsAny<MessagingRoutingRuleRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AConversationAnAgentHasRead_IsNotWaiting()
    {
        var harness = new Harness();
        harness.Rules.Add(Rule(MessagingRoutingRuleTriggers.Waiting));
        var conversation = Conversation("c1");
        conversation.IsRead = true;
        harness.Waiting(conversation);

        Assert.Equal(0, await harness.Sweep.RouteWaitingAsync(TestContext.Current.CancellationToken));
        harness.Evaluator.Verify(evaluator => evaluator.AssignAsync(It.IsAny<MessagingRoutingRuleRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AConversationWhoseLockIsBusy_IsTriedOnTheNextPass()
    {
        var harness = new Harness(lockAcquired: false);
        harness.Rules.Add(Rule(MessagingRoutingRuleTriggers.Waiting));
        harness.Waiting(Conversation("c1"));

        Assert.Equal(0, await harness.Sweep.RouteWaitingAsync(TestContext.Current.CancellationToken));
        harness.Store.Verify(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoRuleFindingAnAgent_LeavesTheConversationUnsaved()
    {
        var harness = new Harness();
        harness.Rules.Add(Rule(MessagingRoutingRuleTriggers.Waiting));
        harness.Waiting(Conversation("c1"));

        Assert.Equal(0, await harness.Sweep.RouteWaitingAsync(TestContext.Current.CancellationToken));
        harness.Store.Verify(store => store.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static MessagingRoutingRule Rule(MessagingRoutingRuleTriggers triggers, int waitingMinutes = 5)
        => new()
        {
            ItemId = $"rule-{waitingMinutes}-{triggers}",
            DisplayText = "Rule",
            Enabled = true,
            Triggers = triggers,
            WaitingMinutes = waitingMinutes,
            CandidateSource = MessagingRoutingCandidateSources.Users,
            UserIds = ["user-1"],
        };

    private static MessagingConversation Conversation(string id, string queueId = null)
        => new()
        {
            ItemId = id,
            Channel = "SMS",
            ServiceAddress = "+17025550100",
            ContactAddress = "+17025550142",
            Status = ConversationStatus.Open,
            AssignmentStatus = queueId is null ? ConversationAssignmentStatus.Unassigned : ConversationAssignmentStatus.Pooled,
            OwnerType = queueId is null ? ConversationOwnerType.Personal : ConversationOwnerType.Queue,
            OwnerId = queueId,
            LastMessageUtc = _now.AddMinutes(-30),
        };

    private sealed class Harness
    {
        public Harness(bool lockAcquired = true)
        {
            var rules = new Mock<ICatalog<MessagingRoutingRule>>();
            rules.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Rules.ToArray());

            Store.Setup(store => store.GetUnownedAwaitingReplyAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => WaitingConversations.ToArray());

            Evaluator.Setup(evaluator => evaluator.AssignAsync(It.IsAny<MessagingRoutingRuleRequest>(), It.IsAny<CancellationToken>()))
                .Callback<MessagingRoutingRuleRequest, CancellationToken>((request, _) => LastRequest = request)
                .ReturnsAsync((MessagingRoutingRuleMatch)null);

            var locker = new Mock<ILocker>();
            locker.Setup(value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);

            var distributedLock = new Mock<IDistributedLock>();
            distributedLock
                .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((locker.Object, lockAcquired));

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            Sweep = new MessagingRoutingRulesSweep(
                rules.Object,
                Store.Object,
                Evaluator.Object,
                Notifier.Object,
                distributedLock.Object,
                Options.Create(new MessagingWorkspaceOptions()),
                clock.Object,
                NullLogger<MessagingRoutingRulesSweep>.Instance);
        }

        public List<MessagingRoutingRule> Rules { get; } = [];

        public List<MessagingConversation> WaitingConversations { get; } = [];

        public Mock<IMessagingConversationStore> Store { get; } = new();

        public Mock<IMessagingRoutingRuleEvaluator> Evaluator { get; } = new();

        public Mock<IMessagingRealTimeNotifier> Notifier { get; } = new();

        public MessagingRoutingRuleRequest LastRequest { get; private set; }

        public MessagingRoutingRulesSweep Sweep { get; }

        public MessagingConversation Waiting(MessagingConversation conversation)
        {
            WaitingConversations.Add(conversation);
            Store.Setup(store => store.FindByIdAsync(conversation.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

            return conversation;
        }

        public void Assigns(MessagingRoutingRule rule, string agentId)
            => Evaluator.Setup(evaluator => evaluator.AssignAsync(It.IsAny<MessagingRoutingRuleRequest>(), It.IsAny<CancellationToken>()))
                .Callback<MessagingRoutingRuleRequest, CancellationToken>((request, _) =>
                {
                    LastRequest = request;
                    request.Conversation.AssignedAgentId = agentId;
                    request.Conversation.AssignmentStatus = ConversationAssignmentStatus.Assigned;
                })
                .ReturnsAsync(new MessagingRoutingRuleMatch(rule, agentId, "user-1"));
    }
}
