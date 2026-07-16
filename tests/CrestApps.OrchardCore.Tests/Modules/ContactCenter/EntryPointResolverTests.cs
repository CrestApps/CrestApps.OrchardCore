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
        manager.Setup(m => m.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync([entryPoint]);

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
        manager.Setup(m => m.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var resolver = new EntryPointResolver(manager.Object, new Mock<IBusinessHoursService>().Object);

        // Act
        var plan = await resolver.ResolveAsync("+15550000000", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(plan);
    }
}
