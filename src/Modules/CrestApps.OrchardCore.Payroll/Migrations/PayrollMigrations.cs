using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Creates the Payroll feature's tables and adds the pay codes payroll needs.
/// </summary>
internal sealed class PayrollMigrations : DataMigration
{
    private readonly PayrollPayCodeSeeder _seeder;

    public PayrollMigrations(PayrollPayCodeSeeder seeder)
    {
        _seeder = seeder;
    }

    public async Task<int> CreateAsync()
    {
        await PayrollIndexSchema.CreateAsync(SchemaBuilder);
        await _seeder.SeedAsync();

        return 3;
    }

    // Pay profiles and pay items.
    public async Task<int> UpdateFrom1Async()
    {
        await PayrollIndexSchema.CreateProfileTablesAsync(SchemaBuilder);

        return 2;
    }

    // Salary, bonus, commission, tips, a reimbursement and a loan repayment; codes already there are left alone.
    public async Task<int> UpdateFrom2Async()
    {
        await _seeder.SeedAsync();

        return 3;
    }
}
