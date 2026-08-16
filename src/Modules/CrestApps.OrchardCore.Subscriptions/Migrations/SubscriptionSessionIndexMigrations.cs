using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Creates the YesSql map index table and database indexes for subscription sessions.
/// </summary>
public sealed class SubscriptionSessionIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the subscription session index table and its lookup indexes.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SubscriptionSessionIndex>(table => table
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<string>("Status")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("ModifiedUtc")
            .Column<DateTime>("CompletedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionSessionIndex>(table => table
            .CreateIndex("IDX_SubscriptionSessionIndex_DocumentId",
                "DocumentId",
                "SessionId",
                "Status",
                "ModifiedUtc",
                "ContentItemVersionId",
                "ContentItemId")
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionSessionIndex>(table => table
            .CreateIndex("IDX_SubscriptionSessionIndex_OwnerId",
                "DocumentId",
                "OwnerId",
                "Status",
                "ModifiedUtc")
        );

        return 1;
    }
}
