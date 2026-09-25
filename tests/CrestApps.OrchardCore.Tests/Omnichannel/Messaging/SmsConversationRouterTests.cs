using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Ownership of an SMS thread used to be decided in three places that had drifted apart: the inbound chain, the
/// escalation path (which always pooled), and the unpicked-thread sweep (which called the strategy itself). The
/// router is the single entry point, and the trigger says which of those a call is, so the same rules apply
/// whichever door the thread comes through.
/// </summary>
public sealed class SmsConversationRouterTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Route_RunsTheChainInOrder_AndStopsAtTheFirstRouterThatClaims()
    {
        // Arrange
        var first = new RecordingRouter(order: 100, claims: false);
        var second = new RecordingRouter(order: 200, claims: true);
        var third = new RecordingRouter(order: 300, claims: true);

        var router = CreateRouter(third, first, second);

        // Act
        var context = await router.RouteAsync(CreateContext(MessagingRoutingTrigger.Inbound), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.WasCalled);
        Assert.True(second.WasCalled);
        Assert.False(third.WasCalled);
        Assert.Same(second, context.ClaimedBy);
    }

    [Theory]
    [InlineData(MessagingRoutingTrigger.Inbound)]
    [InlineData(MessagingRoutingTrigger.Handoff)]
    [InlineData(MessagingRoutingTrigger.Reassignment)]
    [InlineData(MessagingRoutingTrigger.ManualTransfer)]
    public async Task Route_TellsEveryRouterWhichTriggerItIsServing(MessagingRoutingTrigger trigger)
    {
        // Arrange
        // A router that cannot see the trigger has to infer it, and the three paths inferred it differently.
        var observer = new RecordingRouter(order: 100, claims: true);
        var router = CreateRouter(observer);

        // Act
        await router.RouteAsync(CreateContext(trigger), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(trigger, observer.ObservedTrigger);
    }

    [Fact]
    public async Task Route_WhenNoRouterClaims_LeavesTheConversationUnassigned()
    {
        // Arrange
        var router = CreateRouter(new RecordingRouter(order: 100, claims: false));

        // Act
        var context = await router.RouteAsync(CreateContext(MessagingRoutingTrigger.Inbound), TestContext.Current.CancellationToken);

        // Assert
        // An unclaimed thread is visible and claimable rather than silently attached to whoever ran last.
        Assert.Null(context.ClaimedBy);
        Assert.Equal(ConversationAssignmentStatus.Unassigned, context.Conversation.AssignmentStatus);
    }

    private static MessagingConversationRouter CreateRouter(params IMessagingInboundRouter[] routers)
        => new(routers, MessagingTestChannels.Resolver(MessagingTestChannels.AcceptingDispatcher().Object), NullLogger<MessagingConversationRouter>.Instance);

    private static MessagingRoutingContext CreateContext(MessagingRoutingTrigger trigger)
    {
        return new MessagingRoutingContext
        {
            Trigger = trigger,
            Message = new OmnichannelMessage { Id = "m1", Content = "hello" },
            Endpoint = new OmnichannelChannelEndpoint { ItemId = "e1", Value = "+16502530000" },
            Conversation = new MessagingConversation
            {
                Channel = "SMS",
                ItemId = "c1",
                ServiceAddress = "+16502530000",
                ContactAddress = "+16502530001",
                AssignmentStatus = ConversationAssignmentStatus.Unassigned,
                CreatedUtc = _now,
            },
            IsNewConversation = true,
        };
    }

    private sealed class RecordingRouter : IMessagingInboundRouter
    {
        private readonly bool _claims;

        public RecordingRouter(int order, bool claims)
        {
            Order = order;
            _claims = claims;
        }

        public int Order { get; }

        public bool WasCalled { get; private set; }

        public MessagingRoutingTrigger? ObservedTrigger { get; private set; }

        public Task<bool> TryRouteAsync(MessagingRoutingContext context, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            ObservedTrigger = context.Trigger;

            return Task.FromResult(_claims);
        }
    }
}
