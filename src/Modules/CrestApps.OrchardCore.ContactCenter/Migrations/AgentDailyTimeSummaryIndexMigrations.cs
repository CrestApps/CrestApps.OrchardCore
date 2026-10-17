using CrestApps.OrchardCore.ContactCenter.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentDailyTimeSummaryIndex"/>.
/// </summary>
internal sealed class AgentDailyTimeSummaryIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the daily agent time summary index table and the indexes a report and the roller find a day by.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AgentDailyTimeSummaryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<DateTime>("Date", column => column.NotNull())
            .Column<int>("Version", column => column.NotNull().WithDefault(0))
            .Column<int>("EventCount", column => column.NotNull().WithDefault(0)),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AgentDailyTimeSummaryIndex>(table => table
            .CreateIndex(
                "IDX_AgentDailyTimeSummaryIndex_Date",
                "Date",
                "Version",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentDailyTimeSummaryIndex>(table => table
            .CreateIndex(
                "IDX_AgentDailyTimeSummaryIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 1;
    }
}
