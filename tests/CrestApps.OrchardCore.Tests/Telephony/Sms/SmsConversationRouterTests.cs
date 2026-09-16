using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

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
        var context = await router.RouteAsync(CreateContext(SmsRoutingTrigger.Inbound), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.WasCalled);
        Assert.True(second.WasCalled);
        Assert.False(third.WasCalled);
        Assert.Same(second, context.ClaimedBy);
    }

    [Theory]
    [InlineData(SmsRoutingTrigger.Inbound)]
    [InlineData(SmsRoutingTrigger.Handoff)]
    [InlineData(SmsRoutingTrigger.Reassignment)]
    [InlineData(SmsRoutingTrigger.ManualTransfer)]
    public async Task Route_TellsEveryRouterWhichTriggerItIsServing(SmsRoutingTrigger trigger)
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
        var context = await router.RouteAsync(CreateContext(SmsRoutingTrigger.Inbound), TestContext.Current.CancellationToken);

        // Assert
        // An unclaimed thread is visible and claimable rather than silently attached to whoever ran last.
        Assert.Null(context.ClaimedBy);
        Assert.Equal(SmsConversationAssignmentStatus.Unassigned, context.Conversation.AssignmentStatus);
    }

    private static SmsConversationRouter CreateRouter(params ISmsInboundRouter[] routers)
        => new(routers, NullLogger<SmsConversationRouter>.Instance);

    private static SmsRoutingContext CreateContext(SmsRoutingTrigger trigger)
    {
        return new SmsRoutingContext
        {
            Trigger = trigger,
            Message = new OmnichannelMessage { Id = "m1", Content = "hello" },
            Endpoint = new OmnichannelChannelEndpoint { ItemId = "e1", Value = "+16502530000" },
            Conversation = new SmsConversation
            {
                ItemId = "c1",
                ServiceAddress = "+16502530000",
                ContactAddress = "+16502530001",
                AssignmentStatus = SmsConversationAssignmentStatus.Unassigned,
                CreatedUtc = _now,
            },
            IsNewConversation = true,
        };
    }

    private sealed class RecordingRouter : ISmsInboundRouter
    {
        private readonly bool _claims;

        public RecordingRouter(int order, bool claims)
        {
            Order = order;
            _claims = claims;
        }

        public int Order { get; }

        public bool WasCalled { get; private set; }

        public SmsRoutingTrigger? ObservedTrigger { get; private set; }

        public Task<bool> TryRouteAsync(SmsRoutingContext context, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            ObservedTrigger = context.Trigger;

            return Task.FromResult(_claims);
        }
    }
}
