using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.WorkOrders.Migrations;

/// <summary>
/// Creates the service agreement table.
/// </summary>
internal sealed class ServiceAgreementsMigrations : DataMigration
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await ServiceAgreementsIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
