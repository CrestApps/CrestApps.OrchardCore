using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// A dialer batch with no campaign, for a subject with no default campaign, created every activity and then failed to
/// queue each one, since dialer work waits on its campaign's queue. That left a half-loaded batch of activities no
/// agent could receive. The loader now stops such a batch before it creates anything; the editor refuses it, but
/// older and imported loads can still reach the loader.
/// </summary>
public sealed partial class DefaultContactActivityBatchLoaderTests
{
    [Fact]
    public async Task LoadAsync_DialerBatchWithoutCampaign_WhenSubjectHasNoDefaultCampaign_CreatesNoActivitiesAndEnqueuesNothing()
    {
        // Arrange
        var databasePath = DatabasePath("dialer-no-campaign");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550801", cellNationalNumber: "5555550801");
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550802", cellNationalNumber: "5555550802");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Dialer, OmnichannelConstants.Channels.Phone);
            batch.DialerProfileId = DialerProfileId;
            batch.CampaignId = null;

            var dialer = new RecordingDialerContributor();
            var logger = new RecordingLogger<DefaultContactActivityBatchLoader>();

            // Act
            await using (var session = store.CreateSession())
            {
                var loader = CreateCampaignDialerLoader(session, store, connectionString, dialer, logger, subjectDefaultCampaignId: null);

                await loader.LoadAsync(
                    new ActivityBatchLoadContext(batch, LoaderId, LoaderUserName),
                    TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            Assert.Empty(await ListLoadedActivitiesAsync(store));
            Assert.Empty(dialer.EnqueuedActivityIds);
            Assert.Equal(0L, batch.TotalLoaded.GetValueOrDefault());
            Assert.Equal(OmnichannelActivityBatchStatus.New, batch.Status);
            Assert.Contains(
                logger.At(LogLevel.Warning),
                message => message.Contains(batch.ItemId, StringComparison.Ordinal) &&
                    message.Contains("no campaign", StringComparison.Ordinal));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    // The guard must not over-reach: a batch that leaves the campaign blank still loads when its subject has a default
    // campaign, and each activity is queued on that campaign.
    [Fact]
    public async Task LoadAsync_DialerBatchWithoutCampaign_WhenSubjectHasDefaultCampaign_QueuesOnTheSubjectCampaign()
    {
        // Arrange
        var databasePath = DatabasePath("dialer-subject-campaign");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550811", cellNationalNumber: "5555550811");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Dialer, OmnichannelConstants.Channels.Phone);
            batch.DialerProfileId = DialerProfileId;
            batch.CampaignId = null;

            var dialer = new RecordingDialerContributor();

            // Act
            await using (var session = store.CreateSession())
            {
                var loader = CreateCampaignDialerLoader(
                    session,
                    store,
                    connectionString,
                    dialer,
                    new RecordingLogger<DefaultContactActivityBatchLoader>(),
                    subjectDefaultCampaignId: "subject-campaign");

                await loader.LoadAsync(
                    new ActivityBatchLoadContext(batch, LoaderId, LoaderUserName),
                    TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            var activity = Assert.Single(await ListLoadedActivitiesAsync(store));

            Assert.Equal("subject-campaign", activity.CampaignId);

            // The profile is recorded on the activity so the activity screens can say which dialer will call it.
            Assert.Equal(DialerProfileId, activity.DialerProfileId);
            Assert.Equal([activity.ItemId], dialer.EnqueuedActivityIds);
            Assert.Equal(["subject-campaign"], dialer.EnqueuedCampaignIds);
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, batch.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static DefaultContactActivityBatchLoader CreateCampaignDialerLoader(
        ISession session,
        IStore store,
        string connectionString,
        IActivityDialerContributor dialer,
        ILogger<DefaultContactActivityBatchLoader> logger,
        string subjectDefaultCampaignId)
    {
        var sourceOptions = new ActivityBatchSourceOptions();
        sourceOptions.AddSource(ActivitySources.Dialer, entry => entry.RequiresUserAssignment = false);

        return new DefaultContactActivityBatchLoader(
            CreateBatchCatalog(),
            session,
            new UtcLocalClock(),
            new StubClock(_now),
            new StubSubjectFlowSettingsService(new SubjectFlowSettings
            {
                SubjectContentType = SubjectContentType,
                Channel = OmnichannelConstants.Channels.Phone,
                ChannelEndpointId = "flow-endpoint",
                CampaignId = subjectDefaultCampaignId,
            }),
            CreateActivityManager(),
            store,
            new SqliteDbConnectionAccessor(connectionString),
            [dialer],
            Options.Create(sourceOptions),
            new ContactOptOutResolver(session),
            new NoNotInServiceNumbers(),
            logger);
    }

    /// <summary>
    /// Records what the loader asks the dialer to queue, without committing anything.
    /// </summary>
    private sealed class RecordingDialerContributor : IActivityDialerContributor
    {
        public List<string> EnqueuedActivityIds { get; } = [];

        public List<string> EnqueuedCampaignIds { get; } = [];

        public Task<IEnumerable<ActivityDialerProfileDescriptor>> GetProfilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ActivityDialerProfileDescriptor>>([]);

        public Task<ActivityDialerProfileDescriptor> FindByIdAsync(string profileId, CancellationToken cancellationToken = default)
            => Task.FromResult(new ActivityDialerProfileDescriptor
            {
                ProfileId = profileId,
                DisplayName = "Dialer",
                ActivitySource = ActivitySources.Dialer,
            });

        public Task EnqueueAsync(
            string activityId,
            string campaignId,
            ActivityDialerProfileDescriptor profile,
            CancellationToken cancellationToken = default)
        {
            EnqueuedActivityIds.Add(activityId);
            EnqueuedCampaignIds.Add(campaignId);

            return Task.CompletedTask;
        }
    }
}
