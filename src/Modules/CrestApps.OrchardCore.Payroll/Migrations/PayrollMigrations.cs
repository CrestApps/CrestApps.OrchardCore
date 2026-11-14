using CrestApps.OrchardCore.Payroll.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Creates the Payroll feature's tables.
/// </summary>
internal sealed class PayrollMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await PayrollIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
