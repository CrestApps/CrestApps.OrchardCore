using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public class RoutedQueueRouterTests
{
    [Fact]
    public async Task PushAssigns_ToTheSelectedAgent()
    {
        var harness = new Harness(selectedAgentId: "a2");
        var context = Harness.Context(ConversationRouteTargetType.Queue, ConversationDistributionMode.Routed, "q1");

        var handled = await harness.Router.TryRouteAsync(context, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(ConversationOwnerType.Queue, context.Conversation.OwnerType);
        Assert.Equal("q1", context.Conversation.OwnerId);
        Assert.Equal("a2", context.Conversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Assigned, context.Conversation.AssignmentStatus);
        Assert.NotNull(context.Conversation.AssignedUtc);
    }

    [Fact]
    public async Task FallsThrough_WhenNoAgentAvailable()
    {
        var harness = new Harness(selectedAgentId: null);
        var context = Harness.Context(ConversationRouteTargetType.Queue, ConversationDistributionMode.Routed, "q1");

        var handled = await harness.Router.TryRouteAsync(context, TestContext.Current.CancellationToken);

        // Returning false lets EndpointRouteRouter place the message into the shared pool as a fallback.
        Assert.False(handled);
        Assert.Null(context.Conversation.AssignedAgentId);
    }

    [Fact]
    public async Task Ignores_SharedPoolMode()
    {
        var harness = new Harness(selectedAgentId: "a1");
        var context = Harness.Context(ConversationRouteTargetType.Queue, ConversationDistributionMode.SharedPool, "q1");

        var handled = await harness.Router.TryRouteAsync(context, TestContext.Current.CancellationToken);

        Assert.False(handled);
        harness.Strategy.Verify(s => s.SelectAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ignores_AgentTarget()
    {
        var harness = new Harness(selectedAgentId: "a1");
        var context = Harness.Context(ConversationRouteTargetType.Agent, ConversationDistributionMode.Routed, "agent-7");

        var handled = await harness.Router.TryRouteAsync(context, TestContext.Current.CancellationToken);

        Assert.False(handled);
    }

    [Fact]
    public async Task Ignores_ExistingConversation()
    {
        var harness = new Harness(selectedAgentId: "a1");
        var context = Harness.Context(ConversationRouteTargetType.Queue, ConversationDistributionMode.Routed, "q1", isNew: false);

        var handled = await harness.Router.TryRouteAsync(context, TestContext.Current.CancellationToken);

        Assert.False(handled);
    }

    private sealed class Harness
    {
        public Mock<IMessagingRoutingStrategy> Strategy { get; } = new();

        public RoutedQueueRouter Router { get; }

        public Harness(string selectedAgentId)
        {
            Strategy.Setup(s => s.SelectAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(selectedAgentId);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

            Router = new RoutedQueueRouter(Strategy.Object, clock.Object);
        }

        public static MessagingRoutingContext Context(
            ConversationRouteTargetType targetType,
            ConversationDistributionMode mode,
            string targetId,
            bool isNew = true)
        {
            var endpoint = new OmnichannelChannelEndpoint { ItemId = "ep1", Channel = "SMS", Value = "+15553334444" };
            endpoint.Put(new MessagingEndpointRoutingSettings
            {
                TargetType = targetType,
                DistributionMode = mode,
                TargetId = targetId,
            });

            return new MessagingRoutingContext
            {
                Message = new OmnichannelMessage { Channel = "SMS", CustomerAddress = "+15551112222", ServiceAddress = "+15553334444" },
                Endpoint = endpoint,
                Conversation = new MessagingConversation { Channel = "SMS", ItemId = "conv1", ServiceAddress = "+15553334444", ContactAddress = "+15551112222" },
                IsNewConversation = isNew,
            };
        }
    }
}
