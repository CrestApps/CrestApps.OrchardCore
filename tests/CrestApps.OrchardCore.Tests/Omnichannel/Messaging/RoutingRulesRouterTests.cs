using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// The routing rules router offers a contact's message on a conversation nobody owns to the rules: a reply on a thread a
/// workflow started, which used to wait for someone to claim it, and a new conversation whose entry point names no agent.
/// </summary>
public sealed class RoutingRulesRouterTests
{
    [Fact]
    public async Task AReplyOnAnUnownedConversation_IsOfferedToTheRules_AndReopensIt()
    {
        // Arrange
        var harness = new Harness(assigns: true);
        var conversation = new MessagingConversation
        {
            ItemId = "c1",
            OwnerType = ConversationOwnerType.Personal,
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
            Status = ConversationStatus.Closed,
        };

        // Act
        var handled = await harness.Router.TryRouteAsync(Harness.Context(conversation, isNew: false), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(ConversationStatus.Open, conversation.Status);
        Assert.Equal(MessagingRoutingRuleTriggers.ContactReplied, harness.LastRequest.Trigger);
        Assert.Null(harness.LastRequest.QueueId);
    }

    [Fact]
    public async Task AReplyOnAPooledQueueConversation_CarriesTheQueue()
    {
        // Arrange
        var harness = new Harness(assigns: true);
        var conversation = new MessagingConversation
        {
            ItemId = "c1",
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = "sales",
            AssignmentStatus = ConversationAssignmentStatus.Pooled,
        };

        // Act
        await harness.Router.TryRouteAsync(Harness.Context(conversation, isNew: false), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("sales", harness.LastRequest.QueueId);
    }

    [Fact]
    public async Task AConversationSomebodyOwns_IsLeftAlone()
    {
        // Arrange
        var harness = new Harness(assigns: true);
        var assigned = new MessagingConversation
        {
            ItemId = "c1",
            AssignedAgentId = "agent-1",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
        };
        var personal = new MessagingConversation
        {
            ItemId = "c2",
            OwnerType = ConversationOwnerType.Personal,
            OwnerId = "agent-2",
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
        };

        // Act
        var assignedHandled = await harness.Router.TryRouteAsync(Harness.Context(assigned, isNew: false), TestContext.Current.CancellationToken);
        var personalHandled = await harness.Router.TryRouteAsync(Harness.Context(personal, isNew: false), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(assignedHandled);
        Assert.False(personalHandled);
        harness.Evaluator.Verify(e => e.AssignAsync(It.IsAny<MessagingRoutingRuleRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ANewConversation_ToAQueue_IsOfferedWithTheQueue_ButOneToAnAgentIsNot()
    {
        // Arrange
        var harness = new Harness(assigns: true);
        var toQueue = Harness.Context(new MessagingConversation { ItemId = "c1" }, isNew: true, new MessagingInboundRouting
        {
            TargetType = ConversationRouteTargetType.Queue,
            TargetId = "support",
        });

        var toAgent = Harness.Context(new MessagingConversation { ItemId = "c2" }, isNew: true, new MessagingInboundRouting
        {
            TargetType = ConversationRouteTargetType.Agent,
            TargetId = "agent-7",
        });

        // Act
        var queueHandled = await harness.Router.TryRouteAsync(toQueue, TestContext.Current.CancellationToken);
        var agentHandled = await harness.Router.TryRouteAsync(toAgent, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(queueHandled);
        Assert.Equal(MessagingRoutingRuleTriggers.NewConversation, harness.LastRequest.Trigger);
        Assert.Equal("support", harness.LastRequest.QueueId);
        Assert.False(agentHandled);
        harness.Evaluator.Verify(e => e.AssignAsync(It.IsAny<MessagingRoutingRuleRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoRuleAssigning_DeclinesSoRoutingGoesOnAsBefore()
    {
        // Arrange
        var harness = new Harness(assigns: false);
        var conversation = new MessagingConversation
        {
            ItemId = "c1",
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
            Status = ConversationStatus.Closed,
        };

        // Act
        var handled = await harness.Router.TryRouteAsync(Harness.Context(conversation, isNew: false), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(handled);
        Assert.Equal(ConversationStatus.Closed, conversation.Status);
    }

    [Fact]
    public async Task OnlyAnInboundMessage_IsOffered()
    {
        // Arrange
        var harness = new Harness(assigns: true);
        var context = new MessagingRoutingContext
        {
            Trigger = MessagingRoutingTrigger.Reassignment,
            Conversation = new MessagingConversation { ItemId = "c1" },
        };

        // Act
        var handled = await harness.Router.TryRouteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(handled);
    }

    private sealed class Harness
    {
        public Harness(bool assigns)
        {
            Evaluator
                .Setup(e => e.AssignAsync(It.IsAny<MessagingRoutingRuleRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MessagingRoutingRuleRequest request, CancellationToken _) =>
                {
                    LastRequest = request;

                    return assigns
                        ? new MessagingRoutingRuleMatch(new MessagingRoutingRule { DisplayText = "Rule" }, "agent-1", "user-1")
                        : null;
                });

            Router = new RoutingRulesRouter(Evaluator.Object);
        }

        public Mock<IMessagingRoutingRuleEvaluator> Evaluator { get; } = new();

        public RoutingRulesRouter Router { get; }

        public MessagingRoutingRuleRequest LastRequest { get; private set; }

        public static MessagingRoutingContext Context(MessagingConversation conversation, bool isNew, MessagingInboundRouting routing = null)
            => new()
            {
                Trigger = MessagingRoutingTrigger.Inbound,
                Conversation = conversation,
                IsNewConversation = isNew,
                Routing = routing,
            };
    }
}
