using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An activity that leaves the routable set through the CRM — an admin purge, a cancellation, a completion or a
/// delete — has to tell the Contact Center to withdraw its queued work, naming who made the change.
/// </summary>
public sealed class ContactCenterActivityRoutabilityHandlerTests
{
    [Fact]
    public async Task UpdateAsync_WhenASupervisorPurgesTheActivity_WithdrawsItsWorkInTheirName()
    {
        // Arrange
        var context = new HandlerContext(currentUserId: "supervisor-user");
        var activity = new OmnichannelActivity { ItemId = "activity-1", AssignedToId = "agent-user" };
        activity.Status = ActivityStatus.Purged;
        activity.PurgedById = "supervisor-user";

        // Act
        await context.Manager.UpdateAsync(activity, cancellationToken: TestContext.Current.CancellationToken);
        await context.RunScheduledAsync();

        // Assert
        context.Withdrawal.Verify(service => service.WithdrawAsync(
            "activity-1",
            QueuedWorkWithdrawalReasons.For(ActivityStatus.Purged),
            ContactCenterActor.Supervisor("supervisor-user"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenTheAssignedAgentCancelsTheActivity_WithdrawsItsWorkAsTheAgent()
    {
        // Arrange
        var context = new HandlerContext(currentUserId: "agent-user");
        var activity = new OmnichannelActivity { ItemId = "activity-1", AssignedToId = "agent-user", Status = ActivityStatus.Cancelled };

        // Act
        await context.Manager.UpdateAsync(activity, cancellationToken: TestContext.Current.CancellationToken);
        await context.RunScheduledAsync();

        // Assert
        context.Withdrawal.Verify(service => service.WithdrawAsync(
            "activity-1",
            QueuedWorkWithdrawalReasons.For(ActivityStatus.Cancelled),
            ContactCenterActor.Agent("agent-user"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenNoUserMadeTheChange_WithdrawsItsWorkAsThePlatform()
    {
        // Arrange
        var context = new HandlerContext(currentUserId: null);
        var activity = new OmnichannelActivity { ItemId = "activity-1", Status = ActivityStatus.Completed };

        // Act
        await context.Manager.UpdateAsync(activity, cancellationToken: TestContext.Current.CancellationToken);
        await context.RunScheduledAsync();

        // Assert
        context.Withdrawal.Verify(service => service.WithdrawAsync(
            "activity-1",
            QueuedWorkWithdrawalReasons.For(ActivityStatus.Completed),
            ContactCenterActor.System,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ActivityStatus.Pending)]
    [InlineData(ActivityStatus.Scheduled)]
    [InlineData(ActivityStatus.Reserved)]
    [InlineData(ActivityStatus.InProgress)]
    [InlineData(ActivityStatus.AwaitingAgentResponse)]
    public async Task UpdateAsync_WhenTheActivityIsStillRoutable_LeavesItsWorkQueued(ActivityStatus status)
    {
        // Arrange
        var context = new HandlerContext(currentUserId: "supervisor-user");
        var activity = new OmnichannelActivity { ItemId = "activity-1", Status = status };

        // Act
        await context.Manager.UpdateAsync(activity, cancellationToken: TestContext.Current.CancellationToken);
        await context.RunScheduledAsync();

        // Assert
        Assert.Equal(0, context.ScheduledCount);
        context.Withdrawal.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteAsync_WithdrawsTheDeletedActivitysWork()
    {
        // Arrange
        var context = new HandlerContext(currentUserId: "supervisor-user");
        var activity = new OmnichannelActivity { ItemId = "activity-1", Status = ActivityStatus.Pending };

        // Act
        await context.Manager.DeleteAsync(activity, TestContext.Current.CancellationToken);
        await context.RunScheduledAsync();

        // Assert
        context.Withdrawal.Verify(service => service.WithdrawAsync(
            "activity-1",
            QueuedWorkWithdrawalReasons.ActivityDeleted,
            ContactCenterActor.Supervisor("supervisor-user"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenThereIsNoScopeToDeferTo_WithdrawsStraightAway()
    {
        // Arrange
        var context = new HandlerContext(currentUserId: null, canDefer: false);
        var activity = new OmnichannelActivity { ItemId = "activity-1", Status = ActivityStatus.Purged };

        // Act
        await context.Manager.UpdateAsync(activity, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        context.Withdrawal.Verify(service => service.WithdrawAsync(
            "activity-1",
            QueuedWorkWithdrawalReasons.For(ActivityStatus.Purged),
            ContactCenterActor.System,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A real activity manager over a stand-in store with the handler under test attached, and a scope executor
    /// that holds deferred work until the test runs it, as a shell scope does after it commits.
    /// </summary>
    private sealed class HandlerContext
    {
        private readonly List<Func<IQueuedWorkWithdrawalService, Task>> _scheduled = [];

        public HandlerContext(string currentUserId, bool canDefer = true)
        {
            var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
            scopeExecutor
                .Setup(executor => executor.ScheduleAfterCommit(It.IsAny<Func<IQueuedWorkWithdrawalService, Task>>()))
                .Callback<Func<IQueuedWorkWithdrawalService, Task>>(operation => _scheduled.Add(operation))
                .Returns(canDefer);
            scopeExecutor
                .Setup(executor => executor.ExecuteAsync(It.IsAny<Func<IQueuedWorkWithdrawalService, Task>>()))
                .Returns<Func<IQueuedWorkWithdrawalService, Task>>(operation => operation(Withdrawal.Object));

            var httpContext = new DefaultHttpContext();

            if (currentUserId is not null)
            {
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, currentUserId)], "Test"));
            }

            var handler = new ContactCenterActivityRoutabilityHandler(
                scopeExecutor.Object,
                new HttpContextAccessor { HttpContext = httpContext });

            Manager = new OmnichannelActivityManager(
                Mock.Of<IOmnichannelActivityStore>(),
                [handler],
                NullLogger<CatalogManager<OmnichannelActivity>>.Instance);
        }

        public Mock<IQueuedWorkWithdrawalService> Withdrawal { get; } = new();

        public OmnichannelActivityManager Manager { get; }

        public int ScheduledCount => _scheduled.Count;

        public async Task RunScheduledAsync()
        {
            foreach (var operation in _scheduled)
            {
                await operation(Withdrawal.Object);
            }
        }
    }
}
