using System.Data.Common;
using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Tests.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Requires a tenant created today and a tenant upgraded from the first released schema to end up with the same
/// activity index shape.
/// </summary>
/// <remarks>
/// Two tenants running the same code against differently shaped tables is a defect that no functional test can
/// reach. SQLite reconciles an integer written into a text column through column affinity, so an upgraded tenant
/// answers every query correctly there and the divergence stays invisible until the same tenant runs on an
/// engine that does not coerce, where the write fails outright. The shape itself is therefore the evidence.
/// </remarks>
public sealed class OmnichannelActivitySchemaConvergenceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ActivityIndex_FreshTenantAndTenantUpgradedFromAnyReachableSchema_HaveTheSameShape(int startingVersion)
    {
        var freshPath = DatabasePath("omnichannel-activity-fresh");
        var upgradedPath = DatabasePath("omnichannel-activity-upgraded");
        var freshStore = await CreateStoreAsync(freshPath);
        var upgradedStore = await CreateStoreAsync(upgradedPath);

        try
        {
            string freshSchema;
            string upgradedSchema;
            int freshVersion;
            int upgradedVersion;

            await using (var session = freshStore.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(freshStore.Configuration, transaction);
                var migration = new OmnichannelActivityIndexMigrations(freshStore)
                {
                    SchemaBuilder = schemaBuilder,
                };

                freshVersion = await migration.CreateAsync();
                freshSchema = await SqliteSchemaSnapshot.CaptureAsync(
                    schemaBuilder.Connection,
                    transaction,
                    GetIndexTableName(freshStore));
            }

            await using (var session = upgradedStore.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(upgradedStore.Configuration, transaction);
                await CreateHistoricalActivityIndexAsync(schemaBuilder, startingVersion);
                var migration = new OmnichannelActivityIndexMigrations(upgradedStore)
                {
                    SchemaBuilder = schemaBuilder,
                };

                upgradedVersion = await MigrationChainRunner.RunUpgradeChainAsync(migration, startingVersion);
                upgradedSchema = await SqliteSchemaSnapshot.CaptureAsync(
                    schemaBuilder.Connection,
                    transaction,
                    GetIndexTableName(upgradedStore));
            }

            Assert.Equal(freshVersion, upgradedVersion);
            AssertSameShape(freshSchema, upgradedSchema);
        }
        finally
        {
            freshStore.Dispose();
            upgradedStore.Dispose();
            File.Delete(freshPath);
            File.Delete(upgradedPath);
        }
    }

    [Fact]
    public async Task ActivityIndex_UpgradingATenantWithHistory_KeepsEveryEnumValueItAlreadyHeld()
    {
        var databasePath = DatabasePath("omnichannel-activity-values");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateFirstReleasedActivityIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName(store);

            // A released tenant holds the integer the index property wrote through a text column. A row seeded by
            // hand can hold the member's name instead, so both shapes are carried into the upgrade.
            await InsertLegacyActivityAsync(schemaBuilder, tableName, 1, "activity-1", ((int)ActivityStatus.Completed).ToString(), ((int)ActivityUrgencyLevel.High).ToString());
            await InsertLegacyActivityAsync(schemaBuilder, tableName, 2, "activity-2", nameof(ActivityStatus.Cancelled), nameof(ActivityUrgencyLevel.Low));
            await InsertLegacyActivityAsync(schemaBuilder, tableName, 3, "activity-3", "not-a-status", "not-an-urgency");

            var migration = new OmnichannelActivityIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };

            await MigrationChainRunner.RunUpgradeChainAsync(migration, 1);

            Assert.Equal((long)ActivityStatus.Completed, await ReadLongAsync(schemaBuilder, tableName, "Status", "activity-1"));
            Assert.Equal((long)ActivityUrgencyLevel.High, await ReadLongAsync(schemaBuilder, tableName, "UrgencyLevel", "activity-1"));
            Assert.Equal((long)ActivityStatus.Cancelled, await ReadLongAsync(schemaBuilder, tableName, "Status", "activity-2"));
            Assert.Equal((long)ActivityUrgencyLevel.Low, await ReadLongAsync(schemaBuilder, tableName, "UrgencyLevel", "activity-2"));

            // A value that names no member is recorded as unknown rather than rewritten to the first member,
            // because a real member written over an unreadable value hides the problem instead of reporting it.
            Assert.Null(await ReadLongAsync(schemaBuilder, tableName, "Status", "activity-3"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task InsertLegacyActivityAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        long documentId,
        string itemId,
        string status,
        string urgencyLevel)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText =
            $"""
            INSERT INTO "{tableName}" ("DocumentId", "ItemId", "Status", "UrgencyLevel", "Attempts", "ScheduledUtc", "CreatedUtc")
            VALUES (@DocumentId, @ItemId, @Status, @UrgencyLevel, 0, @ScheduledUtc, @CreatedUtc)
            """;

        AddParameter(command, "@DocumentId", documentId);
        AddParameter(command, "@ItemId", itemId);
        AddParameter(command, "@Status", status);
        AddParameter(command, "@UrgencyLevel", urgencyLevel);
        AddParameter(command, "@ScheduledUtc", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        AddParameter(command, "@CreatedUtc", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long?> ReadLongAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        string columnName,
        string itemId)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"""SELECT "{columnName}" FROM "{tableName}" WHERE "ItemId" = @ItemId""";
        AddParameter(command, "@ItemId", itemId);

        var value = await command.ExecuteScalarAsync();

        return value is null || value is DBNull
            ? null
            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.db");

    /// <summary>
    /// Fails with every differing line rather than the first differing character, because a schema divergence is
    /// usually several columns wide and repairing one at a time hides how much has drifted.
    /// </summary>
    private static void AssertSameShape(string freshSchema, string upgradedSchema)
    {
        if (string.Equals(freshSchema, upgradedSchema, StringComparison.Ordinal))
        {
            return;
        }

        var fresh = freshSchema.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var upgraded = upgradedSchema.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var onlyFresh = fresh.Except(upgraded, StringComparer.Ordinal);
        var onlyUpgraded = upgraded.Except(fresh, StringComparer.Ordinal);

        Assert.Fail(
            $"""
            A freshly created tenant and an upgraded tenant do not have the same activity index shape.

            Only on a fresh tenant:
            {string.Join(Environment.NewLine, onlyFresh)}

            Only on an upgraded tenant:
            {string.Join(Environment.NewLine, onlyUpgraded)}
            """);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, TestContext.Current.CancellationToken);

        return store;
    }

    private static string GetIndexTableName(IStore store)
    {
        return store.Configuration.TablePrefix +
            store.Configuration.TableNameConvention.GetIndexTable(
                typeof(OmnichannelActivityIndex),
                OmnichannelConstants.CollectionName);
    }

    /// <summary>
    /// Recreates the activity index exactly as a historical version declared it, so the upgrade path under test
    /// starts from a shape tenants actually have rather than from today's shape.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder the historical table is created through.</param>
    /// <param name="version">The historical schema version to reproduce.</param>
    private static Task CreateHistoricalActivityIndexAsync(SchemaBuilder schemaBuilder, int version)
    {
        return version switch
        {
            1 => CreateFirstReleasedActivityIndexAsync(schemaBuilder),
            3 => CreateAssignmentEraActivityIndexAsync(schemaBuilder),
            4 => CreateCorrectedEnumActivityIndexAsync(schemaBuilder),
            _ => throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "No historical activity index schema is recorded for this version."),
        };
    }

    /// <summary>
    /// Recreates the activity index as the first released schema declared it: every enum-valued column as text,
    /// and no assignment columns at all.
    /// </summary>
    private static async Task CreateFirstReleasedActivityIndexAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("AIProfileName", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<string>("UrgencyLevel", column => column.WithLength(50))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("InteractionType", column => column.WithLength(50)),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityIndex_DocumentId",
                "DocumentId",
                "Channel",
                "ChannelEndpointId",
                "PreferredDestination",
                "ScheduledUtc"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_DocumentId",
                "DocumentId",
                "AssignedToId",
                "Status",
                "InteractionType",
                "ScheduledUtc"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_BatchLoading",
                "ContactContentType",
                "ContactContentItemId",
                "Status",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);
    }

    /// <summary>
    /// Recreates the activity index as the schema that first declared the enum columns correctly: the same
    /// version number as the text-column schema, but the five enum columns already the integer type.
    /// </summary>
    /// <remarks>
    /// Version four is the one historical version reachable in two different shapes, because the declaration was
    /// corrected without the version being raised. A tenant created from the corrected build needs no rebuild,
    /// and running the text-to-number translation over its integer columns would compare a number against a
    /// string, which SQLite coerces and a strongly typed engine rejects. Starting the chain here is what proves
    /// the rebuild recognizes a column that is already correct and leaves it alone.
    /// </remarks>
    private static async Task CreateCorrectedEnumActivityIndexAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<ActivityKind>("Kind")
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("AIProfileName", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
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
            .Column<ActivityInteractionType>("InteractionType"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityIndex_DocumentId",
                "DocumentId",
                "Channel",
                "ChannelEndpointId",
                "PreferredDestination",
                "ScheduledUtc"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_DocumentId",
                "DocumentId",
                "AssignedToId",
                "Status",
                "AssignmentStatus",
                "InteractionType",
                "ScheduledUtc"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_BatchLoading",
                "ContactContentType",
                "ContactContentItemId",
                "Status",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivity_Assignment",
                "AssignmentStatus",
                "ReservationId",
                "ReservedById",
                "ScheduledUtc",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);
    }

    /// <summary>
    /// Recreates the activity index as the schema that introduced assignment declared it: the assignment columns
    /// are present but still text, and the denormalized username columns still exist.
    /// </summary>
    private static async Task CreateAssignmentEraActivityIndexAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Kind", column => column.WithLength(50))
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("AIProfileName", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<string>("AssignedToUsername", column => column.WithLength(255))
            .Column<DateTime>("AssignedToUtc")
            .Column<string>("AssignmentStatus", column => column.WithLength(50))
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("CreatedByUsername", column => column.WithLength(255))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<string>("UrgencyLevel", column => column.WithLength(50))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("InteractionType", column => column.WithLength(50)),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityIndex_DocumentId",
                "DocumentId",
                "Channel",
                "ChannelEndpointId",
                "PreferredDestination",
                "ScheduledUtc"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_DocumentId",
                "DocumentId",
                "AssignedToId",
                "Status",
                "AssignmentStatus",
                "InteractionType",
                "ScheduledUtc"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_BatchLoading",
                "ContactContentType",
                "ContactContentItemId",
                "Status",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivity_Assignment",
                "AssignmentStatus",
                "ReservationId",
                "ReservedById",
                "ScheduledUtc",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);
    }
}
