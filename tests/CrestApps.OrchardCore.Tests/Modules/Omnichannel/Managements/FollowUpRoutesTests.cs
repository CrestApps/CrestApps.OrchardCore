using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// The follow-up a subject action gives to an agent leaves the dialer, and one it sends to the dialer is a phone call
/// the dialer offers to whichever agent is free.
/// </summary>
public sealed class FollowUpRoutesTests
{
    private static readonly DateTime _now = new(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApplyAgentRoute_TakesTheDialerRecordOffTheDialerAndKeepsItsOwner()
    {
        var activity = new OmnichannelActivity
        {
            Source = ActivitySources.PowerDial,
            DialerProfileId = "profile-1",
            ReservationId = "reservation-1",
            AssignedToId = "user-1",
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
        };

        FollowUpRoutes.ApplyAgentRoute(activity, ActivitySources.Manual, "campaign-2", ownerId: null, ownerName: null, _now);

        Assert.Equal(ActivitySources.Manual, activity.Source);
        Assert.Null(activity.DialerProfileId);
        Assert.Null(activity.ReservationId);
        Assert.Equal("user-1", activity.AssignedToId);
        Assert.Equal("campaign-2", activity.CampaignId);
        Assert.Equal(ActivityStatus.NotStated, activity.Status);
    }

    [Fact]
    public void ApplyAgentRoute_WhenNobodyOwnsIt_LeavesItAvailableForAnyAgent()
    {
        var activity = new OmnichannelActivity { Source = ActivitySources.PowerDial };

        FollowUpRoutes.ApplyAgentRoute(activity, ActivitySources.Manual, campaignId: null, ownerId: null, ownerName: null, _now);

        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        Assert.Equal(ActivityStatus.Scheduled, activity.Status);
    }

    [Fact]
    public void ApplyDialerRoute_MakesItAnUnownedPhoneCallUnderTheProfile()
    {
        var activity = new OmnichannelActivity
        {
            Source = ActivitySources.Manual,
            AssignedToId = "user-1",
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
        };
        var profile = new ActivityDialerProfileDescriptor { ProfileId = "profile-2", ActivitySource = ActivitySources.PowerDial };

        FollowUpRoutes.ApplyDialerRoute(activity, profile, "campaign-1", "+17025550100");

        Assert.Equal(ActivitySources.PowerDial, activity.Source);
        Assert.Equal("profile-2", activity.DialerProfileId);
        Assert.Equal(OmnichannelConstants.Channels.Phone, activity.Channel);
        Assert.Equal("+17025550100", activity.PreferredDestination);
        Assert.Null(activity.AssignedToId);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
    }

    [Theory]
    [InlineData(ActivitySources.Manual, true)]
    [InlineData(ActivitySources.Callback, true)]
    [InlineData(ActivitySources.Automatic, false)]
    [InlineData(ActivitySources.PowerDial, false)]
    public void IsAgentSource_OffersNeitherTheAIsNorADialersSource(string source, bool expected)
        => Assert.Equal(expected, FollowUpRoutes.IsAgentSource(source));
}
