using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterEventMetricDeltaIndex"/>.
/// </summary>
internal sealed class ContactCenterEventMetricDeltaIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the event metric contribution index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContactCenterEventMetricDeltaIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DateKey", column => column.NotNull().WithLength(10))
            .Column<DateTime>("Date")
            .Column<string>("EventType", column => column.NotNull().WithLength(128))
            .Column<long>("Count")
            .Column<DateTime>("CreatedUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        // No index is created for the roller. It drains the table in whatever order the store hands rows back,
        // which the document identity index YesSql already maintains answers as a covering read. An index
        // ordering the drain by day and type would be worse on both sides: the document query groups by
        // document identity, so the engine cannot prove that ordering from the join and sorts the whole backlog
        // into a temporary tree before returning one batch, and every appended contribution would pay to
        // maintain an index on the path this design exists to keep free of work.
        //
        // Retention ages a contribution from when it was appended, so that purge is an index seek rather than a
        // scan of a table that grows with traffic.
        await SchemaBuilder.AlterIndexTableAsync<ContactCenterEventMetricDeltaIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterEventMetricDeltaIndex_Retention",
                "CreatedUtc",
                "DocumentId"),
            collection: ContactCenterConstants.CollectionName);

        // A reader has to add the contributions that have not been folded yet to the totals it reports, and it
        // asks for them by day. That read is on the request path, so it is given a range-seekable index rather
        // than being left to scan the table. Retention ages by append time and cannot answer a query by day.
        await SchemaBuilder.AlterIndexTableAsync<ContactCenterEventMetricDeltaIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterEventMetricDeltaIndex_Summary",
                "Date",
                "DocumentId"),
            collection: ContactCenterConstants.CollectionName);

        return 1;
    }
}
