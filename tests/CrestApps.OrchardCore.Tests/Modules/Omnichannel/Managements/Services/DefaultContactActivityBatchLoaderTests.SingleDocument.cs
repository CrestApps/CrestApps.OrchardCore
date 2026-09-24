using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Services;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// A dialer batch was stored as two batch documents with the same id. Queueing each loaded activity for the dialer
/// commits the unit of work, and a committed YesSql session no longer tracks what it loaded before, so the batch
/// saved after the first contact was inserted as a second document: one copy stayed "Loading" with a single
/// activity counted, the other said "Loaded" with the real count. These load a dialer batch through the real batch
/// catalog, with a dialer that commits the way the Contact Center one does, and read back what was stored.
/// </summary>
public sealed partial class DefaultContactActivityBatchLoaderTests
{
    private const string DialerProfileId = "dialer-profile-1";

    [Fact]
    public async Task LoadAsync_WhenTheDialerCommitsEachQueuedContact_StoresTheBatchOnce()
    {
        // Arrange
        var databasePath = DatabasePath("dialer-commit");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await CreateBatchTableAsync(store);

            var batch = await SeedDialerBatchAsync(store, contactCount: 3);

            // Act
            await using (var session = store.CreateSession())
            {
                var catalog = CreateRealBatchCatalog(session);
                var loader = CreateDialerLoader(session, store, connectionString, catalog);
                var tracked = await catalog.FindByIdAsync(batch.ItemId, TestContext.Current.CancellationToken);

                await loader.LoadAsync(
                    new ActivityBatchLoadContext(tracked, LoaderId, LoaderUserName),
                    TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            var stored = Assert.Single(await ReadBatchesAsync(store, batch.ItemId));

            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, stored.Status);
            Assert.Equal(3L, stored.TotalLoaded.GetValueOrDefault());
            Assert.Equal(3, (await ListLoadedActivitiesAsync(store)).Count);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task CoordinatorLoadAsync_WhenTheLoaderFailsAfterACommit_ReturnsTheBatchToNewWithoutASecondCopy()
    {
        // Arrange
        var databasePath = DatabasePath("coordinator-commit");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await CreateBatchTableAsync(store);

            var batch = NewBatch(FailingLoader.FailingSource, OmnichannelConstants.Channels.Phone);
            batch.Status = OmnichannelActivityBatchStatus.Started;

            await using (var seedSession = store.CreateSession())
            {
                await seedSession.SaveAsync(batch, collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using (var session = store.CreateSession())
            {
                var catalog = CreateRealBatchCatalog(session);
                var coordinator = new DefaultActivityBatchLoadCoordinator(
                    catalog,
                    [new FailingLoader(session)],
                    CreateDialerLoader(session, store, connectionString, catalog),
                    NullLogger<DefaultActivityBatchLoadCoordinator>.Instance);

                await coordinator.LoadAsync(batch.ItemId, LoaderId, LoaderUserName, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            var stored = Assert.Single(await ReadBatchesAsync(store, batch.ItemId));

            Assert.Equal(OmnichannelActivityBatchStatus.New, stored.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static DocumentCatalog<OmnichannelActivityBatch, OmnichannelActivityBatchIndex> CreateRealBatchCatalog(ISession session)
        => new(session, OmnichannelConstants.CollectionName);

    private static DefaultContactActivityBatchLoader CreateDialerLoader(
        ISession session,
        IStore store,
        string connectionString,
        ICatalog<OmnichannelActivityBatch> catalog)
    {
        var sourceOptions = new ActivityBatchSourceOptions();
        sourceOptions.AddSource(ActivitySources.Dialer, entry => entry.RequiresUserAssignment = false);

        return new DefaultContactActivityBatchLoader(
            catalog,
            session,
            new UtcLocalClock(),
            new StubClock(_now),
            new StubSubjectFlowSettingsService(new SubjectFlowSettings
            {
                SubjectContentType = SubjectContentType,
                Channel = OmnichannelConstants.Channels.Phone,
                ChannelEndpointId = "flow-endpoint",
            }),
            CreateActivityManager(),
            store,
            new SqliteDbConnectionAccessor(connectionString),
            [new CommittingDialerContributor(session)],
            Options.Create(sourceOptions),
            new ContactOptOutResolver(session),
            NullLogger<DefaultContactActivityBatchLoader>.Instance);
    }

    private static async Task<OmnichannelActivityBatch> SeedDialerBatchAsync(IStore store, int contactCount)
    {
        var batch = NewBatch(ActivitySources.Dialer, OmnichannelConstants.Channels.Phone);
        batch.DialerProfileId = DialerProfileId;
        batch.CampaignId = "campaign-1";

        await using var seedSession = store.CreateSession();

        for (var i = 0; i < contactCount; i++)
        {
            await SaveContactAsync(seedSession, cellPhoneNumber: $"+1555555090{i}", cellNationalNumber: $"555555090{i}");
        }

        await seedSession.SaveAsync(batch, collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
        await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);

        return batch;
    }

    private static async Task CreateBatchTableAsync(IStore store)
    {
        store.RegisterIndexes([new OmnichannelActivityBatchIndexProvider()]);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<string>("Source", column => column.WithLength(50))
            .Column<OmnichannelActivityBatchStatus>("Status")
            .Column<DateTime>("CreatedUtc"),
            collection: OmnichannelConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<OmnichannelActivityBatch>> ReadBatchesAsync(IStore store, string itemId)
    {
        await using var session = store.CreateSession();

        return [.. await session.Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(
            index => index.ItemId == itemId,
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken)];
    }

    /// <summary>
    /// Queues each activity the way the Contact Center dialer does: its queue service commits the unit of work.
    /// </summary>
    private sealed class CommittingDialerContributor : IActivityDialerContributor
    {
        private readonly ISession _session;

        public CommittingDialerContributor(ISession session)
        {
            _session = session;
        }

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
            => _session.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A loader that commits part of its work and then fails, as a load can when the database refuses a write.
    /// </summary>
    private sealed class FailingLoader : IActivityBatchLoader
    {
        public const string FailingSource = "Failing";

        private readonly ISession _session;

        public FailingLoader(ISession session)
        {
            _session = session;
        }

        public string Source => FailingSource;

        public async Task LoadAsync(ActivityBatchLoadContext context, CancellationToken cancellationToken = default)
        {
            await _session.SaveChangesAsync(cancellationToken);

            throw new InvalidOperationException("The load failed.");
        }
    }
}
