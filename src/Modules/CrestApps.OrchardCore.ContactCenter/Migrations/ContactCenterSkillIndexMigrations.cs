using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterSkillIndex"/>.
/// </summary>
internal sealed class ContactCenterSkillIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the skill index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContactCenterSkillIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<bool>("Enabled"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterSkillIndex>(table => table
            .CreateIndex("IDX_ContactCenterSkillIndex_DocumentId", "DocumentId", "ItemId", "Enabled"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterSkillIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterSkillIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 2;
    }

    /// <summary>
    /// Adds the index a record is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned the table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<ContactCenterSkillIndex>(
            SchemaBuilder,
            "IDX_ContactCenterSkillIndex_ItemId",
            ContactCenterStorage.CollectionName,
            "ItemId", "DocumentId");

        return 2;
    }
}
