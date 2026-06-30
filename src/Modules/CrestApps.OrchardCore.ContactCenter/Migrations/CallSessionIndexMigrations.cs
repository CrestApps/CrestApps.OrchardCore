using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallSessionIndex"/>.
/// </summary>
internal sealed class CallSessionIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the call session index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CallSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("ProviderCallId", column => column.WithLength(128))
            .Column<string>("State", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex("IDX_CallSessionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "ProviderCallId",
                "InteractionId",
                "State"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex("IDX_CallSessionIndex_Lookup",
                "ActivityItemId",
                "AgentId",
                "QueueId"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
