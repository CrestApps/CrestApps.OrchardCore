using System.Data;
using System.Data.Common;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using OrchardCore;
using OrchardCore.Data;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Handlers;

public sealed class TimeZoneListOmnichannelActivityFilterHandlerTests
{
    private const string TimeZoneId = "America/Los_Angeles";
    private const string OtherTimeZoneId = "America/New_York";
    private const string UserId = "agent";

    // SQL Server refuses any command that carries more than 2,100 parameters.
    private const int SqlServerParameterLimit = 2100;

    private static readonly DateTime _now = new(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FilteringAsync_WhenMoreContactsShareTheTimeZoneThanOneCommandMayBind_PagesTheMatchingActivities()
    {
        // Arrange
        // The scheduled-activities list filtered by time zone used to load every published contact in that time
        // zone and bind each one into a ContactContentItemId IN (...) list, so SQL Server refused the page once a
        // tenant had more than 2,100 contacts in one time zone. SQLite accepts far more parameters than that, so
        // the store's connections are held to SQL Server's limit once seeding is done.
        var databasePath = DatabasePath("large");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var connectionFactory = new ParameterLimitedConnectionFactory(connectionString);
        var store = await CreateStoreAsync(connectionString, connectionFactory);
        const int contactCount = SqlServerParameterLimit + 400;
        const int matchingActivityCount = 25;

        try
        {
            var expectedItemIds = new List<string>(matchingActivityCount);

            await using (var seedSession = store.CreateSession())
            {
                var contactIds = new List<string>(contactCount);

                for (var i = 0; i < contactCount; i++)
                {
                    var contactId = IdGenerator.GenerateId();
                    contactIds.Add(contactId);
                    await SaveContactAsync(seedSession, contactId, TimeZoneId, published: true, latest: true);
                }

                // A contact with a published version and a newer draft has a row per version. Its activity must
                // still be listed, and listed once.
                await SaveContactAsync(seedSession, contactIds[0], TimeZoneId, published: false, latest: true);

                for (var i = 0; i < matchingActivityCount; i++)
                {
                    expectedItemIds.Add(await SaveActivityAsync(seedSession, contactIds[i], i));
                }

                var otherTimeZoneContactId = IdGenerator.GenerateId();
                await SaveContactAsync(seedSession, otherTimeZoneContactId, OtherTimeZoneId, published: true, latest: true);
                await SaveActivityAsync(seedSession, otherTimeZoneContactId, matchingActivityCount);

                var draftOnlyContactId = IdGenerator.GenerateId();
                await SaveContactAsync(seedSession, draftOnlyContactId, TimeZoneId, published: false, latest: true);
                await SaveActivityAsync(seedSession, draftOnlyContactId, matchingActivityCount + 1);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            connectionFactory.ParameterLimit = SqlServerParameterLimit;

            await using var querySession = store.CreateSession();
            var activityStore = new OmnichannelActivityStore(
                querySession,
                [new TimeZoneListOmnichannelActivityFilterHandler(querySession)],
                [],
                store,
                new SqliteDbConnectionAccessor(connectionString));
            var filter = new ListOmnichannelActivityFilter
            {
                TimeZoneId = TimeZoneId,
            };

            // Act
            var firstPage = await activityStore.PageManualScheduledAsync(UserId, 1, 10, filter, TestContext.Current.CancellationToken);
            var lastPage = await activityStore.PageManualScheduledAsync(UserId, 3, 10, filter, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(matchingActivityCount, firstPage.Count);
            Assert.Equal(10, firstPage.Entries.Count);
            Assert.Equal(matchingActivityCount, lastPage.Count);
            Assert.Equal(5, lastPage.Entries.Count);

            // Ordered newest first, so the first page holds the activities seeded last.
            Assert.Equal(
                expectedItemIds.AsEnumerable().Reverse().Take(10),
                firstPage.Entries.Select(activity => activity.ItemId));
            Assert.Equal(
                expectedItemIds.Take(5).Reverse(),
                lastPage.Entries.Select(activity => activity.ItemId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task FilteringAsync_WhenNoContactIsInTheTimeZone_ReturnsNoActivities()
    {
        // Arrange
        var databasePath = DatabasePath("empty");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString, new ParameterLimitedConnectionFactory(connectionString));

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                var contactId = IdGenerator.GenerateId();
                await SaveContactAsync(seedSession, contactId, OtherTimeZoneId, published: true, latest: true);
                await SaveActivityAsync(seedSession, contactId, 0);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var activityStore = new OmnichannelActivityStore(
                querySession,
                [new TimeZoneListOmnichannelActivityFilterHandler(querySession)],
                [],
                store,
                new SqliteDbConnectionAccessor(connectionString));

            // Act
            var page = await activityStore.PageManualScheduledAsync(
                UserId,
                1,
                10,
                new ListOmnichannelActivityFilter { TimeZoneId = TimeZoneId },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, page.Count);
            Assert.Empty(page.Entries);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string connectionString, IConnectionFactory connectionFactory)
    {
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite(connectionString);
            configuration.ConnectionFactory = connectionFactory;
        });

        // Contacts are content items in the default collection, and activities live in their own collection, so
        // the filter has to reach across the two.
        store.RegisterIndexes([new OmnichannelActivityIndexProvider(), new SeededContactIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            OmnichannelConstants.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelContactIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<bool>("IsConverted", column => column.NotNull().WithDefault(false))
            .Column<bool>("Published", column => column.NotNull().WithDefault(false))
            .Column<bool>("Latest", column => column.NotNull().WithDefault(false))
            .Column<string>("PrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryEmailAddress", column => column.WithLength(255))
            .Column<string>("NormalizedPrimaryEmailAddress", column => column.WithLength(255))
            .Column<string>("TimeZoneId", column => column.WithLength(64)));
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<ActivityKind>("Kind")
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("DialerProfileId", column => column.Nullable().WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
            .Column<ActivityAssignmentStatus>("AssignmentStatus")
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<ActivityUrgencyLevel>("UrgencyLevel")
            .Column<ActivityStatus>("Status")
            .Column<ActivityInteractionType>("InteractionType")
            .Column<bool>("AiEscalated")
            .Column<string>("TerminalReasonCode", column => column.Nullable().WithLength(64)),
            collection: OmnichannelConstants.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveContactAsync(ISession session, string contentItemId, string timeZoneId, bool published, bool latest)
    {
        await session.SaveAsync(
            new SeededContact
            {
                ContentItemId = contentItemId,
                TimeZoneId = timeZoneId,
                Published = published,
                Latest = latest,
            },
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task<string> SaveActivityAsync(ISession session, string contactContentItemId, int order)
    {
        var itemId = IdGenerator.GenerateId();

        await session.SaveAsync(
            new OmnichannelActivity
            {
                ItemId = itemId,
                Channel = "Phone",
                ChannelEndpointId = "endpoint",
                ContactContentItemId = contactContentItemId,
                ContactContentType = "Lead",
                SubjectContentType = "LeadFollowUp",
                PreferredDestination = "+15555550100",
                AssignedToId = UserId,
                ScheduledUtc = _now.AddMinutes(order),
                CreatedUtc = _now,
                InteractionType = ActivityInteractionType.Manual,
                Status = ActivityStatus.NotStated,
            },
            collection: OmnichannelConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);

        return itemId;
    }

    private static string DatabasePath(string suffix)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            $"omnichannel-time-zone-filter-{suffix}-{Guid.NewGuid():N}.db");
    }

    /// <summary>
    /// Stands in for a contact content item version, so the contact index can be seeded without the content
    /// management stack.
    /// </summary>
    private sealed class SeededContact
    {
        public string ContentItemId { get; set; }

        public string TimeZoneId { get; set; }

        public bool Published { get; set; }

        public bool Latest { get; set; }
    }

    private sealed class SeededContactIndexProvider : IndexProvider<SeededContact>
    {
        public override void Describe(DescribeContext<SeededContact> context)
        {
            context
                .For<OmnichannelContactIndex>()
                .Map(contact => new OmnichannelContactIndex
                {
                    ContentItemId = contact.ContentItemId,
                    ContentType = "Lead",
                    TimeZoneId = contact.TimeZoneId,
                    Published = contact.Published,
                    Latest = contact.Latest,
                });
        }
    }

    private sealed class SqliteDbConnectionAccessor : IDbConnectionAccessor
    {
        private readonly string _connectionString;

        public SqliteDbConnectionAccessor(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }
    }

    /// <summary>
    /// Opens SQLite connections that refuse a command binding more than <see cref="ParameterLimit"/> parameters,
    /// the way SQL Server refuses one past 2,100.
    /// </summary>
    private sealed class ParameterLimitedConnectionFactory : IConnectionFactory
    {
        private readonly string _connectionString;

        public ParameterLimitedConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public int? ParameterLimit { get; set; }

        public Type DbConnectionType => typeof(SqliteConnection);

        public DbConnection CreateConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.StateChange += OnStateChange;

            return connection;
        }

        private void OnStateChange(object sender, StateChangeEventArgs e)
        {
            if (e.CurrentState != ConnectionState.Open || sender is not SqliteConnection connection || ParameterLimit is not int limit)
            {
                return;
            }

            SQLitePCL.raw.sqlite3_limit(connection.Handle, SQLitePCL.raw.SQLITE_LIMIT_VARIABLE_NUMBER, limit);
        }
    }
}
