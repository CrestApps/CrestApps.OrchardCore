using CrestApps.Core;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// A dialer inventory load saved without a campaign, for a subject with no default campaign, loaded activities that
/// could never be queued: dialer work waits on its campaign's queue, which is what agents sign in to. The editor now
/// refuses that load. These pin when the campaign error is raised and when it is not.
/// </summary>
public sealed class OmnichannelActivityBatchDisplayDriverTests
{
    private const string SubjectContentType = "LeadFollowUp";
    private const string ContactContentType = "Lead";
    private const string DialerProfileId = "dialer-profile-1";
    private const string CampaignId = "campaign-1";
    private const string CampaignError = "A campaign is required for dialer activity loads because the selected subject has no default campaign.";

    // The bug: a dialer load with no campaign, for a subject with no default campaign, saved cleanly.
    [Fact]
    public async Task UpdateAsync_DialerLoadWithoutCampaign_WhenSubjectHasNoDefaultCampaign_AddsCampaignError()
    {
        // Arrange
        var driver = CreateDriver(subjectDefaultCampaignId: null);
        var context = PostedFormUpdateModel.CreateContext(NewDialerModel(campaignId: null), isNew: true);

        // Act
        await driver.UpdateAsync(new OmnichannelActivityBatch(), context);

        // Assert
        Assert.Contains(CampaignError, CampaignErrors(context));
    }

    [Fact]
    public async Task UpdateAsync_DialerLoadWithCampaign_AddsNoCampaignError()
    {
        // Arrange
        var driver = CreateDriver(subjectDefaultCampaignId: null);
        var context = PostedFormUpdateModel.CreateContext(NewDialerModel(campaignId: CampaignId), isNew: true);

        // Act
        await driver.UpdateAsync(new OmnichannelActivityBatch(), context);

        // Assert
        Assert.Empty(CampaignErrors(context));
        Assert.True(context.Updater.ModelState.IsValid);
    }

    // The subject's default campaign is where the loader falls back to, so a load that leaves the campaign blank is
    // still valid when the subject has one.
    [Fact]
    public async Task UpdateAsync_DialerLoadWithoutCampaign_WhenSubjectHasDefaultCampaign_AddsNoCampaignError()
    {
        // Arrange
        var driver = CreateDriver(subjectDefaultCampaignId: CampaignId);
        var context = PostedFormUpdateModel.CreateContext(NewDialerModel(campaignId: null), isNew: true);

        // Act
        await driver.UpdateAsync(new OmnichannelActivityBatch(), context);

        // Assert
        Assert.Empty(CampaignErrors(context));
        Assert.True(context.Updater.ModelState.IsValid);
    }

    // A campaign's waiting records are worked under the profile of the one at the head of its queue, so records loaded
    // under a second profile behind another profile's records were never dialed. The editor refuses such a load.
    [Fact]
    public async Task UpdateAsync_DialerLoad_WhenCampaignHasRecordsWaitingUnderAnotherProfile_AddsProfileError()
    {
        // Arrange
        var driver = CreateDriver(
            subjectDefaultCampaignId: null,
            new ActivityDialerWaitingRecord { ActivityId = "waiting-1", ProfileId = "preview-profile" });
        var context = PostedFormUpdateModel.CreateContext(NewDialerModel(campaignId: CampaignId), isNew: true);

        // Act
        await driver.UpdateAsync(new OmnichannelActivityBatch(), context);

        // Assert
        Assert.Contains(DialerProfileErrors(context), error => error.Contains("one dialer profile", StringComparison.Ordinal));
    }

    // The campaign a blank load falls back to is checked the same way.
    [Fact]
    public async Task UpdateAsync_DialerLoadWithoutCampaign_WhenSubjectCampaignHasRecordsWaitingUnderAnotherProfile_AddsProfileError()
    {
        // Arrange
        var driver = CreateDriver(
            subjectDefaultCampaignId: CampaignId,
            new ActivityDialerWaitingRecord { ActivityId = "waiting-1", ProfileId = "preview-profile" });
        var context = PostedFormUpdateModel.CreateContext(NewDialerModel(campaignId: null), isNew: true);

        // Act
        await driver.UpdateAsync(new OmnichannelActivityBatch(), context);

        // Assert
        Assert.NotEmpty(DialerProfileErrors(context));
    }

    [Fact]
    public async Task UpdateAsync_DialerLoad_WhenCampaignHasRecordsWaitingUnderTheSameProfile_AddsNoProfileError()
    {
        // Arrange
        var driver = CreateDriver(
            subjectDefaultCampaignId: null,
            new ActivityDialerWaitingRecord { ActivityId = "waiting-1", ProfileId = DialerProfileId });
        var context = PostedFormUpdateModel.CreateContext(NewDialerModel(campaignId: CampaignId), isNew: true);

        // Act
        await driver.UpdateAsync(new OmnichannelActivityBatch(), context);

        // Assert
        Assert.Empty(DialerProfileErrors(context));
        Assert.True(context.Updater.ModelState.IsValid);
    }

    private static IEnumerable<string> DialerProfileErrors(UpdateEditorContext context)
        => context.Updater.ModelState
            .Where(entry => entry.Key.EndsWith(nameof(OmnichannelActivityBatchViewModel.DialerProfileId), StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Errors)
            .Select(error => error.ErrorMessage);

    private static IEnumerable<string> CampaignErrors(UpdateEditorContext context)
        => context.Updater.ModelState
            .Where(entry => entry.Key.EndsWith(nameof(OmnichannelActivityBatchViewModel.CampaignId), StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Errors)
            .Select(error => error.ErrorMessage);

    private static OmnichannelActivityBatchViewModel NewDialerModel(string campaignId)
        => new()
        {
            DisplayText = "Renewals dialer",
            Source = ActivitySources.Dialer,
            SubjectContentType = SubjectContentType,
            ContactContentType = ContactContentType,
            DialerProfileId = DialerProfileId,
            CampaignId = campaignId,
            ScheduleAt = new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc),
        };

    private static ActivityChannelOptions CreateChannelOptions()
    {
        var options = new ActivityChannelOptions();
        options.AddChannel(OmnichannelConstants.Channels.Phone);
        options.AddChannel(OmnichannelConstants.Channels.Sms);

        return options;
    }

    private static OmnichannelActivityBatchDisplayDriver CreateDriver(string subjectDefaultCampaignId, params ActivityDialerWaitingRecord[] waitingRecords)
    {
        var sourceOptions = new ActivityBatchSourceOptions();
        sourceOptions.AddSource(ActivitySources.Dialer, entry => entry.RequiresUserAssignment = false);

        var subjectFlowSettingsService = new Mock<ISubjectFlowSettingsService>();
        subjectFlowSettingsService
            .Setup(x => x.FindConfiguredFlowSettingsAsync(SubjectContentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectFlowSettings
            {
                SubjectContentType = SubjectContentType,
                CampaignId = subjectDefaultCampaignId,
            });

        var campaignCatalog = new Mock<ICatalog<OmnichannelCampaign>>();
        campaignCatalog
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(
                id == CampaignId ? new OmnichannelCampaign { ItemId = CampaignId, DisplayText = "Renewals" } : null));

        var optionsProvider = new BulkActivityAdminFormOptionsProvider(
            Mock.Of<ICatalogManager<OmnichannelCampaign>>(),
            [new StubDialerContributor(waitingRecords)],
            Options.Create(new ActivitySourceOptions()),
            Options.Create(new ActivityChannelOptions()),
            new PassThroughStringLocalizer<BulkActivityAdminFormOptionsProvider>());

        return new OmnichannelActivityBatchDisplayDriver(
            Mock.Of<IDisplayNameProvider>(),
            Mock.Of<IContentDefinitionManager>(),
            new OmnichannelContentTypeProvider(Mock.Of<IServiceScopeFactory>()),
            Mock.Of<ITimeZoneSelectListProvider>(),
            Mock.Of<ILocalClock>(),
            Mock.Of<ISession>(),
            Mock.Of<INamedCatalog<OmnichannelDisposition>>(),
            campaignCatalog.Object,
            Mock.Of<ICatalog<Cadence>>(),
            Mock.Of<ICatalog<OmnichannelChannelEndpoint>>(),
            subjectFlowSettingsService.Object,
            optionsProvider,
            Options.Create(sourceOptions),
            Options.Create(CreateChannelOptions()),
            Array.Empty<IAIProfileManager>(),
            Mock.Of<IBusinessHoursGate>(),
            new PassThroughStringLocalizer<OmnichannelActivityBatchDisplayDriver>());
    }

    /// <summary>
    /// Knows the one dialer profile the tests post, so the dialer-profile check passes and only the campaign rule is
    /// under test.
    /// </summary>
    private sealed class StubDialerContributor : IActivityDialerContributor
    {
        private readonly IReadOnlyCollection<ActivityDialerWaitingRecord> _waitingRecords;

        public StubDialerContributor(IReadOnlyCollection<ActivityDialerWaitingRecord> waitingRecords)
        {
            _waitingRecords = waitingRecords;
        }

        public Task<IEnumerable<ActivityDialerProfileDescriptor>> GetProfilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ActivityDialerProfileDescriptor>>([]);

        public Task<ActivityDialerProfileDescriptor> FindByIdAsync(string profileId, CancellationToken cancellationToken = default)
            => Task.FromResult(profileId == DialerProfileId
                ? new ActivityDialerProfileDescriptor
                {
                    ProfileId = profileId,
                    DisplayName = "Dialer",
                    ActivitySource = ActivitySources.Dialer,
                }
                : null);

        public Task<IReadOnlyCollection<ActivityDialerWaitingRecord>> GetWaitingRecordsAsync(string campaignId, CancellationToken cancellationToken = default)
            => Task.FromResult(campaignId == CampaignId ? _waitingRecords : []);

        public Task EnqueueAsync(
            string activityId,
            string campaignId,
            ActivityDialerProfileDescriptor profile,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
