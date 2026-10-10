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
                .Setup(dialer => dialer.GetWaitingRecordsAsync(CampaignId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => WaitingRecords);

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
                new Mock<IDisplayManager<OmnichannelActivityContainer>>().Object,
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
