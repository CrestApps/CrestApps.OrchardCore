using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the index tables of the interval statistics: the computed days, and the queue and agent days.
/// </summary>
internal sealed class IntervalStatisticsIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the index tables and the indexes the reader and the roller find a day by.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<IntervalStatisticsDayIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<DateTime>("Date", column => column.NotNull())
            .Column<int>("Version", column => column.NotNull().WithDefault(0))
            .Column<bool>("Final", column => column.NotNull().WithDefault(false))
            .Column<int>("InteractionCount", column => column.NotNull().WithDefault(0))
            .Column<DateTime>("ComputedUtc", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<IntervalStatisticsDayIndex>(table => table
            .CreateIndex("IDX_IntervalStatisticsDayIndex_Date", "Date", "Version", "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.CreateMapIndexTableAsync<QueueIntervalStatisticsIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<DateTime>("Date", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<QueueIntervalStatisticsIndex>(table => table
            .CreateIndex("IDX_QueueIntervalStatisticsIndex_Date", "Date", "QueueId", "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<QueueIntervalStatisticsIndex>(table => table
            .CreateIndex("IDX_QueueIntervalStatisticsIndex_Queue", "QueueId", "Date", "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.CreateMapIndexTableAsync<AgentIntervalStatisticsIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<DateTime>("Date", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentIntervalStatisticsIndex>(table => table
            .CreateIndex("IDX_AgentIntervalStatisticsIndex_Date", "Date", "UserId", "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentIntervalStatisticsIndex>(table => table
            .CreateIndex("IDX_AgentIntervalStatisticsIndex_User", "UserId", "Date", "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 1;
    }
}
