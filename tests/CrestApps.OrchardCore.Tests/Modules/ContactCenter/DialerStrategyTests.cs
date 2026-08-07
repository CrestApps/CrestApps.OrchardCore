using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerStrategyTests
{
    [Fact]
    public void PowerDialerStrategy_Mode_IsPower()
    {
        var strategy = new PowerDialerStrategy(Mock.Of<IActivityAssignmentService>(), Mock.Of<IDialerAttemptService>());

        Assert.Equal(DialerMode.Power, strategy.Mode);
    }

    [Fact]
    public void ProgressiveDialerStrategy_Mode_IsProgressive()
    {
        var strategy = new ProgressiveDialerStrategy(Mock.Of<IActivityAssignmentService>(), Mock.Of<IDialerAttemptService>());

        Assert.Equal(DialerMode.Progressive, strategy.Mode);
    }

    [Fact]
    public async Task PowerDialerStrategy_RunCycle_StopsAtCappedCallsPerAgent()
    {
        // Arrange
        var assignmentService = new Mock<IActivityAssignmentService>();
        assignmentService
            .Setup(s => s.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ActivityReservation { ItemId = "r", ActivityItemId = "a", AgentId = "agent" });

        var attemptService = new Mock<IDialerAttemptService>();
        attemptService
            .Setup(s => s.TryDialAsync(It.IsAny<DialerProfile>(), It.IsAny<ActivityReservation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var strategy = new PowerDialerStrategy(assignmentService.Object, attemptService.Object);
        var profile = new DialerProfile { QueueId = "q1", Mode = DialerMode.Power, CallsPerAgent = 10 };

        // Act
        var started = await strategy.RunCycleAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PowerDialerStrategy.MaxCallsPerAgent, started);
        assignmentService.Verify(s => s.AssignNextAsync("q1", It.IsAny<CancellationToken>()), Times.Exactly(PowerDialerStrategy.MaxCallsPerAgent));
    }

    [Fact]
    public async Task ProgressiveDialerStrategy_RunCycle_DialsOncePerAvailableAgentUntilNoReservation()
    {
        // Arrange
        var assignmentService = new Mock<IActivityAssignmentService>();
        assignmentService
            .SetupSequence(s => s.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1", ActivityItemId = "a1", AgentId = "agent1" })
            .ReturnsAsync(new ActivityReservation { ItemId = "r2", ActivityItemId = "a2", AgentId = "agent2" })
            .ReturnsAsync((ActivityReservation)null);

        var attemptService = new Mock<IDialerAttemptService>();
        attemptService
            .Setup(s => s.TryDialAsync(It.IsAny<DialerProfile>(), It.IsAny<ActivityReservation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var strategy = new ProgressiveDialerStrategy(assignmentService.Object, attemptService.Object);
        var profile = new DialerProfile { QueueId = "q1", Mode = DialerMode.Progressive };

        // Act
        var started = await strategy.RunCycleAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, started);
        assignmentService.Verify(s => s.AssignNextAsync("q1", It.IsAny<CancellationToken>()), Times.Exactly(3));
        attemptService.Verify(s => s.TryDialAsync(It.IsAny<DialerProfile>(), It.IsAny<ActivityReservation>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DialerStrategy_WhenNoAgentReserved_ReturnsZero()
    {
        // Arrange
        var assignmentService = new Mock<IActivityAssignmentService>();
        assignmentService
            .Setup(s => s.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityReservation)null);

        var attemptService = new Mock<IDialerAttemptService>();
        var strategy = new ProgressiveDialerStrategy(assignmentService.Object, attemptService.Object);
        var profile = new DialerProfile { QueueId = "q1", Mode = DialerMode.Progressive };

        // Act
        var started = await strategy.RunCycleAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, started);
        attemptService.Verify(s => s.TryDialAsync(It.IsAny<DialerProfile>(), It.IsAny<ActivityReservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
