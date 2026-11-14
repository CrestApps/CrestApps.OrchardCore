using CrestApps.OrchardCore.Payroll.Core.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Creates the table of payroll tax rules.
/// </summary>
internal sealed class PayrollTaxTablesMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await PayrollTaxRuleIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
