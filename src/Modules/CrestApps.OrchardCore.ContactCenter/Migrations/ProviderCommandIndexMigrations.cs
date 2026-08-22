using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ProviderCommandIndex"/>, including the unique idempotency key that
/// guarantees one provider command per key per tenant.
/// </summary>
internal sealed class ProviderCommandIndexMigrations : DataMigration
{
    private static readonly string _terminalStatusValues = string.Join(
        ", ",
        ((int)ProviderCommandStatus.Confirmed).ToString(CultureInfo.InvariantCulture),
        ((int)ProviderCommandStatus.Compensated).ToString(CultureInfo.InvariantCulture),
        ((int)ProviderCommandStatus.Failed).ToString(CultureInfo.InvariantCulture));

    private readonly IStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The document store, used to resolve the physical table name.</param>
    /// <param name="clock">The clock used to date the retention backfill.</param>
    public ProviderCommandIndexMigrations(
        IStore store,
        IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Creates the provider command index table and its idempotency, due, and reclaim indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProviderCommandIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CommandId", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(ContactCenterStorage.ProviderNameLength))
            .Column<ProviderCommandStatus>("Status")
            .Column<long>("FenceToken", column => column.NotNull().WithDefault(0L))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime>("LeaseExpiresUtc", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ProviderCommandIndex>(table =>
        {
            table.CreateIndex(
                "IDX_ProviderCommandIndex_Due",
                "Status",
                "NextAttemptUtc",
                "DocumentId");
            table.CreateIndex(
                "IDX_ProviderCommandIndex_Reclaim",
                "Status",
                "LeaseExpiresUtc",
                "DocumentId");
        },
            collection: ContactCenterStorage.CollectionName
        );

        // The retention column is left to the update step so a fresh installation reaches the current schema
        // through the same step an existing deployment takes, rather than through a second declaration that
        // could drift from it.
        return 1;
    }

    /// <summary>
    /// Adds the completion time settled commands are purged by. Neither the retry time nor the lease time can
    /// serve, because neither advances once a command has finished.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ProviderCommandIndex>(table => table
            .AddColumn<DateTime?>("CompletedUtc"),
            collection: ContactCenterStorage.CollectionName);

        // Adding a column does not re-project rows that already exist, and a settled command is never written
        // again, so the pre-upgrade backlog would otherwise keep a null completion time and stay forever.
        // Legacy settled rows are dated from the upgrade, which is later than the truth and so never purges early.
        await ContactCenterMigrationSql.AddRetentionColumnAsync(
            SchemaBuilder,
            _store,
            typeof(ProviderCommandIndex),
            "CompletedUtc",
            _clock.UtcNow,
            $"{SchemaBuilder.Dialect.QuoteForColumnName("Status")} IN ({_terminalStatusValues})");

        await SchemaBuilder.AlterIndexTableAsync<ProviderCommandIndex>(table => table
            .CreateIndex(
                "IDX_ProviderCommandIndex_Retention",
                "Status",
                "CompletedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 2;
    }
}
