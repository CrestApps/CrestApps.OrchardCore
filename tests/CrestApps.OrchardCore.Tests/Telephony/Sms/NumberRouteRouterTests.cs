using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services.Routers;
using CrestApps.OrchardCore.Telephony.Sms.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class NumberRouteRouterTests
{
    [Fact]
    public async Task AgentTarget_AssignsConversationToTheAgentPersonally()
    {
        var route = new SmsNumberRoute
        {
            DialedNumber = "+15553334444",
            TargetType = SmsNumberRouteTargetType.Agent,
            TargetId = "agent-1",
            Enabled = true,
        };

        var context = CreateContext(isNew: true);
        var handled = await CreateRouter(route).TryRouteAsync(context);

        Assert.True(handled);
        Assert.Equal(SmsConversationOwnerType.Personal, context.Conversation.OwnerType);
        Assert.Equal("agent-1", context.Conversation.OwnerId);
        Assert.Equal("agent-1", context.Conversation.AssignedAgentId);
        Assert.Equal(SmsConversationAssignmentStatus.Assigned, context.Conversation.AssignmentStatus);
    }

    [Fact]
    public async Task QueueTarget_PlacesConversationInTheQueueSharedPool()
    {
        var route = new SmsNumberRoute
        {
            DialedNumber = "+15553334444",
            TargetType = SmsNumberRouteTargetType.Queue,
            TargetId = "queue-1",
            DistributionMode = SmsNumberRouteDistributionMode.SharedPool,
            Enabled = true,
        };

        var context = CreateContext(isNew: true);
        var handled = await CreateRouter(route).TryRouteAsync(context);

        Assert.True(handled);
        Assert.Equal(SmsConversationOwnerType.Queue, context.Conversation.OwnerType);
        Assert.Equal("queue-1", context.Conversation.OwnerId);
        Assert.Null(context.Conversation.AssignedAgentId);
        Assert.Equal(SmsConversationAssignmentStatus.Pooled, context.Conversation.AssignmentStatus);
    }

    [Fact]
    public async Task NoRoute_ContinuesTheChain()
    {
        var context = CreateContext(isNew: true);
        var handled = await CreateRouter(null).TryRouteAsync(context);

        Assert.False(handled);
    }

    [Fact]
    public async Task ExistingConversation_IsIgnored()
    {
        var route = new SmsNumberRoute { DialedNumber = "+15553334444", TargetType = SmsNumberRouteTargetType.Agent, TargetId = "agent-1" };
        var context = CreateContext(isNew: false);

        var handled = await CreateRouter(route).TryRouteAsync(context);

        Assert.False(handled);
    }

    private static NumberRouteRouter CreateRouter(SmsNumberRoute route)
    {
        var manager = new Mock<ISmsNumberRouteManager>();
        manager.Setup(m => m.FindByDialedNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);

        return new NumberRouteRouter(manager.Object);
    }

    private static SmsInboundRoutingContext CreateContext(bool isNew)
        => new()
        {
            Message = new OmnichannelMessage { ServiceAddress = "+15553334444", CustomerAddress = "+15551112222" },
            Endpoint = new OmnichannelChannelEndpoint { Value = "+15553334444" },
            Conversation = new SmsConversation { ServiceAddress = "+15553334444", CustomerAddress = "+15551112222" },
            IsNewConversation = isNew,
        };
}
