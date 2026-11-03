using CrestApps.OrchardCore.DataConnectors.Indexes;
using CrestApps.OrchardCore.DataConnectors.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.DataConnectors.Migrations;

/// <summary>
/// Creates the indexes of connections, data sets and refreshes, and the table that holds the rows of imported data
/// sets.
/// </summary>
internal sealed class DataConnectorsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<DataConnectionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<string>("Connector", column => column.WithLength(128)),
            collection: DataConnectorConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<ConnectedDataSetIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<string>("ConnectionId", column => column.WithLength(26))
            .Column<DataSetAccessMode>("AccessMode")
            .Column<bool>("IsEnabled")
            .Column<DateTime>("NextRefreshUtc", column => column.Nullable())
            .Column<DateTime>("RefreshRequestedUtc", column => column.Nullable()),
            collection: DataConnectorConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<DataSetRefreshRunIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DataSetId", column => column.WithLength(26))
            .Column<DataSetRefreshStatus>("Status")
            .Column<DateTime>("StartedUtc"),
            collection: DataConnectorConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<DataConnectionIndex>(table => table
            .CreateIndex("IDX_DataConnectionIndex_ItemId", "ItemId"),
            collection: DataConnectorConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ConnectedDataSetIndex>(table =>
        {
            table.CreateIndex("IDX_ConnectedDataSetIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ConnectedDataSetIndex_Connection", "ConnectionId");
            table.CreateIndex("IDX_ConnectedDataSetIndex_Due", "IsEnabled", "AccessMode", "NextRefreshUtc");
        }, collection: DataConnectorConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DataSetRefreshRunIndex>(table =>
        {
            table.CreateIndex("IDX_DataSetRefreshRunIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_DataSetRefreshRunIndex_DataSet", "DataSetId", "StartedUtc");
        }, collection: DataConnectorConstants.CollectionName);

        // The rows of imported data sets: one row per record and version, the values as JSON keyed by field name.
        await SchemaBuilder.CreateTableAsync(DataConnectorConstants.RowsTableName, table => table
            .Column<long>("Id", column => column.PrimaryKey().Identity())
            .Column<string>("DataSetId", column => column.NotNull().WithLength(26))
            .Column<string>("Version", column => column.NotNull().WithLength(26))
            .Column<string>("RowKey", column => column.Nullable().WithLength(64))
            .Column<string>("Data", column => column.Nullable().Unlimited())
        );

        await SchemaBuilder.AlterTableAsync(DataConnectorConstants.RowsTableName, table =>
        {
            table.CreateIndex("IDX_DataConnectorsRows_Version", "DataSetId", "Version", "Id");
            table.CreateIndex("IDX_DataConnectorsRows_Key", "DataSetId", "Version", "RowKey");
        });

        return 1;
    }
}
