using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="VoiceMediaItemIndex"/>.
/// </summary>
internal sealed class VoiceMediaItemIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the voice media library index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<VoiceMediaItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<VoiceMediaItemIndex>(table => table
            .CreateIndex("IDX_VoiceMediaItemIndex_DocumentId", "DocumentId", "ItemId", "Name"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }
}
