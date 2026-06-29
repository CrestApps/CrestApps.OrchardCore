using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
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
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterSkillIndex>(table => table
            .CreateIndex("IDX_ContactCenterSkillIndex_DocumentId", "DocumentId", "ItemId", "Enabled"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
