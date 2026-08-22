using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routers;
using CrestApps.OrchardCore.Sms.Workspace.Models;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class NumberRouteRouterTests
{
    [Fact]
    public async Task AgentTarget_AssignsConversationToTheAgentPersonally()
    {
        var routing = new SmsEndpointRoutingSettings { TargetType = SmsNumberRouteTargetType.Agent, TargetId = "agent-1" };
        var context = CreateContext(routing, isNew: true);

        var handled = await new NumberRouteRouter().TryRouteAsync(context);

        Assert.True(handled);
        Assert.Equal(SmsConversationOwnerType.Personal, context.Conversation.OwnerType);
        Assert.Equal("agent-1", context.Conversation.OwnerId);
        Assert.Equal("agent-1", context.Conversation.AssignedAgentId);
        Assert.Equal(SmsConversationAssignmentStatus.Assigned, context.Conversation.AssignmentStatus);
    }

    [Fact]
    public async Task QueueTarget_PlacesConversationInTheQueueSharedPool()
    {
        var routing = new SmsEndpointRoutingSettings
        {
            TargetType = SmsNumberRouteTargetType.Queue,
            TargetId = "queue-1",
            DistributionMode = SmsNumberRouteDistributionMode.SharedPool,
        };
        var context = CreateContext(routing, isNew: true);

        var handled = await new NumberRouteRouter().TryRouteAsync(context);

        Assert.True(handled);
        Assert.Equal(SmsConversationOwnerType.Queue, context.Conversation.OwnerType);
        Assert.Equal("queue-1", context.Conversation.OwnerId);
        Assert.Null(context.Conversation.AssignedAgentId);
        Assert.Equal(SmsConversationAssignmentStatus.Pooled, context.Conversation.AssignmentStatus);
    }

    [Fact]
    public async Task NoRoutingTarget_ContinuesTheChain()
    {
        var context = CreateContext(routing: null, isNew: true);

        var handled = await new NumberRouteRouter().TryRouteAsync(context);

        Assert.False(handled);
    }

    [Fact]
    public async Task ExistingConversation_IsIgnored()
    {
        var routing = new SmsEndpointRoutingSettings { TargetType = SmsNumberRouteTargetType.Agent, TargetId = "agent-1" };
        var context = CreateContext(routing, isNew: false);

        var handled = await new NumberRouteRouter().TryRouteAsync(context);

        Assert.False(handled);
    }

    private static SmsInboundRoutingContext CreateContext(SmsEndpointRoutingSettings routing, bool isNew)
    {
        var endpoint = new OmnichannelChannelEndpoint { Channel = "SMS", Value = "+15553334444" };

        if (routing is not null)
        {
            endpoint.Put(routing);
        }

        return new SmsInboundRoutingContext
        {
            Message = new OmnichannelMessage { ServiceAddress = "+15553334444", CustomerAddress = "+15551112222" },
            Endpoint = endpoint,
            Conversation = new SmsConversation { ServiceAddress = "+15553334444", CustomerAddress = "+15551112222" },
            IsNewConversation = isNew,
        };
    }
}
