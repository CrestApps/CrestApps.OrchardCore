using System.Globalization;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ActivityReservationIndex"/>.
/// </summary>
internal sealed class ActivityReservationIndexMigrationsSchemaMigration : ISchemaMigration
{
    // YesSql persists the ReservationStatus enum as its underlying integer, so rows written under the former
    // string column hold that integer as text ("0", "1", ...). These invariant numeric strings match the stored
    // representation on every provider (comparing against the enum names would never match real rows).
    private static readonly string PendingStatusValue =
        ((int)ReservationStatus.Pending).ToString(CultureInfo.InvariantCulture);

    private static readonly string AcceptedStatusValue =
        ((int)ReservationStatus.Accepted).ToString(CultureInfo.InvariantCulture);

    private static readonly string _terminalStatusValues = string.Join(
        ", ",
        ((int)ReservationStatus.Rejected).ToString(CultureInfo.InvariantCulture),
        ((int)ReservationStatus.Expired).ToString(CultureInfo.InvariantCulture),
        ((int)ReservationStatus.Canceled).ToString(CultureInfo.InvariantCulture));

    private readonly IStore _store;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityReservationIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ActivityReservationIndexMigrationsSchemaMigration(
        IStore store,
        TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "ActivityReservationIndexMigrations";

    /// <summary>
    /// Creates the reservation index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<ActivityReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("AgentClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(26))
            .Column<ReservationStatus>("Status")
            .Column<DateTime>("ExpiresUtc", column => column.NotNull())
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<ActivityReservationIndex>(table => table
            .CreateIndex("IDX_ActivityReservationIndex_DocumentId", "DocumentId", "AgentId", "Status", "ExpiresUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        // The claim constraints are created the same way the upgrade path creates them. Declaring them inline
        // instead would leave a freshly installed database with a different shape than an upgraded one, and
        // the two would then diverge again on the next rolling upgrade.
        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            builder,
            _store,
            typeof(ActivityReservationIndex),
            "UQ_ActivityReservationIndex_ActivityClaimKey",
            "ActivityClaimKey");
        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            builder,
            _store,
            typeof(ActivityReservationIndex),
            "UQ_ActivityReservationIndex_AgentClaimKey",
            "AgentClaimKey");

        await builder.AlterIndexTableAsync<ActivityReservationIndex>(table => table
            .CreateIndex("IDX_ActivityReservationIndex_Retention", "Status", "ModifiedUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 3;
    }

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public async Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        switch (version)
        {
            case 1:
                return await UpdateFrom1Async(builder);

            case 2:
                return await UpdateFrom2Async(builder);

            default:
                return version;
        }
    }

    private static async Task EnsureLegacyRowsCanBeConstrainedAsync(ISchemaBuilder builder,
        string quotedTableName,
        string activityItemColumn,
        string agentIdColumn,
        string itemIdColumn,
        string statusColumn)
    {
        var hasMissingIdentifiers = await ContactCenterMigrationSql.ExistsAsync(
            builder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {itemIdColumn} IS NULL OR {itemIdColumn} = ''
               OR {activityItemColumn} IS NULL OR {activityItemColumn} = ''
               OR {agentIdColumn} IS NULL OR {agentIdColumn} = ''
            """);

        if (hasMissingIdentifiers)
        {
            throw new InvalidOperationException(
                "The Contact Center reservation index contains rows without item, activity, or agent identifiers. Repair the legacy rows before enabling unique active reservation claims.");
        }

        var hasDuplicateActivityClaims = await ContactCenterMigrationSql.ExistsAsync(
            builder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {statusColumn} IN (@PendingStatus, @AcceptedStatus)
            GROUP BY {activityItemColumn}
            HAVING COUNT(*) > 1
            """,
            ("@PendingStatus", PendingStatusValue),
            ("@AcceptedStatus", AcceptedStatusValue));

        if (hasDuplicateActivityClaims)
        {
            throw new InvalidOperationException(
                "The Contact Center reservation index contains multiple active reservations for one activity. Resolve the duplicate legacy reservations before enabling unique active reservation claims.");
        }

        var hasDuplicateAgentClaims = await ContactCenterMigrationSql.ExistsAsync(
            builder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {statusColumn} = @PendingStatus
            GROUP BY {agentIdColumn}
            HAVING COUNT(*) > 1
            """,
            ("@PendingStatus", PendingStatusValue));

        if (hasDuplicateAgentClaims)
        {
            throw new InvalidOperationException(
                "The Contact Center reservation index contains multiple pending reservations for one agent. Resolve the duplicate legacy reservations before enabling unique pending-agent claims.");
        }
    }

    /// <summary>
    /// Moves the schema from version 1 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, matching the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method: folding every version into one switch would make
    /// one authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
                    // Adds portable unique active-claim constraints to existing reservation indexes.
                    var tableName = builder.TablePrefix +
                        builder.TableNameConvention.GetIndexTable(
                            typeof(ActivityReservationIndex),
                            ContactCenterStorage.CollectionName);
                    var quotedTableName = builder.Dialect.QuoteForTableName(tableName, _store.Configuration.Schema);
                    var activityClaimColumn = builder.Dialect.QuoteForColumnName("ActivityClaimKey");
                    var activityItemColumn = builder.Dialect.QuoteForColumnName("ActivityItemId");
                    var agentClaimColumn = builder.Dialect.QuoteForColumnName("AgentClaimKey");
                    var agentIdColumn = builder.Dialect.QuoteForColumnName("AgentId");
                    var itemIdColumn = builder.Dialect.QuoteForColumnName("ItemId");
                    var statusColumn = builder.Dialect.QuoteForColumnName("Status");

                    await EnsureLegacyRowsCanBeConstrainedAsync(builder,
                        quotedTableName,
                        activityItemColumn,
                        agentIdColumn,
                        itemIdColumn,
                        statusColumn);

                    await builder.AlterIndexTableAsync<ActivityReservationIndex>(table =>
                    {
                        table.AddColumn<string>(
                            "ActivityClaimKey",
                            column => column.NotNull().WithDefault(string.Empty).WithLength(26));
                        table.AddColumn<string>(
                            "AgentClaimKey",
                            column => column.NotNull().WithDefault(string.Empty).WithLength(26));
                    },
                        collection: ContactCenterStorage.CollectionName);

                    await using (var command = builder.Connection.CreateCommand())
                    {
                        command.Transaction = builder.Transaction;
                        command.CommandText = $"""
                            UPDATE {quotedTableName}
                            SET {activityClaimColumn} = CASE
                                    WHEN {statusColumn} IN (@PendingStatus, @AcceptedStatus) THEN {activityItemColumn}
                                    ELSE {itemIdColumn}
                                END,
                                {agentClaimColumn} = CASE
                                    WHEN {statusColumn} = @PendingStatus THEN {agentIdColumn}
                                    ELSE {itemIdColumn}
                                END
                            """;

                        var pendingStatus = command.CreateParameter();
                        pendingStatus.ParameterName = "@PendingStatus";
                        pendingStatus.Value = PendingStatusValue;
                        command.Parameters.Add(pendingStatus);

                        var acceptedStatus = command.CreateParameter();
                        acceptedStatus.ParameterName = "@AcceptedStatus";
                        acceptedStatus.Value = AcceptedStatusValue;
                        command.Parameters.Add(acceptedStatus);

                        await command.ExecuteNonQueryAsync();
                    }

                    await ContactCenterMigrationSql.CreateUniqueIndexAsync(
                        builder,
                        _store,
                        typeof(ActivityReservationIndex),
                        "UQ_ActivityReservationIndex_ActivityClaimKey",
                        "ActivityClaimKey");
                    await ContactCenterMigrationSql.CreateUniqueIndexAsync(
                        builder,
                        _store,
                        typeof(ActivityReservationIndex),
                        "UQ_ActivityReservationIndex_AgentClaimKey",
                        "AgentClaimKey");

                    return 2;
                }

    /// <summary>
    /// Moves the schema from version 2 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, matching the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method: folding every version into one switch would make
    /// one authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
                    // Adds the time a reservation reached a terminal status, which is the age settled reservations are purged
                    // by. Neither the creation time nor the expiry can serve: an accepted reservation lives for as long as the
                    // work does, and it keeps an expiry in the future that never arrives.
                    await builder.AlterIndexTableAsync<ActivityReservationIndex>(table => table
                        .AddColumn<DateTime>("ModifiedUtc", column => column.Nullable()),
                        collection: ContactCenterStorage.CollectionName);

                    await ContactCenterMigrationSql.AddRetentionColumnAsync(
                        builder,
                        _store,
                        typeof(ActivityReservationIndex),
                        "ModifiedUtc",
                        _timeProvider.GetUtcNow().UtcDateTime,
                        $"{builder.Dialect.QuoteForColumnName("Status")} IN ({_terminalStatusValues})");

                    await builder.AlterIndexTableAsync<ActivityReservationIndex>(table => table
                        .CreateIndex("IDX_ActivityReservationIndex_Retention", "Status", "ModifiedUtc", "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 3;
                }
}
