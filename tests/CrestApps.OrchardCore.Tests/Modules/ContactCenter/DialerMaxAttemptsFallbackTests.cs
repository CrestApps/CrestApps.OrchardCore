using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The dialer profile's Max attempts is the fallback limit: the subject flow decides first.
/// </summary>
/// <remarks>
/// It used to be a hard cap on top of the flow, so a subject that wanted a contact tried five times stopped at the
/// profile's three, and a call that ended in a way the flow had no rule for borrowed whatever disposition "Try again"
/// was wired to. Now a "Try again" that names a maximum is the limit; one that does not stops at the profile's; and an
/// ending the flow has no rule for is dispositioned for what happened and retried by the dialer up to the profile's.
/// </remarks>
public sealed class DialerMaxAttemptsFallbackTests
{
    private static readonly DateTime _now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, 3, 3)]
    [InlineData(6, 3, 6)]
    [InlineData(2, 3, 2)]
    [InlineData(0, 3, 3)]
    public void Resolve_PrefersTheSubjectsMaximum_AndFallsBackToTheProfiles(int? subjectMaximum, int profileMaximum, int expected)
    {
        // Act
        var limit = DialerAttemptLimit.Resolve(
            new OmnichannelActivity { MaxAttempts = subjectMaximum },
            new DialerProfile { MaxAttempts = profileMaximum });

        // Assert
        Assert.Equal(expected, limit);
    }

    [Theory]
    [InlineData(6, 4, false)]
    [InlineData(2, 3, true)]
    [InlineData(null, 4, true)]
    [InlineData(null, 3, false)]
    public async Task CreatingAsync_LetsTheSubjectsMaximumDecide_WithTheProfilesAsTheFallback(int? subjectMaximum, int attempt, bool expectRefused)
    {
        // Arrange: the profile allows three attempts.
        var harness = new FollowUpHarness();
        var context = new FollowUpActivityContext
        {
            PreviousActivity = new OmnichannelActivity { ItemId = "activity-1", Source = ActivitySources.PowerDial, Attempts = attempt - 1 },
            FollowUpActivity = new OmnichannelActivity { ItemId = "activity-2", Attempts = attempt },
            CreatedBy = OmnichannelConstants.ActionTypes.TryAgain,
            MaxAttempts = subjectMaximum,
        };

        // Act
        await harness.Handler.CreatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectRefused, context.Cancel);

        if (!expectRefused)
        {
            // The attempt carries the subject's maximum on, so the dial-time checks apply the same limit.
            Assert.Equal(subjectMaximum, context.FollowUpActivity.MaxAttempts);
        }
    }

    [Theory]
    [InlineData(false, 24 * 60, 60)]
    [InlineData(true, 24 * 60, 24 * 60)]
    [InlineData(true, 5, 60)]
    [InlineData(false, 5, 60)]
    public async Task CreatingAsync_UsesTheProfilesRetryDelay_WhenTheSubjectChoseNoSchedule(bool hasChosenSchedule, int scheduledInMinutes, int expectedDueInMinutes)
    {
        // Arrange: the profile waits 60 minutes between attempts, and the last call ended now.
        var harness = new FollowUpHarness();
        var context = new FollowUpActivityContext
        {
            PreviousActivity = new OmnichannelActivity { ItemId = "activity-1", Source = ActivitySources.PowerDial, Attempts = 1, CompletedUtc = _now },
            FollowUpActivity = new OmnichannelActivity { ItemId = "activity-2", Attempts = 2, ScheduledUtc = _now.AddMinutes(scheduledInMinutes) },
            CreatedBy = OmnichannelConstants.ActionTypes.TryAgain,
            HasChosenSchedule = hasChosenSchedule,
        };

        // Act
        await harness.Handler.CreatingAsync(context, TestContext.Current.CancellationToken);

        // Assert: a schedule the subject chose stands, never sooner than the delay; one it did not is the delay.
        Assert.False(context.Cancel);
        Assert.Equal(_now.AddMinutes(expectedDueInMinutes), context.FollowUpActivity.ScheduledUtc);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, null)]
    public async Task TryAgainWithNoScheduleOrMaximum_OnDialerWork_RetriesAfterTheRetryDelay_UntilTheProfilesMaxAttempts(int completedAttempt, int? expectedNextAttempt)
    {
        // Arrange: the live case end to end from the disposition on. A voicemail answered attempt N, the dialer
        // dispositioned it "No answer", and the subject's Try again names neither a schedule nor a maximum. The real
        // executor creates the next attempt and the real dialer handler applies the profile: three attempts, an hour
        // apart.
        var action = new SubjectAction
        {
            ItemId = "try-again",
            Source = OmnichannelConstants.ActionTypes.TryAgain,
            SubjectContentType = "LeadGeneration",
            DispositionId = "no-answer",
        };
        action.Put(new TryAgainActionMetadata { AssignmentType = SubjectActionOwnerAssignmentType.SameOwner });

        var actionCatalog = new Mock<global::CrestApps.Core.Services.ISourceCatalog<SubjectAction>>();
        actionCatalog
            .Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { action });

        OmnichannelActivity saved = null;
        var session = new Mock<global::YesSql.ISession>();
        session
            .Setup(value => value.SaveAsync(It.IsAny<object>(), false, OmnichannelConstants.CollectionName, It.IsAny<CancellationToken>()))
            .Callback<object, bool, string, CancellationToken>((entity, _, _, _) => saved = (OmnichannelActivity)entity)
            .Returns(Task.CompletedTask);

        var executor = new global::CrestApps.OrchardCore.Omnichannel.Managements.Services.DefaultSubjectActionExecutor(
            actionCatalog.Object,
            Mock.Of<ISubjectFlowSettingsService>(),
            Mock.Of<global::OrchardCore.ContentManagement.IContentManager>(),
            Mock.Of<INotInServiceNumberService>(),
            session.Object,
            Clock(),
            Mock.Of<ILocalClock>(),
            global::CrestApps.OrchardCore.Tests.Modules.Omnichannel.TestContactMethods.Accessor,
            NullLogger<global::CrestApps.OrchardCore.Omnichannel.Managements.Services.DefaultSubjectActionExecutor>.Instance,
            followUpHandlers: [new FollowUpHarness().Handler]);

        var context = new SubjectActionExecutionContext
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                Source = ActivitySources.PowerDial,
                SubjectContentType = "LeadGeneration",
                Attempts = completedAttempt,
                CompletedUtc = _now,
                CompletedById = "agent-user",
                CompletedByUsername = "agent",
            },
            Disposition = new OmnichannelDisposition { ItemId = "no-answer", Outcome = DispositionOutcome.NoAnswer },
        };

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        if (expectedNextAttempt is null)
        {
            // The profile's three attempts are used: no fourth is created, rather than one a day forever.
            Assert.Null(saved);

            return;
        }

        Assert.NotNull(saved);
        Assert.Equal(expectedNextAttempt, saved.Attempts);
        Assert.Equal(_now.AddMinutes(60), saved.ScheduledUtc);
        Assert.Equal("profile-1", saved.DialerProfileId);
        Assert.Null(saved.MaxAttempts);
    }

    [Theory]
    [InlineData(DialerAttemptOutcomes.NoAnswer, false, true)]
    [InlineData(DialerAttemptOutcomes.Busy, false, true)]
    [InlineData(DialerAttemptOutcomes.AnsweringMachine, false, true)]
    [InlineData(DialerAttemptOutcomes.Disconnected, false, true)]
    [InlineData(DialerAttemptOutcomes.Failed, false, false)]
    [InlineData(DialerAttemptOutcomes.Rejected, false, false)]
    [InlineData(DialerAttemptOutcomes.NoAnswer, true, false)]
    public async Task FinalizeAsync_RetriesUnderTheProfile_OnlyWhenTheFlowHasNoRuleForTheOutcome(string outcome, bool handledBySubjectFlow, bool expectRetry)
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            Source = ActivitySources.PowerDial,
            Status = ActivityStatus.NotStated,
            SubjectContentType = "LeadFollowUp",
        };

        var harness = new FinalizerHarness(activity, handledBySubjectFlow);

        // Act
        var result = await harness.Finalizer.FinalizeAsync(new DialerAttemptFinalizationRequest
        {
            ActivityItemId = activity.ItemId,
            Outcome = outcome,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialerAttemptFinalizationResult.Dispositioned, result);
        harness.Completer.Verify(
            value => value.CompleteAsync(It.Is<ActivityOutcomeCompletionRequest>(request => !request.PreferRetriedDisposition), It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Scheduler.Verify(
            value => value.ScheduleUnhandledOutcomeRetryAsync(activity.ItemId, It.IsAny<CancellationToken>()),
            expectRetry ? Times.Once() : Times.Never());
    }

    // A record that has used every attempt is finalized with the attempt-limit reason, and is not tried again.
    [Fact]
    public async Task FinalizeAsync_DoesNotRetryARecordThatUsedEveryAttempt()
    {
        // Arrange
        var activity = new OmnichannelActivity { ItemId = "activity-1", Source = ActivitySources.PowerDial, Status = ActivityStatus.NotStated };
        var harness = new FinalizerHarness(activity, handledBySubjectFlow: false);

        // Act
        await harness.Finalizer.FinalizeAsync(new DialerAttemptFinalizationRequest
        {
            ActivityItemId = activity.ItemId,
            Outcome = DialerAttemptOutcomes.NoAnswer,
            TerminalReasonCode = DialerTerminalReasons.MaxAttemptsReached,
        }, TestContext.Current.CancellationToken);

        // Assert
        harness.Scheduler.Verify(
            value => value.ScheduleUnhandledOutcomeRetryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // The dialer retries a call its flow has no rule for as soon as it ends, and a workflow may ask too: the second
    // request finds the attempt the first scheduled rather than calling the contact twice.
    [Fact]
    public async Task ScheduleRetryAsync_WhenTheNextAttemptIsAlreadyScheduled_ReturnsItWithoutCreatingAnother()
    {
        // Arrange
        var activity = new OmnichannelActivity { ItemId = "activity-1", Source = ActivitySources.PowerDial, Status = ActivityStatus.Completed };
        activity.Put(new DialerRetryRecord
        {
            NextActivityItemId = "activity-2",
            AttemptNumber = 2,
            DueUtc = _now.AddMinutes(30),
            CreatedBy = DialerRetryScheduler.CreatedByDialer,
        });

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(value => value.FindByIdAsync("activity-1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var scheduler = new DialerRetryScheduler(
            activityManager.Object,
            [],
            Clock(),
            NullLogger<DialerRetryScheduler>.Instance);

        // Act
        var result = await scheduler.ScheduleRetryAsync("activity-1", delayMinutes: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialerRetryScheduleStatus.Scheduled, result.Status);
        Assert.Equal("activity-2", result.NextActivityItemId);
        activityManager.Verify(value => value.CreateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScheduleUnhandledOutcomeRetryAsync_CreatesTheNextAttempt_AndRecordsIt()
    {
        // Arrange
        var activity = new OmnichannelActivity { ItemId = "activity-1", Source = ActivitySources.PowerDial, Status = ActivityStatus.Completed, Attempts = 1 };

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(value => value.FindByIdAsync("activity-1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        activityManager.Setup(value => value.CreateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        activityManager.Setup(value => value.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        FollowUpActivityContext seen = null;
        var handler = new Mock<IFollowUpActivityHandler>();
        handler
            .Setup(value => value.CreatingAsync(It.IsAny<FollowUpActivityContext>(), It.IsAny<CancellationToken>()))
            .Callback<FollowUpActivityContext, CancellationToken>((context, _) => seen = context)
            .Returns(Task.CompletedTask);

        var scheduler = new DialerRetryScheduler(activityManager.Object, [handler.Object], Clock(), NullLogger<DialerRetryScheduler>.Instance);

        // Act
        var result = await scheduler.ScheduleUnhandledOutcomeRetryAsync("activity-1", TestContext.Current.CancellationToken);

        // Assert: the dialer's own retry sets no maximum, so the profile's Max attempts is the limit the handler applies.
        Assert.Equal(DialerRetryScheduleStatus.Scheduled, result.Status);
        Assert.Equal(2, result.AttemptNumber);
        Assert.NotNull(seen);
        Assert.Null(seen.MaxAttempts);
        Assert.Equal(DialerRetryScheduler.CreatedByDialer, seen.CreatedBy);
        Assert.True(activity.TryGet<DialerRetryRecord>(out var record));
        Assert.Equal(result.NextActivityItemId, record.NextActivityItemId);
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private sealed class FollowUpHarness
    {
        public FollowUpHarness()
        {
            var queueItems = new Mock<IQueueItemManager>();
            queueItems
                .Setup(value => value.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueItem
                {
                    ItemId = "item-1",
                    QueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1"),
                    ActivityItemId = "activity-1",
                    DialerProfileId = "profile-1",
                });

            var profiles = new Mock<IDialerProfileReader>();
            profiles
                .Setup(value => value.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DialerProfile { ItemId = "profile-1", Name = "Profile", MaxAttempts = 3, RetryDelayMinutes = 60 });

            var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
            scopeExecutor.Setup(value => value.ScheduleAfterCommit(It.IsAny<Func<IActivityQueueService, Task>>())).Returns(true);

            Handler = new DialerFollowUpActivityHandler(
                queueItems.Object,
                profiles.Object,
                new Mock<IInteractionManager>().Object,
                scopeExecutor.Object,
                Clock(),
                NullLogger<DialerFollowUpActivityHandler>.Instance);
        }

        public DialerFollowUpActivityHandler Handler { get; }
    }

    private sealed class FinalizerHarness
    {
        public FinalizerHarness(OmnichannelActivity activity, bool handledBySubjectFlow)
        {
            var activityManager = new Mock<IOmnichannelActivityManager>();
            activityManager.Setup(value => value.FindByIdAsync(activity.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

            Completer
                .Setup(value => value.CompleteAsync(It.IsAny<ActivityOutcomeCompletionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var result = ActivityDispositionResult.Success(activity);
                    result.HandledBySubjectFlow = handledBySubjectFlow;

                    return result;
                });

            Scheduler
                .Setup(value => value.ScheduleUnhandledOutcomeRetryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DialerRetryScheduleResult { Status = DialerRetryScheduleStatus.Scheduled });

            var services = new ServiceCollection()
                .AddSingleton(Completer.Object)
                .AddSingleton(Scheduler.Object)
                .BuildServiceProvider();

            Finalizer = new DialerAttemptFinalizer(
                activityManager.Object,
                new Mock<IInteractionManager>().Object,
                new Mock<IContactCenterWorkStateService>().Object,
                services,
                NullLogger<DialerAttemptFinalizer>.Instance);
        }

        public Mock<IActivityOutcomeCompleter> Completer { get; } = new();

        public Mock<IDialerRetryScheduler> Scheduler { get; } = new();

        public DialerAttemptFinalizer Finalizer { get; }
    }
}
