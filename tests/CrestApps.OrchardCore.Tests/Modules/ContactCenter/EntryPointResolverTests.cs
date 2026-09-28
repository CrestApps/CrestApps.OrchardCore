using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class EntryPointResolverTests
{
    [Fact]
    public void CreatePlan_WhenOpen_QueuesToTarget()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint { ItemId = "e1", TargetQueueId = "q1", Priority = InteractionPriority.High };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert
        Assert.True(plan.ShouldQueue);
        Assert.Equal("q1", plan.TargetQueueId);
        Assert.Equal(InteractionPriority.High, plan.Priority);
    }

    [Fact]
    public void CreatePlan_WhenClosedWithOverflow_QueuesToOverflow()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetQueueId = "q1",
            OverflowQueueId = "q2",
            ClosedAction = EntryPointClosedAction.Overflow,
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: false);

        // Assert
        Assert.True(plan.ShouldQueue);
        Assert.Equal("q2", plan.TargetQueueId);
    }

    [Theory]
    [InlineData(EntryPointClosedAction.Voicemail)]
    [InlineData(EntryPointClosedAction.Reject)]
    public void CreatePlan_WhenClosedWithVoicemailOrReject_DoesNotQueue(EntryPointClosedAction action)
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint { ItemId = "e1", TargetQueueId = "q1", ClosedAction = action };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: false);

        // Assert
        Assert.False(plan.ShouldQueue);
        Assert.Null(plan.TargetQueueId);
    }

    [Fact]
    public void CreatePlan_WhenOpenWithAgentTarget_RoutesDirectlyToAgentWithNoQueueFallback()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent1",
            TargetQueueId = "q1",
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert
        Assert.True(plan.ShouldQueue);
        Assert.True(plan.RouteToAgent);
        Assert.Equal("agent1", plan.TargetAgentId);

        // The call is carried under the synthetic direct-routing queue, never the (now unused) TargetQueueId.
        Assert.Equal(ContactCenterConstants.DirectRouting.QueueId, plan.TargetQueueId);
    }

    [Theory]
    [InlineData(45, 45)]   // configured window is honored
    [InlineData(0, 30)]    // non-positive window falls back to the default
    [InlineData(1000, 300)] // above the maximum is clamped
    [InlineData(1, 5)]     // below the minimum is clamped up
    public void CreatePlan_WhenAgentTargetWithVoicemail_UsesConfiguredRingWindow(int configured, int expected)
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent1",
            VoicemailEnabled = true,
            RingTimeoutSeconds = configured,
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert
        Assert.True(plan.RouteToAgent);
        Assert.Equal(expected, plan.RingTimeoutSeconds);
    }

    [Fact]
    public void CreatePlan_WhenAgentTargetWithVoicemailDisabled_UsesZeroRingWindow()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent1",
            VoicemailEnabled = false,
            RingTimeoutSeconds = 45,
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert: 0 signals "no voicemail" downstream — the caller keeps ringing and is held for the agent.
        Assert.True(plan.RouteToAgent);
        Assert.Equal(0, plan.RingTimeoutSeconds);
    }

    [Fact]
    public void CreatePlan_WhenAgentTargetHasNoAgent_FallsBackToQueueRouting()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = null,
            TargetQueueId = "q1",
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert
        Assert.True(plan.ShouldQueue);
        Assert.False(plan.RouteToAgent);
        Assert.Equal("q1", plan.TargetQueueId);
    }

    [Theory]
    [InlineData(EntryPointClosedAction.HoldInQueue)]
    [InlineData(EntryPointClosedAction.Overflow)]
    [InlineData(EntryPointClosedAction.Voicemail)]
    public void CreatePlan_WhenClosedWithAgentTarget_SendsToVoicemail(EntryPointClosedAction action)
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent1",
            ClosedAction = action,
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: false);

        // Assert
        Assert.False(plan.ShouldQueue);
        Assert.Null(plan.TargetQueueId);
        Assert.Equal(EntryPointClosedAction.Voicemail, plan.ClosedAction);
    }

    [Fact]
    public void CreatePlan_WhenClosedWithAgentTargetAndReject_Rejects()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetType = EntryPointTargetType.Agent,
            TargetAgentId = "agent1",
            ClosedAction = EntryPointClosedAction.Reject,
        };

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: false);

        // Assert
        Assert.False(plan.ShouldQueue);
        Assert.Null(plan.TargetQueueId);
        Assert.Equal(EntryPointClosedAction.Reject, plan.ClosedAction);
    }

    [Fact]
    public async Task ResolveAsync_MatchesDialedNumberAndEvaluatesBusinessHours()
    {
        // Arrange
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "e1",
            TargetQueueId = "q1",
            BusinessHoursCalendarId = "cal1",
            DialedNumbers = ["+15551234567"],
        };

        var manager = new Mock<IContactCenterEntryPointManager>();
        manager.Setup(m => m.GetEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync([entryPoint]);

        var businessHours = new Mock<IBusinessHoursService>();
        businessHours.Setup(b => b.IsOpenAsync("cal1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var resolver = new EntryPointResolver(manager.Object, businessHours.Object);

        // Act
        var plan = await resolver.ResolveAsync("+15551234567", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.IsOpen);
        Assert.Equal("q1", plan.TargetQueueId);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoEntryPointMatches_ReturnsNull()
    {
        // Arrange
        var manager = new Mock<IContactCenterEntryPointManager>();
        manager.Setup(m => m.GetEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var resolver = new EntryPointResolver(manager.Object, new Mock<IBusinessHoursService>().Object);

        // Act
        var plan = await resolver.ResolveAsync("+15550000000", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(plan);
    }
}
