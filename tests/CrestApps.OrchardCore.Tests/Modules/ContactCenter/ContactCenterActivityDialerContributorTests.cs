using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterActivityDialerContributorTests
{
    [Fact]
    public async Task GetProfilesAsync_WhenProfilesExist_MapsImplementationNeutralDescriptors()
    {
        // Arrange
        var profileManager = new Mock<IDialerProfileManager>();
        profileManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateProfile("profile-1", "Preview profile", DialerMode.Preview),
                CreateProfile("profile-2", null, DialerMode.Power),
            ]);
        var contributor = new ContactCenterActivityDialerContributor(
            profileManager.Object,
            Mock.Of<IActivityQueueService>(),
            Mock.Of<IQueueItemManager>(),
            Mock.Of<global::YesSql.ISession>(),
            Mock.Of<global::OrchardCore.Locking.Distributed.IDistributedLock>(),
            global::Microsoft.Extensions.Options.Options.Create(new ContactCenterCoordinationOptions()));

        // Act
        var descriptors = (await contributor.GetProfilesAsync(TestContext.Current.CancellationToken)).ToArray();

        // Assert: a descriptor is reusable settings only — it no longer carries a campaign or routing target.
        Assert.Collection(
            descriptors,
            descriptor =>
            {
                Assert.Equal("profile-1", descriptor.ProfileId);
                Assert.Equal("Preview profile", descriptor.DisplayName);
                Assert.Equal(ActivitySources.PreviewDial, descriptor.ActivitySource);
            },
            descriptor =>
            {
                Assert.Equal("profile-2", descriptor.ProfileId);
                Assert.Equal("profile-2", descriptor.DisplayName);
                Assert.Equal(ActivitySources.PowerDial, descriptor.ActivitySource);
            });
    }

    [Fact]
    public async Task FindByIdAsync_WhenProfileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var profileManager = new Mock<IDialerProfileManager>();
        profileManager
            .Setup(manager => manager.FindByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DialerProfile)null);
        var contributor = new ContactCenterActivityDialerContributor(
            profileManager.Object,
            Mock.Of<IActivityQueueService>(),
            Mock.Of<IQueueItemManager>(),
            Mock.Of<global::YesSql.ISession>(),
            Mock.Of<global::OrchardCore.Locking.Distributed.IDistributedLock>(),
            global::Microsoft.Extensions.Options.Options.Create(new ContactCenterCoordinationOptions()));

        // Act
        var descriptor = await contributor.FindByIdAsync(
            "missing",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(descriptor);
    }

    [Fact]
    public async Task EnqueueAsync_DerivesTheCampaignQueueAndStampsTheProfile()
    {
        // Arrange
        var queueService = new Mock<IActivityQueueService>();
        var contributor = new ContactCenterActivityDialerContributor(
            Mock.Of<IDialerProfileManager>(),
            queueService.Object,
            Mock.Of<IQueueItemManager>(),
            Mock.Of<global::YesSql.ISession>(),
            Mock.Of<global::OrchardCore.Locking.Distributed.IDistributedLock>(),
            global::Microsoft.Extensions.Options.Options.Create(new ContactCenterCoordinationOptions()));
        var profile = new ActivityDialerProfileDescriptor
        {
            ProfileId = "profile-1",
        };

        // Act
        await contributor.EnqueueAsync(
            "activity-1",
            "campaign-1",
            profile,
            TestContext.Current.CancellationToken);

        // Assert: the routing target is the campaign's virtual queue (derived from the campaign, not the profile),
        // and the profile is stamped on the queue item so the pacer can apply its settings.
        var campaignQueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");

        queueService.Verify(
            service => service.EnqueueAsync(
                "activity-1",
                campaignQueueId,
                null,
                "profile-1",
                TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueAsync_WhenCampaignIsMissing_Throws()
    {
        // Arrange
        var contributor = new ContactCenterActivityDialerContributor(
            Mock.Of<IDialerProfileManager>(),
            Mock.Of<IActivityQueueService>(),
            Mock.Of<IQueueItemManager>(),
            Mock.Of<global::YesSql.ISession>(),
            Mock.Of<global::OrchardCore.Locking.Distributed.IDistributedLock>(),
            global::Microsoft.Extensions.Options.Options.Create(new ContactCenterCoordinationOptions()));
        var profile = new ActivityDialerProfileDescriptor
        {
            ProfileId = "profile-1",
        };

        // Act and assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            contributor.EnqueueAsync(
                "activity-1",
                string.Empty,
                profile,
                TestContext.Current.CancellationToken));
    }

    // Moving a waiting record to another dialer profile used to leave its queue item on the old profile, because
    // queueing a record that is already waiting returns its queue item untouched. It keeps its place and takes the new one.
    [Fact]
    public async Task EnqueueAsync_WhenTheRecordIsAlreadyWaitingInTheCampaignQueue_RetagsItWithTheNewProfile()
    {
        // Arrange
        var campaignQueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");
        var waiting = new QueueItem
        {
            ItemId = "item-1",
            QueueId = campaignQueueId,
            ActivityItemId = "activity-1",
            DialerProfileId = "preview-profile",
        }.RestorePersistedStatus(QueueItemStatus.Waiting);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(waiting);
        queueItemManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<QueueItem>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var queueService = new Mock<IActivityQueueService>();
        var contributor = new ContactCenterActivityDialerContributor(
            Mock.Of<IDialerProfileManager>(),
            queueService.Object,
            queueItemManager.Object,
            Mock.Of<global::YesSql.ISession>(),
            Mock.Of<global::OrchardCore.Locking.Distributed.IDistributedLock>(),
            global::Microsoft.Extensions.Options.Options.Create(new ContactCenterCoordinationOptions()));

        // Act
        await contributor.EnqueueAsync(
            "activity-1",
            "campaign-1",
            new ActivityDialerProfileDescriptor { ProfileId = "power-profile" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("power-profile", waiting.DialerProfileId);
        Assert.Equal(QueueItemStatus.Waiting, waiting.Status);
        queueItemManager.Verify(
            manager => manager.UpdateAsync(waiting, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Once);
        queueService.Verify(
            service => service.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<InteractionPriority?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetWaitingRecordsAsync_ReadsTheCampaignQueueWithEachRecordsProfile()
    {
        // Arrange
        var campaignQueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.GetWaitingAsync(campaignQueueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new QueueItem { ItemId = "item-1", ActivityItemId = "activity-1", DialerProfileId = "preview-profile" },
                new QueueItem { ItemId = "item-2", ActivityItemId = "activity-2", DialerProfileId = "power-profile" },
            ]);

        var contributor = new ContactCenterActivityDialerContributor(
            Mock.Of<IDialerProfileManager>(),
            Mock.Of<IActivityQueueService>(),
            queueItemManager.Object,
            Mock.Of<global::YesSql.ISession>(),
            Mock.Of<global::OrchardCore.Locking.Distributed.IDistributedLock>(),
            global::Microsoft.Extensions.Options.Options.Create(new ContactCenterCoordinationOptions()));

        // Act
        var records = await contributor.GetWaitingRecordsAsync("campaign-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Collection(
            records,
            record =>
            {
                Assert.Equal("activity-1", record.ActivityId);
                Assert.Equal("preview-profile", record.ProfileId);
            },
            record =>
            {
                Assert.Equal("activity-2", record.ActivityId);
                Assert.Equal("power-profile", record.ProfileId);
            });
    }

    private static DialerProfile CreateProfile(
        string profileId,
        string name,
        DialerMode mode)
    {
        return new DialerProfile
        {
            ItemId = profileId,
            Name = name,
            Mode = mode,
        };
    }
    // Moving a record to another campaign moved its label and left it waiting in the old campaign's queue, where agents
    // of the old campaign dialed it. It is taken out of the old queue and waits in the new one under its profile.
    [Fact]
    public async Task MoveToCampaignAsync_WhenTheRecordWaitsInAnotherCampaign_MovesItToTheNewCampaignsQueue()
    {
        // Arrange
        var oldQueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1");
        var newQueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-2");
        var waiting = new QueueItem
        {
            ItemId = "item-1",
            QueueId = oldQueueId,
            ActivityItemId = "activity-1",
            DialerProfileId = "power-profile",
            Priority = InteractionPriority.High,
        }.RestorePersistedStatus(QueueItemStatus.Waiting);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(waiting);
        queueItemManager
            .Setup(manager => manager.FindByIdAsync("item-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(waiting);

        var queueService = new Mock<IActivityQueueService>();
        var contributor = CreateMovingContributor(queueService.Object, queueItemManager.Object, lockAcquired: true);

        // Act
        var result = await contributor.MoveToCampaignAsync("activity-1", "campaign-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityDialerCampaignMoveResult.Moved, result);
        queueService.Verify(service => service.DequeueAsync(waiting, QueueItemStatus.Removed, It.IsAny<CancellationToken>()), Times.Once);
        queueService.Verify(
            service => service.EnqueueAsync("activity-1", newQueueId, InteractionPriority.High, "power-profile", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // A record being dialed, or on a call an agent took, is left where it is: moving it would pull it from a live call.
    [Theory]
    [InlineData(QueueItemStatus.Reserved)]
    [InlineData(QueueItemStatus.Assigned)]
    public async Task MoveToCampaignAsync_WhenTheDialerIsWorkingTheRecord_LeavesItWhereItIs(QueueItemStatus status)
    {
        // Arrange
        var item = new QueueItem
        {
            ItemId = "item-1",
            QueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1"),
            ActivityItemId = "activity-1",
        }.RestorePersistedStatus(status);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var queueService = new Mock<IActivityQueueService>();
        var contributor = CreateMovingContributor(queueService.Object, queueItemManager.Object, lockAcquired: true);

        // Act
        var result = await contributor.MoveToCampaignAsync("activity-1", "campaign-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityDialerCampaignMoveResult.InFlight, result);
        queueService.Verify(service => service.DequeueAsync(It.IsAny<QueueItem>(), It.IsAny<QueueItemStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Routing holds the queue lock while it offers work; a record it may be offering right now is not moved under it.
    [Fact]
    public async Task MoveToCampaignAsync_WhenRoutingHoldsTheQueue_LeavesTheRecordWhereItIs()
    {
        // Arrange
        var waiting = new QueueItem
        {
            ItemId = "item-1",
            QueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1"),
            ActivityItemId = "activity-1",
        }.RestorePersistedStatus(QueueItemStatus.Waiting);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(waiting);

        var queueService = new Mock<IActivityQueueService>();
        var contributor = CreateMovingContributor(queueService.Object, queueItemManager.Object, lockAcquired: false);

        // Act
        var result = await contributor.MoveToCampaignAsync("activity-1", "campaign-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityDialerCampaignMoveResult.InFlight, result);
        queueService.Verify(service => service.DequeueAsync(It.IsAny<QueueItem>(), It.IsAny<QueueItemStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MoveToCampaignAsync_WhenTheRecordWaitsInARealQueue_DoesNotMoveIt()
    {
        // Arrange
        var waiting = new QueueItem
        {
            ItemId = "item-1",
            QueueId = "support-queue",
            ActivityItemId = "activity-1",
        }.RestorePersistedStatus(QueueItemStatus.Waiting);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(waiting);

        var contributor = CreateMovingContributor(Mock.Of<IActivityQueueService>(), queueItemManager.Object, lockAcquired: true);

        // Act
        var result = await contributor.MoveToCampaignAsync("activity-1", "campaign-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityDialerCampaignMoveResult.NotQueued, result);
    }

    private static ContactCenterActivityDialerContributor CreateMovingContributor(
        IActivityQueueService queueService,
        IQueueItemManager queueItemManager,
        bool lockAcquired)
    {
        var locker = new Mock<global::OrchardCore.Locking.ILocker>();
        locker.Setup(value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var distributedLock = new Mock<global::OrchardCore.Locking.Distributed.IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((locker.Object, lockAcquired));

        return new ContactCenterActivityDialerContributor(
            Mock.Of<IDialerProfileManager>(),
            queueService,
            queueItemManager,
            Mock.Of<global::YesSql.ISession>(),
            distributedLock.Object,
            global::Microsoft.Extensions.Options.Options.Create(new ContactCenterCoordinationOptions()));
    }
}
