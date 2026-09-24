using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// Queued work whose activity stopped being routable outside routing — purged, cancelled, completed or deleted
/// from the CRM — must leave its queue instead of being offered to an agent. Runs the real routing, queue and
/// reservation services over a SQLite store.
/// </summary>
public sealed class QueuedWorkWithdrawalTests
{
    [Fact]
    public async Task AssignQueueAsync_WhenTheWaitingActivityWasPurged_RemovesItInsteadOfOfferingIt()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        await fixture.Harness.SignInAgentAsync("agent-1", "user-1");
        var (_, queueItem) = await fixture.SeedAsync("activity-1", ActivityStatus.Purged);

        // Act
        var assigned = await fixture.AssignmentService.AssignQueueAsync(QueuedWorkWithdrawalFixture.CampaignQueueId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(0, assigned);
        Assert.Equal(QueueItemStatus.Removed, (await fixture.FindQueueItemAsync(queueItem.ItemId)).Status);
        Assert.Equal(AgentPresenceStatus.Available, await fixture.Harness.GetPresenceAsync("agent-1"));
        Assert.Empty(await fixture.Reservations.GetActiveByAgentAsync("agent-1", TestContext.Current.CancellationToken));

        var withdrawal = Assert.Single(fixture.Withdrawals);
        Assert.Equal(ContactCenterActorType.System, withdrawal.ActorType);
        Assert.Equal(queueItem.ItemId, withdrawal.AggregateId);
        Assert.Equal(QueuedWorkWithdrawalReasons.For(ActivityStatus.Purged), withdrawal.GetData<QueueItemWithdrawnEventData>().Reason);
    }

    [Fact]
    public async Task AssignQueueAsync_OnTheNextSweep_DoesNotRecordTheWithdrawalAgain()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        await fixture.Harness.SignInAgentAsync("agent-1", "user-1");
        await fixture.SeedAsync("activity-1", ActivityStatus.Cancelled);
        await fixture.AssignmentService.AssignQueueAsync(QueuedWorkWithdrawalFixture.CampaignQueueId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Act
        await fixture.AssignmentService.AssignQueueAsync(QueuedWorkWithdrawalFixture.CampaignQueueId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Single(fixture.Withdrawals);
    }

    [Fact]
    public async Task AssignQueueAsync_WhenTheWaitingActivityWasDeleted_RemovesIt()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        await fixture.Harness.SignInAgentAsync("agent-1", "user-1");
        var (_, queueItem) = await fixture.SeedAsync("activity-1", ActivityStatus.Pending, storeActivity: false);

        // Act
        await fixture.AssignmentService.AssignQueueAsync(QueuedWorkWithdrawalFixture.CampaignQueueId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(QueueItemStatus.Removed, (await fixture.FindQueueItemAsync(queueItem.ItemId)).Status);
        Assert.Equal(
            QueuedWorkWithdrawalReasons.ActivityDeleted,
            Assert.Single(fixture.Withdrawals).GetData<QueueItemWithdrawnEventData>().Reason);
    }

    [Fact]
    public async Task AssignQueueAsync_WhenDeadWorkIsAheadOfLiveWork_OffersTheLiveWork()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        await fixture.Harness.SignInAgentAsync("agent-1", "user-1");
        var (_, dead) = await fixture.SeedAsync("activity-dead", ActivityStatus.Purged);
        var (_, live) = await fixture.SeedAsync("activity-live", ActivityStatus.Pending);

        // Act
        var assigned = await fixture.AssignmentService.AssignQueueAsync(QueuedWorkWithdrawalFixture.CampaignQueueId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(1, assigned);
        Assert.Equal(QueueItemStatus.Removed, (await fixture.FindQueueItemAsync(dead.ItemId)).Status);
        Assert.Equal(QueueItemStatus.Reserved, (await fixture.FindQueueItemAsync(live.ItemId)).Status);
        var reservation = Assert.Single(await fixture.Reservations.GetActiveByAgentAsync("agent-1", TestContext.Current.CancellationToken));
        Assert.Equal("activity-live", reservation.ActivityItemId);
    }

    [Fact]
    public async Task AssignQueueAsync_WhenTheActivityIsStillRoutable_OffersIt()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        await fixture.Harness.SignInAgentAsync("agent-1", "user-1");
        var (_, queueItem) = await fixture.SeedAsync("activity-1", ActivityStatus.Pending);

        // Act
        await fixture.AssignmentService.AssignQueueAsync(QueuedWorkWithdrawalFixture.CampaignQueueId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(QueueItemStatus.Reserved, (await fixture.FindQueueItemAsync(queueItem.ItemId)).Status);
        Assert.Empty(fixture.Withdrawals);
    }

    [Fact]
    public async Task WithdrawAsync_WhenTheWorkIsWaiting_RemovesItAndNamesWhoPurgedIt()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        var (activity, queueItem) = await fixture.SeedAsync("activity-1", ActivityStatus.Pending);
        activity.Status = ActivityStatus.Purged;

        // Act
        var outcome = await fixture.WithdrawalService.WithdrawAsync(
            activity.ItemId,
            QueuedWorkWithdrawalReasons.For(ActivityStatus.Purged),
            ContactCenterActor.Supervisor("supervisor-user"),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(QueuedWorkWithdrawalOutcome.Withdrawn, outcome);
        Assert.Equal(QueueItemStatus.Removed, (await fixture.FindQueueItemAsync(queueItem.ItemId)).Status);

        var withdrawal = Assert.Single(fixture.Withdrawals);
        Assert.Equal(ContactCenterActorType.Supervisor, withdrawal.ActorType);
        Assert.Equal("supervisor-user", withdrawal.ActorId);

        var data = withdrawal.GetData<QueueItemWithdrawnEventData>();
        Assert.Equal(QueuedWorkWithdrawalFixture.CampaignQueueId, data.QueueId);
        Assert.Equal(activity.ItemId, data.ActivityItemId);
        Assert.Equal(nameof(QueueItemStatus.Waiting), data.PreviousState);
        Assert.Null(data.RevokedReservationId);
    }

    [Fact]
    public async Task WithdrawAsync_WhenTheWorkIsRingingAnAgent_RevokesTheOfferAndReleasesTheAgent()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        await fixture.Harness.SignInAgentAsync("agent-1", "user-1");
        var (activity, queueItem) = await fixture.SeedAsync("activity-1", ActivityStatus.Pending);
        await fixture.AssignmentService.AssignQueueAsync(QueuedWorkWithdrawalFixture.CampaignQueueId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();
        var offer = Assert.Single(await fixture.Reservations.GetActiveByAgentAsync("agent-1", TestContext.Current.CancellationToken));
        Assert.Equal(AgentPresenceStatus.Reserved, await fixture.Harness.GetPresenceAsync("agent-1"));
        activity.Status = ActivityStatus.Purged;

        // Act
        var outcome = await fixture.WithdrawalService.WithdrawAsync(
            activity.ItemId,
            QueuedWorkWithdrawalReasons.For(ActivityStatus.Purged),
            ContactCenterActor.Supervisor("supervisor-user"),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(QueuedWorkWithdrawalOutcome.OfferRevoked, outcome);
        Assert.Equal(QueueItemStatus.Removed, (await fixture.FindQueueItemAsync(queueItem.ItemId)).Status);
        Assert.Equal(ReservationStatus.Canceled, (await fixture.Reservations.FindByIdAsync(offer.ItemId, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(AgentPresenceStatus.Available, await fixture.Harness.GetPresenceAsync("agent-1"));

        var data = Assert.Single(fixture.Withdrawals).GetData<QueueItemWithdrawnEventData>();
        Assert.Equal(offer.ItemId, data.RevokedReservationId);
        Assert.Equal("agent-1", data.AgentId);
        Assert.Equal(nameof(QueueItemStatus.Reserved), data.PreviousState);
    }

    [Fact]
    public async Task WithdrawAsync_WhenAnAgentHasTakenTheWork_LeavesItWithThem()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        await fixture.Harness.SignInAgentAsync("agent-1", "user-1");
        var (activity, queueItem) = await fixture.SeedAsync("activity-1", ActivityStatus.Pending);
        await fixture.AssignmentService.AssignQueueAsync(QueuedWorkWithdrawalFixture.CampaignQueueId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();
        var offer = Assert.Single(await fixture.Reservations.GetActiveByAgentAsync("agent-1", TestContext.Current.CancellationToken));
        await fixture.ReservationService.AcceptAsync(offer.ItemId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();
        var presenceBefore = await fixture.Harness.GetPresenceAsync("agent-1");
        activity.Status = ActivityStatus.Purged;

        // Act
        var outcome = await fixture.WithdrawalService.WithdrawAsync(
            activity.ItemId,
            QueuedWorkWithdrawalReasons.For(ActivityStatus.Purged),
            ContactCenterActor.Supervisor("supervisor-user"),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(QueuedWorkWithdrawalOutcome.LeftWithAgent, outcome);
        Assert.Equal(QueueItemStatus.Assigned, (await fixture.FindQueueItemAsync(queueItem.ItemId)).Status);
        Assert.Equal(ReservationStatus.Accepted, (await fixture.Reservations.FindByIdAsync(offer.ItemId, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(presenceBefore, await fixture.Harness.GetPresenceAsync("agent-1"));
        Assert.Empty(fixture.Withdrawals);
    }

    [Fact]
    public async Task WithdrawAsync_WhenTheOfferWasAcceptedButTheItemStillReadsReserved_LeavesItWithTheAgent()
    {
        // Arrange: the agent answered, but the queue item has not caught up with the acceptance yet.
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        await fixture.Harness.SignInAgentAsync("agent-1", "user-1");
        var (activity, queueItem) = await fixture.SeedAsync("activity-1", ActivityStatus.Pending);
        await fixture.AssignmentService.AssignQueueAsync(QueuedWorkWithdrawalFixture.CampaignQueueId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();
        var offer = Assert.Single(await fixture.Reservations.GetActiveByAgentAsync("agent-1", TestContext.Current.CancellationToken));
        await fixture.ReservationService.AcceptAsync(offer.ItemId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        var lagging = await fixture.FindQueueItemAsync(queueItem.ItemId);
        lagging.RestorePersistedStatus(QueueItemStatus.Reserved);
        lagging.ReservationId = offer.ItemId;
        await fixture.QueueItems.UpdateAsync(lagging, cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();
        var presenceBefore = await fixture.Harness.GetPresenceAsync("agent-1");
        activity.Status = ActivityStatus.Purged;

        // Act
        var outcome = await fixture.WithdrawalService.WithdrawAsync(
            activity.ItemId,
            QueuedWorkWithdrawalReasons.For(ActivityStatus.Purged),
            ContactCenterActor.Supervisor("supervisor-user"),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(QueuedWorkWithdrawalOutcome.LeftWithAgent, outcome);
        Assert.Equal(ReservationStatus.Accepted, (await fixture.Reservations.FindByIdAsync(offer.ItemId, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(presenceBefore, await fixture.Harness.GetPresenceAsync("agent-1"));
        Assert.Empty(fixture.Withdrawals);
    }

    [Fact]
    public async Task PurgingAQueuedActivityFromTheAdmin_WithdrawsItsWorkInThePurgersName()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        await fixture.Harness.SignInAgentAsync("agent-1", "user-1");
        var (activity, queueItem) = await fixture.SeedAsync("activity-1", ActivityStatus.Pending);
        var activityManager = fixture.CreateActivityManager(currentUserId: "supervisor-user");

        // Act: the admin bulk purge marks the activity and saves it through the CRM's activity manager.
        ActivityPurgeHelper.Purge(activity, fixture.Harness.Clock.UtcNow, "supervisor-user", "supervisor");
        await activityManager.UpdateAsync(activity, cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        var assigned = await fixture.AssignmentService.AssignQueueAsync(QueuedWorkWithdrawalFixture.CampaignQueueId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(0, assigned);
        Assert.Equal(QueueItemStatus.Removed, (await fixture.FindQueueItemAsync(queueItem.ItemId)).Status);

        var withdrawal = Assert.Single(fixture.Withdrawals);
        Assert.Equal(ContactCenterActorType.Supervisor, withdrawal.ActorType);
        Assert.Equal("supervisor-user", withdrawal.ActorId);
        Assert.Equal(QueuedWorkWithdrawalReasons.For(ActivityStatus.Purged), withdrawal.GetData<QueueItemWithdrawnEventData>().Reason);
    }

    [Fact]
    public async Task WithdrawAsync_WhenNothingIsQueued_DoesNothing()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();

        // Act
        var outcome = await fixture.WithdrawalService.WithdrawAsync(
            "activity-never-queued",
            QueuedWorkWithdrawalReasons.For(ActivityStatus.Completed),
            ContactCenterActor.System,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(QueuedWorkWithdrawalOutcome.NothingQueued, outcome);
        Assert.Empty(fixture.Withdrawals);
    }
}
