using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Users;
using CrestApps.OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// Manage Activities' "Change dialer profile" action.
/// </summary>
/// <remarks>
/// It used to stop at the label: the activities took the dialer's source, but a record loaded by hand was never queued
/// for the dialer and a queued one kept its old profile, so nothing dialed them under the new profile and they could
/// not be found from any dialer screen.
/// </remarks>
public sealed class BulkChangeDialerProfileTests
{
    private const string CampaignId = "campaign-1";
    private const string PowerProfileId = "power-profile";
    private const string OtherCampaignId = "campaign-2";

    [Fact]
    public async Task BulkChangeDialerProfileAsync_WhenTheRecordWasLoadedByHand_QueuesItOnItsCampaignUnderTheNewProfile()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", CampaignId);

        // Act
        var processed = await harness.Controller.BulkChangeDialerProfileAsync([activity], PowerProfileId);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(PowerProfileId, activity.DialerProfileId);
        Assert.Equal(ActivitySources.PowerDial, activity.Source);
        harness.Dialer.Verify(
            dialer => dialer.EnqueueAsync(
                "activity-1",
                CampaignId,
                It.Is<ActivityDialerProfileDescriptor>(profile => profile.ProfileId == PowerProfileId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // The dialer offers each record to whichever agent signed in to the campaign is free, so an assignee routes nothing.
    // Keeping one also left the record in that user's own list, where it could be called by hand while the dialer
    // offered it to someone else.
    [Fact]
    public async Task BulkChangeDialerProfileAsync_WhenTheRecordIsAssigned_ClearsTheAssignee()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", CampaignId);
        activity.AssignedToId = "omar";
        activity.AssignedToUsername = "Omar";
        activity.AssignmentStatus = ActivityAssignmentStatus.Assigned;

        // Act
        await harness.Controller.BulkChangeDialerProfileAsync([activity], PowerProfileId);

        // Assert
        Assert.Null(activity.AssignedToId);
        Assert.Null(activity.AssignedToUsername);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
    }

    [Fact]
    public async Task BulkChangeDialerProfileAsync_WhenTheRecordHasNoCampaign_LeavesItUnchangedAndSaysWhy()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", campaignId: null);

        // Act
        var processed = await harness.Controller.BulkChangeDialerProfileAsync([activity], PowerProfileId);

        // Assert
        Assert.Equal(0, processed);
        Assert.Null(activity.DialerProfileId);
        Assert.Equal(ActivitySources.Manual, activity.Source);
        harness.AssertNotQueued();
        Assert.NotEmpty(harness.Notifier.Invocations);
    }

    // A campaign's waiting records are worked under the profile of the one at the head of its queue, so records moved
    // to a second profile behind another profile's records would never be dialed.
    [Fact]
    public async Task BulkChangeDialerProfileAsync_WhenTheCampaignHasOtherRecordsWaitingUnderAnotherProfile_LeavesTheRecordsUnchanged()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", CampaignId);
        harness.WaitingRecords.Add(new ActivityDialerWaitingRecord { ActivityId = "other-activity", ProfileId = "preview-profile" });

        // Act
        var processed = await harness.Controller.BulkChangeDialerProfileAsync([activity], PowerProfileId);

        // Assert
        Assert.Equal(0, processed);
        Assert.Null(activity.DialerProfileId);
        harness.AssertNotQueued();
        Assert.NotEmpty(harness.Notifier.Invocations);
    }

    // Moving every waiting record of the campaign to the new profile leaves it on one profile, so it is allowed.
    [Fact]
    public async Task BulkChangeDialerProfileAsync_WhenTheOnlyOtherProfileRecordsAreTheOnesBeingMoved_MovesThem()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", CampaignId);
        harness.WaitingRecords.Add(new ActivityDialerWaitingRecord { ActivityId = "activity-1", ProfileId = "preview-profile" });

        // Act
        var processed = await harness.Controller.BulkChangeDialerProfileAsync([activity], PowerProfileId);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(PowerProfileId, activity.DialerProfileId);
        harness.Dialer.Verify(
            dialer => dialer.EnqueueAsync("activity-1", CampaignId, It.IsAny<ActivityDialerProfileDescriptor>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Picking a campaign with the dialer profile moves the records to it, so records that never had a campaign can be
    // queued in one step instead of being refused.
    [Fact]
    public async Task BulkChangeDialerProfileAsync_WhenACampaignIsPicked_MovesTheRecordsToItAndQueuesThemThere()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", campaignId: null);

        // Act
        var processed = await harness.Controller.BulkChangeDialerProfileAsync([activity], PowerProfileId, OtherCampaignId);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(OtherCampaignId, activity.CampaignId);
        harness.Dialer.Verify(
            dialer => dialer.EnqueueAsync("activity-1", OtherCampaignId, It.IsAny<ActivityDialerProfileDescriptor>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // A record the dialer took while it was being moved is on a call: it stays where it is rather than being pulled away.
    [Fact]
    public async Task BulkChangeDialerProfileAsync_WhenTheDialerTakesTheRecordDuringTheMove_LeavesItAlone()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", CampaignId);
        harness.Dialer
            .Setup(dialer => dialer.MoveToCampaignAsync("activity-1", OtherCampaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActivityDialerCampaignMoveResult.InFlight);

        // Act
        var processed = await harness.Controller.BulkChangeDialerProfileAsync([activity], PowerProfileId, OtherCampaignId);

        // Assert
        Assert.Equal(0, processed);
        Assert.Equal(CampaignId, activity.CampaignId);
        harness.AssertNotQueued();
    }

    [Fact]
    public async Task BulkChangeCampaignAsync_WhenTheRecordWaitsForTheDialer_MovesItsPlaceInTheDialerToo()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", CampaignId);
        activity.DialerProfileId = PowerProfileId;
        activity.Source = ActivitySources.PowerDial;

        // Act
        var processed = await harness.Controller.BulkChangeCampaignAsync([activity], OtherCampaignId);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(OtherCampaignId, activity.CampaignId);
        harness.Dialer.Verify(
            dialer => dialer.MoveToCampaignAsync("activity-1", OtherCampaignId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BulkChangeCampaignAsync_WhenTheRecordIsNotDialerWork_OnlyChangesItsCampaign()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", CampaignId);

        // Act
        var processed = await harness.Controller.BulkChangeCampaignAsync([activity], targetCampaignId: null);

        // Assert
        Assert.Equal(1, processed);
        Assert.Null(activity.CampaignId);
        harness.Dialer.Verify(
            dialer => dialer.MoveToCampaignAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // The dialer queues work on its campaign, so dialer work cannot be moved to no campaign at all.
    [Fact]
    public async Task BulkChangeCampaignAsync_WhenDialerWorkIsMovedToNoCampaign_LeavesItUnchanged()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", CampaignId);
        activity.DialerProfileId = PowerProfileId;

        // Act
        var processed = await harness.Controller.BulkChangeCampaignAsync([activity], targetCampaignId: null);

        // Assert
        Assert.Equal(0, processed);
        Assert.Equal(CampaignId, activity.CampaignId);
    }

    // A campaign's waiting records must all use one dialer profile, and records arriving under several would break it.
    [Fact]
    public async Task BulkChangeCampaignAsync_WhenTheRecordsUseDifferentDialerProfiles_LeavesThemUnchanged()
    {
        // Arrange
        var harness = new Harness();
        var power = harness.AddActivity("activity-1", CampaignId);
        power.DialerProfileId = PowerProfileId;
        var preview = harness.AddActivity("activity-2", CampaignId);
        preview.DialerProfileId = "preview-profile";

        // Act
        var processed = await harness.Controller.BulkChangeCampaignAsync([power, preview], OtherCampaignId);

        // Assert
        Assert.Equal(0, processed);
        Assert.Equal(CampaignId, power.CampaignId);
        Assert.Equal(CampaignId, preview.CampaignId);
    }

    [Fact]
    public async Task BulkChangeCampaignAsync_WhenTheTargetCampaignHasRecordsWaitingUnderAnotherProfile_LeavesTheRecordsUnchanged()
    {
        // Arrange
        var harness = new Harness();
        var activity = harness.AddActivity("activity-1", CampaignId);
        activity.DialerProfileId = PowerProfileId;
        harness.WaitingRecords.Add(new ActivityDialerWaitingRecord { ActivityId = "waiting-elsewhere", ProfileId = "preview-profile" });
        harness.WaitingRecordCampaigns["waiting-elsewhere"] = OtherCampaignId;

        // Act
        var processed = await harness.Controller.BulkChangeCampaignAsync([activity], OtherCampaignId);

        // Assert
        Assert.Equal(0, processed);
        Assert.Equal(CampaignId, activity.CampaignId);
    }

    private sealed class Harness
    {
        private readonly Mock<IOmnichannelActivityManager> _activityManager = new();

        public Harness()
        {
            _activityManager
                .Setup(manager => manager.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            Dialer
                .Setup(dialer => dialer.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string profileId, CancellationToken _) => new ActivityDialerProfileDescriptor
                {
                    ProfileId = profileId,
                    DisplayName = profileId,
                    ActivitySource = profileId == PowerProfileId ? ActivitySources.PowerDial : ActivitySources.PreviewDial,
                });
            Dialer
                .Setup(dialer => dialer.GetWaitingRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string campaignId, CancellationToken _) => WaitingRecords.Where(record => WaitingRecordCampaigns.GetValueOrDefault(record.ActivityId, CampaignId) == campaignId).ToArray());

            var htmlLocalizer = new Mock<IHtmlLocalizer<ActivitiesController>>();
            htmlLocalizer
                .Setup(localizer => localizer[It.IsAny<string>()])
                .Returns<string>(name => new LocalizedHtmlString(name, name));
            htmlLocalizer
                .Setup(localizer => localizer[It.IsAny<string>(), It.IsAny<object[]>()])
                .Returns<string, object[]>((name, _) => new LocalizedHtmlString(name, name));

            var stringLocalizer = new Mock<IStringLocalizer<ActivitiesController>>();
            stringLocalizer
                .Setup(localizer => localizer[It.IsAny<string>()])
                .Returns<string>(name => new LocalizedString(name, name));

            Controller = new ActivitiesController(
                new Mock<global::YesSql.ISession>().Object,
                new Mock<IUpdateModelAccessor>().Object,
                new Mock<IContentManager>().Object,
                new Mock<IDisplayManager<OmnichannelActivity>>().Object,
                _activityManager.Object,
                new Mock<IAuthorizationService>().Object,
                new Mock<IContentDefinitionManager>().Object,
                new Mock<IContentItemDisplayManager>().Object,
                new Mock<IActivityDispositionService>().Object,
                new Mock<ISubjectFlowSettingsService>().Object,
                new Mock<IClock>().Object,
                new Mock<ILocalClock>().Object,
                Notifier.Object,
                new UserManager<IUser>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null),
                new Mock<IDisplayNameProvider>().Object,
                [Dialer.Object],
                TestContactMethods.Accessor,
                stringLocalizer.Object,
                htmlLocalizer.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };
        }

        public ActivitiesController Controller { get; }

        public Mock<IActivityDialerContributor> Dialer { get; } = new();

        public Mock<INotifier> Notifier { get; } = new();

        public List<ActivityDialerWaitingRecord> WaitingRecords { get; } = [];

        // The campaign each waiting record waits in, when it is not the default campaign.
        public Dictionary<string, string> WaitingRecordCampaigns { get; } = new(StringComparer.Ordinal);

        public OmnichannelActivity AddActivity(string activityId, string campaignId)
        {
            var activity = new OmnichannelActivity
            {
                ItemId = activityId,
                CampaignId = campaignId,
                Source = ActivitySources.Manual,
                Status = ActivityStatus.NotStated,
            };

            _activityManager
                .Setup(manager => manager.FindByIdAsync(activityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(activity);

            return activity;
        }

        public void AssertNotQueued()
        {
            Dialer.Verify(
                dialer => dialer.EnqueueAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ActivityDialerProfileDescriptor>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
