using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ActivityReservationIndex"/>.
/// </summary>
internal sealed class ActivityReservationIndexMigrations : DataMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityReservationIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ActivityReservationIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the reservation index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ActivityReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("AgentClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<ReservationStatus>("Status")
            .Column<DateTime>("ExpiresUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ActivityReservationIndex>(table => table
            .CreateIndex("IDX_ActivityReservationIndex_DocumentId", "DocumentId", "AgentId", "Status", "ExpiresUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        return 2;
    }

    /// <summary>
    /// Adds portable unique active-claim constraints to existing reservation indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        var tableName = SchemaBuilder.TablePrefix +
            SchemaBuilder.TableNameConvention.GetIndexTable(
                typeof(ActivityReservationIndex),
                ContactCenterConstants.CollectionName);
        var quotedTableName = SchemaBuilder.Dialect.QuoteForTableName(tableName, _store.Configuration.Schema);
        var activityClaimColumn = SchemaBuilder.Dialect.QuoteForColumnName("ActivityClaimKey");
        var activityItemColumn = SchemaBuilder.Dialect.QuoteForColumnName("ActivityItemId");
        var agentClaimColumn = SchemaBuilder.Dialect.QuoteForColumnName("AgentClaimKey");
        var agentIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("AgentId");
        var itemIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("ItemId");
        var statusColumn = SchemaBuilder.Dialect.QuoteForColumnName("Status");

        await EnsureLegacyRowsCanBeConstrainedAsync(
            quotedTableName,
            activityItemColumn,
            agentIdColumn,
            itemIdColumn,
            statusColumn);

        await SchemaBuilder.AlterIndexTableAsync<ActivityReservationIndex>(table =>
        {
            table.AddColumn<string>(
                "ActivityClaimKey",
                column => column.NotNull().WithDefault(string.Empty).WithLength(26));
            table.AddColumn<string>(
                "AgentClaimKey",
                column => column.NotNull().WithDefault(string.Empty).WithLength(26));
        },
            collection: ContactCenterConstants.CollectionName);

        await using (var command = SchemaBuilder.Connection.CreateCommand())
        {
            command.Transaction = SchemaBuilder.Transaction;
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
            pendingStatus.Value = ReservationStatus.Pending.ToString();
            command.Parameters.Add(pendingStatus);

            var acceptedStatus = command.CreateParameter();
            acceptedStatus.ParameterName = "@AcceptedStatus";
            acceptedStatus.Value = ReservationStatus.Accepted.ToString();
            command.Parameters.Add(acceptedStatus);

            await command.ExecuteNonQueryAsync();
        }

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(ActivityReservationIndex),
            "UQ_ActivityReservationIndex_ActivityClaimKey",
            "ActivityClaimKey");
        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(ActivityReservationIndex),
            "UQ_ActivityReservationIndex_AgentClaimKey",
            "AgentClaimKey");

        return 2;
    }

    private async Task EnsureLegacyRowsCanBeConstrainedAsync(
        string quotedTableName,
        string activityItemColumn,
        string agentIdColumn,
        string itemIdColumn,
        string statusColumn)
    {
        var hasMissingIdentifiers = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
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
            SchemaBuilder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {statusColumn} IN (@PendingStatus, @AcceptedStatus)
            GROUP BY {activityItemColumn}
            HAVING COUNT(*) > 1
            """,
            ("@PendingStatus", ReservationStatus.Pending.ToString()),
            ("@AcceptedStatus", ReservationStatus.Accepted.ToString()));

        if (hasDuplicateActivityClaims)
        {
            throw new InvalidOperationException(
                "The Contact Center reservation index contains multiple active reservations for one activity. Resolve the duplicate legacy reservations before enabling unique active reservation claims.");
        }

        var hasDuplicateAgentClaims = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {statusColumn} = @PendingStatus
            GROUP BY {agentIdColumn}
            HAVING COUNT(*) > 1
            """,
            ("@PendingStatus", ReservationStatus.Pending.ToString()));

        if (hasDuplicateAgentClaims)
        {
            throw new InvalidOperationException(
                "The Contact Center reservation index contains multiple pending reservations for one agent. Resolve the duplicate legacy reservations before enabling unique pending-agent claims.");
        }
    }
}
