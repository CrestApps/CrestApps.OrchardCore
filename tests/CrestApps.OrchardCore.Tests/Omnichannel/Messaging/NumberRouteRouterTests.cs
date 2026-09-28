using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public class NumberRouteRouterTests
{
    [Fact]
    public async Task AgentTarget_AssignsConversationToTheAgentPersonally()
    {
        var routing = new MessagingEndpointRoutingSettings { TargetType = ConversationRouteTargetType.Agent, TargetId = "agent-1" };
        var context = CreateContext(routing, isNew: true);

        var handled = await new EndpointRouteRouter().TryRouteAsync(context, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(ConversationOwnerType.Personal, context.Conversation.OwnerType);
        Assert.Equal("agent-1", context.Conversation.OwnerId);
        Assert.Equal("agent-1", context.Conversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Assigned, context.Conversation.AssignmentStatus);
    }

    [Fact]
    public async Task QueueTarget_PlacesConversationInTheQueueSharedPool()
    {
        var routing = new MessagingEndpointRoutingSettings
        {
            TargetType = ConversationRouteTargetType.Queue,
            TargetId = "queue-1",
            DistributionMode = ConversationDistributionMode.SharedPool,
        };
        var context = CreateContext(routing, isNew: true);

        var handled = await new EndpointRouteRouter().TryRouteAsync(context, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(ConversationOwnerType.Queue, context.Conversation.OwnerType);
        Assert.Equal("queue-1", context.Conversation.OwnerId);
        Assert.Null(context.Conversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Pooled, context.Conversation.AssignmentStatus);
    }

    [Fact]
    public async Task NoRoutingTarget_ContinuesTheChain()
    {
        var context = CreateContext(routing: null, isNew: true);

        var handled = await new EndpointRouteRouter().TryRouteAsync(context, TestContext.Current.CancellationToken);

        Assert.False(handled);
    }

    [Fact]
    public async Task ExistingConversation_IsIgnored()
    {
        var routing = new MessagingEndpointRoutingSettings { TargetType = ConversationRouteTargetType.Agent, TargetId = "agent-1" };
        var context = CreateContext(routing, isNew: false);

        var handled = await new EndpointRouteRouter().TryRouteAsync(context, TestContext.Current.CancellationToken);

        Assert.False(handled);
    }

    private static MessagingRoutingContext CreateContext(MessagingEndpointRoutingSettings routing, bool isNew)
    {
        var endpoint = new OmnichannelChannelEndpoint { Channel = "SMS", Value = "+15553334444" };

        if (routing is not null)
        {
            endpoint.Put(routing);
        }

        return new MessagingRoutingContext
        {
            Message = new OmnichannelMessage { ServiceAddress = "+15553334444", CustomerAddress = "+15551112222" },
            Endpoint = endpoint,
            Conversation = new MessagingConversation { Channel = "SMS", ServiceAddress = "+15553334444", ContactAddress = "+15551112222" },
            IsNewConversation = isNew,
        };
    }
}
