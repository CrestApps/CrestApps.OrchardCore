using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class OrphanedActivityRecoveryServiceTests
{
    private static readonly DateTime _now = new(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecoverCandidates_WhenReservationIsStillLive_LeavesTheRecordAlone()
    {
        // Arrange - an offer that is still ringing (live reservation) is owned by the reservation-expiry sweep.
        var harness = new Harness();
        var activity = harness.NewActivity(ActivityStatus.Reserved, reservationId: "res-1");

        harness.Reservations
            .Setup(m => m.FindByIdAsync("res-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ExpiresUtc = _now.AddMinutes(5) });

        // Act
        var recovered = await harness.Service.RecoverCandidatesAsync([activity], _now, CancellationToken.None);

        // Assert
        Assert.Equal(0, recovered);
        harness.VerifyNoRecovery();
    }

    [Fact]
    public async Task RecoverCandidates_WhenInteractionIsUnsettled_LeavesTheRecordAlone()
    {
        // Arrange - a connected, still-live call must never be touched, however old its reservation is.
        var harness = new Harness();
        var activity = harness.NewActivity(ActivityStatus.InProgress);

        harness.Interactions
            .Setup(m => m.FindByActivityIdAsync(activity.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction());

        // Act
        var recovered = await harness.Service.RecoverCandidatesAsync([activity], _now, CancellationToken.None);

        // Assert
        Assert.Equal(0, recovered);
        harness.VerifyNoRecovery();
    }

    [Fact]
    public async Task RecoverCandidates_WhenStatusIsInProgressAndSettled_TerminatesWithoutRedialing()
    {
        // Arrange - a stranded InProgress record may already have reached the customer, so it is failed, not dialed.
        var harness = new Harness();
        var activity = harness.NewActivity(ActivityStatus.InProgress, campaignId: "campaign-1");

        // Act
        var recovered = await harness.Service.RecoverCandidatesAsync([activity], _now, CancellationToken.None);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(ActivityStatus.Failed, activity.Status);
        Assert.Equal("orphaned-recovered", activity.TerminalReasonCode);
        Assert.Equal(_now, activity.CompletedUtc);
        harness.VerifyNeverEnqueued();
    }

    [Fact]
    public async Task RecoverCandidates_WhenPreAnswerStatusButInteractionWasAnswered_Terminates()
    {
        // Arrange - the status says pre-answer, but an answered interaction proves the customer was reached.
        var harness = new Harness();
        var activity = harness.NewActivity(ActivityStatus.AwaitingCustomerAnswer, campaignId: "campaign-1");

        harness.Interactions
            .Setup(m => m.FindByActivityIdAsync(activity.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { AnsweredUtc = _now.AddMinutes(-20) }
                .RestorePersistedStatus(InteractionStatus.Ended));

        // Act
        var recovered = await harness.Service.RecoverCandidatesAsync([activity], _now, CancellationToken.None);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(ActivityStatus.Failed, activity.Status);
        harness.VerifyNeverEnqueued();
    }

    [Fact]
    public async Task RecoverCandidates_WhenNeverAnswered_ReturnsToPendingAndReQueues()
    {
        // Arrange - a pre-answer record with no answered interaction was never reached, so it is safe to retry.
        var harness = new Harness();
        var activity = harness.NewActivity(ActivityStatus.Reserved, campaignId: "campaign-1");

        // Act
        var recovered = await harness.Service.RecoverCandidatesAsync([activity], _now, CancellationToken.None);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        Assert.Null(activity.ReservationId);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        harness.Queues.Verify(
            q => q.EnqueueAsync(activity.ItemId, "__campaign-queue__campaign-1", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecoverCandidates_WhenNeverAnsweredButNoCampaign_ReturnsToPendingWithoutReQueuing()
    {
        // Arrange - without a campaign there is no campaign queue to re-enqueue onto; still clear the stuck status.
        var harness = new Harness();
        var activity = harness.NewActivity(ActivityStatus.Dialing);

        // Act
        var recovered = await harness.Service.RecoverCandidatesAsync([activity], _now, CancellationToken.None);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        harness.VerifyNeverEnqueued();
    }

    [Fact]
    public async Task RecoverCandidates_WhenActivityIsAiAutomatic_LeavesTheRecordAlone()
    {
        // Arrange - an AI-automatic activity is owned end to end by the omnichannel AI voice processor. It carries
        // no Contact Center reservation or interaction, so without this guard an InProgress AI call would be read as
        // a connected orphan and terminated to Failed, racing the call's own hangup conclusion.
        var harness = new Harness();
        var activity = harness.NewActivity(
            ActivityStatus.InProgress,
            campaignId: "campaign-1",
            interactionType: ActivityInteractionType.Automated);

        // Act
        var recovered = await harness.Service.RecoverCandidatesAsync([activity], _now, CancellationToken.None);

        // Assert - it is neither counted, mutated, nor re-enqueued.
        Assert.Equal(0, recovered);
        Assert.Equal(ActivityStatus.InProgress, activity.Status);
        harness.VerifyNoRecovery();
    }

    [Fact]
    public async Task RecoverCandidates_WhenOneRecordThrows_StillRecoversTheRest()
    {
        // Arrange - a failure on one record must not abort the batch.
        var harness = new Harness();
        var faulted = harness.NewActivity(ActivityStatus.InProgress, itemId: "faulted");
        var healthy = harness.NewActivity(ActivityStatus.InProgress, itemId: "healthy", campaignId: "campaign-1");

        harness.Interactions
            .Setup(m => m.FindByActivityIdAsync("faulted", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var recovered = await harness.Service.RecoverCandidatesAsync([faulted, healthy], _now, CancellationToken.None);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(ActivityStatus.Failed, healthy.Status);
    }

    private sealed class Harness
    {
        private readonly Dictionary<string, OmnichannelActivity> _activities = new(StringComparer.Ordinal);

        public Mock<IInteractionManager> Interactions { get; } = new();

        public Mock<IActivityReservationManager> Reservations { get; } = new();

        public Mock<IContactCenterActivityWriter> Writer { get; } = new();

        public Mock<IContactCenterWorkStateService> WorkState { get; } = new();

        public Mock<IActivityQueueService> Queues { get; } = new();

        public Mock<IQueueItemManager> QueueItems { get; } = new();

        public OrphanedActivityRecoveryService Service { get; }

        public Harness()
        {
            // The writer loads the activity by id and applies the mutation; here it looks the activity up in the
            // registry and applies the mutation to that same instance so the test can assert the outcome.
            Writer
                .Setup(w => w.UpdateAsync(It.IsAny<string>(), It.IsAny<Action<OmnichannelActivity>>(), It.IsAny<CancellationToken>()))
                .Returns((string id, Action<OmnichannelActivity> mutate, CancellationToken _) =>
                {
                    if (_activities.TryGetValue(id, out var activity))
                    {
                        mutate(activity);
                    }

                    return Task.CompletedTask;
                });

            WorkState
                .Setup(s => s.MutateAsync(It.IsAny<string>(), It.IsAny<Action<ContactCenterWorkState>>(), It.IsAny<CancellationToken>()))
                .Returns((string _, Action<ContactCenterWorkState> mutate, CancellationToken _) =>
                {
                    var state = new ContactCenterWorkState();
                    mutate(state);

                    return Task.FromResult(state);
                });

            Queues
                .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((QueueItem)null);

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(_now);

            Service = new OrphanedActivityRecoveryService(
                Mock.Of<ISession>(),
                Interactions.Object,
                Reservations.Object,
                Writer.Object,
                WorkState.Object,
                Queues.Object,
                QueueItems.Object,
                clock.Object,
                NullLogger<OrphanedActivityRecoveryService>.Instance);
        }

        public OmnichannelActivity NewActivity(
            ActivityStatus status,
            string itemId = "activity-1",
            string reservationId = null,
            string campaignId = null,
            ActivityInteractionType interactionType = ActivityInteractionType.Manual)
        {
            var activity = new OmnichannelActivity
            {
                ItemId = itemId,
                Status = status,
                ReservationId = reservationId,
                ReservedUtc = _now.AddMinutes(-30),
                CampaignId = campaignId,
                InteractionType = interactionType,
            };

            _activities[itemId] = activity;

            return activity;
        }

        public void VerifyNoRecovery()
        {
            Writer.Verify(
                w => w.UpdateAsync(It.IsAny<string>(), It.IsAny<Action<OmnichannelActivity>>(), It.IsAny<CancellationToken>()),
                Times.Never);
            VerifyNeverEnqueued();
        }

        public void VerifyNeverEnqueued()
            => Queues.Verify(
                q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }
}
