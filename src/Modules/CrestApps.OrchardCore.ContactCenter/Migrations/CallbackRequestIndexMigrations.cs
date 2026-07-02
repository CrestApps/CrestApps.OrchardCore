using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallbackRequestIndex"/>.
/// </summary>
internal sealed class CallbackRequestIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the callback request index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CallbackRequestIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("ScheduledUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .CreateIndex("IDX_CallbackRequestIndex_DocumentId", "DocumentId", "ItemId", "Status", "ScheduledUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
