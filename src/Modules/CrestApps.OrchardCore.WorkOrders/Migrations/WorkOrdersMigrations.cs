using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Legacy;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.WorkOrders.Migrations;

/// <summary>
/// Creates the work order index tables.
/// </summary>
internal sealed class WorkOrdersMigrations : DataMigration
{
    private readonly WorkOrderServiceLevelUpgrade _upgrade;

    public WorkOrdersMigrations(WorkOrderServiceLevelUpgrade upgrade)
    {
        _upgrade = upgrade;
    }

    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await WorkOrdersIndexSchema.CreateAsync(SchemaBuilder);

        return 3;
    }

    /// <summary>
    /// Adds the CRM account column, which replaced the Companies module's company.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<WorkOrderIndex>(table =>
        {
            table.AddColumn<string>("AccountId", column => column.Nullable().WithLength(64));
            table.CreateIndex("IDX_WorkOrderIndex_Account", "AccountId");
        }, collection: WorkOrdersConstants.CollectionName);

        return 2;
    }

    /// <summary>
    /// Moves the policies and clocks Work Orders kept to the shared Service Levels block, which this feature depends on,
    /// so its tables exist by now.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await _upgrade.RunAsync();

        return 3;
    }
}
