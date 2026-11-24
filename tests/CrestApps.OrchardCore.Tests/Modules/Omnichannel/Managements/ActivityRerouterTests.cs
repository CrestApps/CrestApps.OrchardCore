using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// A workflow decides who makes the next call: a dialer record moved to an agent leaves the dialer queue and becomes
/// that agent's manual call, and manual work moved to the dialer is queued on its campaign under the profile.
/// </summary>
public sealed class ActivityRerouterTests
{
    private static readonly DateTime _now = new(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RerouteAsync_WhenADialerRecordGoesToAnAgent_TakesItOffTheDialerAndAssignsTheAgent()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            Source = ActivitySources.PowerDial,
            DialerProfileId = "profile-1",
            CampaignId = "campaign-1",
            Channel = OmnichannelConstants.Channels.Phone,
            Status = ActivityStatus.Scheduled,
            ReservationId = "reservation-1",
        };

        var activities = new Mock<IOmnichannelActivityManager>();
        activities.Setup(manager => manager.FindByIdAsync("activity-1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var dialer = new Mock<IActivityDialerContributor>();
        dialer
            .Setup(contributor => contributor.GetActivitiesInFlightAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));
        dialer
            .Setup(contributor => contributor.WithdrawAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActivityDialerWithdrawResult.Withdrawn);

        var rerouter = CreateRerouter(activities.Object, dialer.Object, new User { UserId = "user-1", UserName = "jane" });

        // Act
        var result = await rerouter.RerouteAsync(
            "activity-1",
            new ActivityRerouteRequest { Target = ActivityRerouteTarget.Agent, AssignToUser = "jane" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityRerouteStatus.Rerouted, result.Status);
        Assert.Equal(["activity-1"], result.ActivityItemIds);
        dialer.Verify(contributor => contributor.WithdrawAsync("activity-1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ActivitySources.Manual, activity.Source);
        Assert.Null(activity.DialerProfileId);
        Assert.Null(activity.ReservationId);
        Assert.Equal("campaign-1", activity.CampaignId);
        Assert.Equal("user-1", activity.AssignedToId);
        Assert.Equal("jane", activity.AssignedToUsername);
        Assert.Equal(ActivityAssignmentStatus.Assigned, activity.AssignmentStatus);
        Assert.Equal(ActivityStatus.NotStated, activity.Status);
        activities.Verify(manager => manager.UpdateAsync(activity, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RerouteAsync_WhenTheDialerIsOnTheCall_LeavesTheRecordAlone()
    {
        // Arrange
        var activity = new OmnichannelActivity { ItemId = "activity-1", Source = ActivitySources.PowerDial, Status = ActivityStatus.Scheduled };
        var activities = new Mock<IOmnichannelActivityManager>();
        activities.Setup(manager => manager.FindByIdAsync("activity-1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var dialer = new Mock<IActivityDialerContributor>();
        dialer
            .Setup(contributor => contributor.GetActivitiesInFlightAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["activity-1"], StringComparer.Ordinal));

        var rerouter = CreateRerouter(activities.Object, dialer.Object, user: null);

        // Act
        var result = await rerouter.RerouteAsync(
            "activity-1",
            new ActivityRerouteRequest { Target = ActivityRerouteTarget.Agent },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityRerouteStatus.Failed, result.Status);
        Assert.Equal(ActivitySources.PowerDial, activity.Source);
        dialer.Verify(contributor => contributor.WithdrawAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RerouteAsync_WhenManualWorkGoesToTheDialer_QueuesItOnTheCampaignUnderTheProfile()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            Source = ActivitySources.Manual,
            CampaignId = "campaign-1",
            Channel = OmnichannelConstants.Channels.Phone,
            PreferredDestination = "+17025550100",
            AssignedToId = "user-1",
            AssignedToUsername = "jane",
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
            Status = ActivityStatus.NotStated,
        };

        var activities = new Mock<IOmnichannelActivityManager>();
        activities.Setup(manager => manager.FindByIdAsync("activity-1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var profile = new ActivityDialerProfileDescriptor { ProfileId = "profile-2", DisplayName = "Power", ActivitySource = ActivitySources.PowerDial };
        var dialer = new Mock<IActivityDialerContributor>();
        dialer.Setup(contributor => contributor.FindByIdAsync("profile-2", It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        dialer
            .Setup(contributor => contributor.GetActivitiesInFlightAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));
        dialer
            .Setup(contributor => contributor.GetWaitingRecordsAsync("campaign-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var rerouter = CreateRerouter(activities.Object, dialer.Object, user: null);

        // Act
        var result = await rerouter.RerouteAsync(
            "activity-1",
            new ActivityRerouteRequest { Target = ActivityRerouteTarget.Dialer, DialerProfileId = "profile-2" },
            TestContext.Current.CancellationToken);

        // Assert: the dialer offers it to whichever agent signed in to the campaign is free, so it has no owner.
        Assert.Equal(ActivityRerouteStatus.Rerouted, result.Status);
        Assert.Equal(ActivitySources.PowerDial, activity.Source);
        Assert.Equal("profile-2", activity.DialerProfileId);
        Assert.Null(activity.AssignedToId);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        dialer.Verify(contributor => contributor.EnqueueAsync("activity-1", "campaign-1", profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RerouteAsync_WhenTheDialerProfileIsMissing_Fails()
    {
        var activities = new Mock<IOmnichannelActivityManager>();
        activities
            .Setup(manager => manager.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "activity-1", Status = ActivityStatus.Scheduled });

        var rerouter = CreateRerouter(activities.Object, Mock.Of<IActivityDialerContributor>(), user: null);

        var result = await rerouter.RerouteAsync(
            "activity-1",
            new ActivityRerouteRequest { Target = ActivityRerouteTarget.Dialer, DialerProfileId = "missing" },
            TestContext.Current.CancellationToken);

        Assert.Equal(ActivityRerouteStatus.Failed, result.Status);
    }

    [Fact]
    public void ApplyAgentRoute_WhenNoAgentIsNamed_KeepsTheOwnerTheActivityHas()
    {
        var activity = new OmnichannelActivity
        {
            Source = ActivitySources.PowerDial,
            DialerProfileId = "profile-1",
            AssignedToId = "user-1",
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
        };

        ActivityRerouter.ApplyAgentRoute(activity, ActivitySources.Manual, "campaign-2", ownerId: null, ownerName: null, _now);

        Assert.Equal("user-1", activity.AssignedToId);
        Assert.Equal("campaign-2", activity.CampaignId);
        Assert.Equal(ActivityStatus.NotStated, activity.Status);
    }

    [Fact]
    public void ApplyAgentRoute_WhenNobodyOwnsIt_LeavesItAvailableForAnyAgent()
    {
        var activity = new OmnichannelActivity { Source = ActivitySources.PowerDial };

        ActivityRerouter.ApplyAgentRoute(activity, ActivitySources.Manual, campaignId: null, ownerId: null, ownerName: null, _now);

        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        Assert.Equal(ActivityStatus.Scheduled, activity.Status);
    }

    private static ActivityRerouter CreateRerouter(IOmnichannelActivityManager activities, IActivityDialerContributor dialer, User user)
    {
        var sources = new ActivitySourceOptions();
        sources.AddSource(ActivitySources.Manual, entry => entry.CanBeSetManually = true);

        var users = new Mock<IUserStore<IUser>>();
        users
            .Setup(store => store.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => user is not null && string.Equals(user.UserName, name, StringComparison.OrdinalIgnoreCase) ? user : null);
        users
            .Setup(store => store.GetUserIdAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUser value, CancellationToken _) => ((User)value).UserId);
        users
            .Setup(store => store.GetUserNameAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUser value, CancellationToken _) => ((User)value).UserName);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new ActivityRerouter(
            activities,
            [dialer],
            Mock.Of<global::YesSql.ISession>(),
            Mock.Of<IContentManager>(),
            Mock.Of<IContactMethodsAccessor>(),
            new UserManager<IUser>(users.Object, null, null, null, null, null, null, null, NullLogger<UserManager<IUser>>.Instance),
            Options.Create(sources),
            clock.Object,
            NullLogger<ActivityRerouter>.Instance);
    }
}
