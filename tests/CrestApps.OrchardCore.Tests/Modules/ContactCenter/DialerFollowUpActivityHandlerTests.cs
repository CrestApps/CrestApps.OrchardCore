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
/// A disposition's "Try again", or a workflow, creates the next attempt of a dialer record as a new activity. When the last
/// call of the contact was abandoned -- a person answered and nobody was there -- the next attempt must reach the dialer
/// marked, so an over-dialing campaign calls the person again only with an agent reserved for them.
/// </summary>
public sealed class DialerFollowUpActivityHandlerTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public async Task CreatingAsync_QueuesTheFollowUp_MarkedWhenTheLastCallWasAbandoned(bool lastCallAbandoned, bool previousItemMarked, bool expectedMark)
    {
        // Arrange
        var previousItem = new QueueItem
        {
            ItemId = "item-1",
            QueueId = "__campaign-queue__campaign-1",
            ActivityItemId = "activity-1",
            DialerProfileId = "profile-1",
            RequiresReservedAgent = previousItemMarked,
        };

        var queueItems = new Mock<IQueueItemManager>();
        queueItems.Setup(value => value.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>())).ReturnsAsync(previousItem);

        var profiles = new Mock<IDialerProfileReader>();
        profiles.Setup(value => value.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialerProfile { ItemId = "profile-1", Name = "Profile", MaxAttempts = 3 });

        var interaction = new Interaction { ItemId = "interaction-1", ActivityItemId = "activity-1" };

        if (lastCallAbandoned)
        {
            DialerCallMetadata.MarkAbandoned(interaction, DialerAbandonment.Reasons.NoAgentAvailable);
        }

        var interactions = new Mock<IInteractionManager>();
        interactions.Setup(value => value.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        Func<IActivityQueueService, Task> queued = null;
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(value => value.ScheduleAfterCommit(It.IsAny<Func<IActivityQueueService, Task>>()))
            .Callback<Func<IActivityQueueService, Task>>(operation => queued = operation)
            .Returns(true);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc));

        var handler = new DialerFollowUpActivityHandler(
            queueItems.Object,
            profiles.Object,
            interactions.Object,
            scopeExecutor.Object,
            clock.Object,
            NullLogger<DialerFollowUpActivityHandler>.Instance);

        var context = new FollowUpActivityContext
        {
            PreviousActivity = new OmnichannelActivity { ItemId = "activity-1", Source = ActivitySources.Dialer },
            FollowUpActivity = new OmnichannelActivity { ItemId = "activity-2", Attempts = 2 },
            CreatedBy = OmnichannelConstants.ActionTypes.TryAgain,
        };

        // Act
        await handler.CreatingAsync(context, TestContext.Current.CancellationToken);

        // Assert: queued once committed, in the same queue with the same profile, marked as the last call warrants.
        Assert.False(context.Cancel);
        Assert.NotNull(queued);

        var queueService = new Mock<IActivityQueueService>();
        await queued(queueService.Object);

        queueService.Verify(value => value.EnqueueAsync("activity-2", previousItem.QueueId, null, "profile-1", expectedMark, CancellationToken.None), Times.Once);
    }
}
