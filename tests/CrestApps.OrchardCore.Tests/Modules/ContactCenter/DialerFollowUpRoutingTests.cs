using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The subject flow decides who makes the next call: a follow-up a "Try again" gave to an agent is not queued for the
/// dialer, and one it sent to the dialer is queued on its own campaign under its own profile.
/// </summary>
public sealed class DialerFollowUpRoutingTests
{
    [Fact]
    public async Task CreatingAsync_WhenTheActionGaveTheCallToAnAgent_DoesNotQueueIt()
    {
        var (handler, scopeExecutor, _) = CreateHandler();

        var context = new FollowUpActivityContext
        {
            PreviousActivity = new OmnichannelActivity { ItemId = "activity-1", Source = ActivitySources.PowerDial },
            FollowUpActivity = new OmnichannelActivity { ItemId = "activity-2", Attempts = 2, Source = ActivitySources.Manual },
            CreatedBy = OmnichannelConstants.ActionTypes.TryAgain,
            Route = FollowUpRoutingTarget.Agent,
        };

        await handler.CreatingAsync(context, TestContext.Current.CancellationToken);

        Assert.False(context.Cancel);
        Assert.Null(scopeExecutor.Queued);
    }

    [Fact]
    public async Task CreatingAsync_WhenTheActionSentManualWorkToTheDialer_QueuesItOnItsCampaignUnderItsProfile()
    {
        var (handler, scopeExecutor, queueService) = CreateHandler();

        var context = new FollowUpActivityContext
        {
            PreviousActivity = new OmnichannelActivity { ItemId = "activity-1", Source = ActivitySources.Manual },
            FollowUpActivity = new OmnichannelActivity
            {
                ItemId = "activity-2",
                Attempts = 1,
                Source = ActivitySources.PowerDial,
                CampaignId = "campaign-2",
                DialerProfileId = "profile-2",
            },
            CreatedBy = OmnichannelConstants.ActionTypes.NewActivity,
            Route = FollowUpRoutingTarget.Dialer,
            HasNamedOwner = true,
            HasChosenSchedule = true,
        };

        await handler.CreatingAsync(context, TestContext.Current.CancellationToken);

        Assert.False(context.Cancel);
        Assert.NotNull(scopeExecutor.Queued);
        await scopeExecutor.Queued(queueService.Object);
        queueService.Verify(value => value.EnqueueAsync(
            "activity-2",
            ContactCenterConstants.CampaignQueue.CreateId("campaign-2"),
            null,
            "profile-2",
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (DialerFollowUpActivityHandler Handler, RecordingScopeExecutor ScopeExecutor, Mock<IActivityQueueService> QueueService) CreateHandler()
    {
        var profiles = new Mock<IDialerProfileReader>();
        profiles.Setup(value => value.FindByIdAsync("profile-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialerProfile { ItemId = "profile-2", Name = "Power", MaxAttempts = 3 });

        var scopeExecutor = new RecordingScopeExecutor();

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc));

        var handler = new DialerFollowUpActivityHandler(
            Mock.Of<IQueueItemManager>(),
            profiles.Object,
            Mock.Of<IInteractionManager>(),
            scopeExecutor.Mock.Object,
            clock.Object,
            NullLogger<DialerFollowUpActivityHandler>.Instance);

        return (handler, scopeExecutor, new Mock<IActivityQueueService>());
    }

    private sealed class RecordingScopeExecutor
    {
        public RecordingScopeExecutor()
        {
            Mock
                .Setup(value => value.ScheduleAfterCommit(It.IsAny<Func<IActivityQueueService, Task>>()))
                .Callback<Func<IActivityQueueService, Task>>(operation => Queued = operation)
                .Returns(true);
        }

        public Mock<IContactCenterScopeExecutor> Mock { get; } = new();

        public Func<IActivityQueueService, Task> Queued { get; private set; }
    }
}
