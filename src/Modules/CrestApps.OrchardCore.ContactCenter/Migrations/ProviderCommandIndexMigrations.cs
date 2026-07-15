using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ProviderCommandIndex"/>, including the unique idempotency key that
/// guarantees one provider command per key per tenant.
/// </summary>
internal sealed class ProviderCommandIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the provider command index table and its idempotency, due, and reclaim indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProviderCommandIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CommandId", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<long>("FenceToken", column => column.NotNull().WithDefault(0L))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime>("LeaseExpiresUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName
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
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
