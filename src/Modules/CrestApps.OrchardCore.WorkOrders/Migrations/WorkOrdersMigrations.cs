using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.WorkOrders.Migrations;

/// <summary>
/// Creates the work order index tables.
/// </summary>
internal sealed class WorkOrdersMigrations : DataMigration
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await WorkOrdersIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
