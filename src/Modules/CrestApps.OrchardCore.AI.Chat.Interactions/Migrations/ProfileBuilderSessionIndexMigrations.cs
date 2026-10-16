using CrestApps.OrchardCore.AI.Chat.Interactions.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ProfileBuilderSessionIndex"/>.
/// </summary>
internal sealed class ProfileBuilderSessionIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the builder session index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProfileBuilderSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ChatInteractionId", column => column.WithLength(26))
            .Column<string>("OwnerId", column => column.WithLength(255))
            .Column<DateTime>("ModifiedUtc"),
            collection: ProfileBuilderStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ProfileBuilderSessionIndex>(table => table
            .CreateIndex("IDX_ProfileBuilderSessionIndex_ItemId", "ItemId", "DocumentId"),
            collection: ProfileBuilderStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ProfileBuilderSessionIndex>(table => table
            .CreateIndex("IDX_ProfileBuilderSessionIndex_ChatInteractionId", "ChatInteractionId", "DocumentId"),
            collection: ProfileBuilderStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ProfileBuilderSessionIndex>(table => table
            .CreateIndex("IDX_ProfileBuilderSessionIndex_OwnerId", "OwnerId", "ModifiedUtc", "DocumentId"),
            collection: ProfileBuilderStorage.CollectionName);

        return 1;
    }
}
