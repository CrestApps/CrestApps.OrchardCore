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
            Mock.Of<IActivityQueueService>());

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
            Mock.Of<IActivityQueueService>());

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
            queueService.Object);
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
            Mock.Of<IActivityQueueService>());
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
