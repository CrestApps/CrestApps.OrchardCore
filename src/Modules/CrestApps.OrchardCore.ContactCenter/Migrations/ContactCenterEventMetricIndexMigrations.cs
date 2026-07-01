using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterEventMetricIndex"/>.
/// </summary>
internal sealed class ContactCenterEventMetricIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the event metric index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContactCenterEventMetricIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DateKey", column => column.WithLength(10))
            .Column<DateTime>("Date")
            .Column<string>("EventType", column => column.WithLength(128)),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterEventMetricIndex>(table => table
            .CreateIndex("IDX_ContactCenterEventMetricIndex_DocumentId", "DocumentId", "DateKey", "Date", "EventType"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
