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
            Mock.Of<IQueueItemManager>());

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
            Mock.Of<IQueueItemManager>());

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
            Mock.Of<IQueueItemManager>());
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
            Mock.Of<IQueueItemManager>());
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
            queueItemManager.Object);

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
            queueItemManager.Object);

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
}
