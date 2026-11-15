using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Creates the Payroll feature's tables and adds the pay codes payroll needs.
/// </summary>
internal sealed class PayrollMigrations : DataMigration
{
    private readonly PayrollPayCodeSeeder _seeder;
    private readonly PayRunApprovalPolicySeeder _policySeeder;

    public PayrollMigrations(PayrollPayCodeSeeder seeder, PayRunApprovalPolicySeeder policySeeder)
    {
        _seeder = seeder;
        _policySeeder = policySeeder;
    }

    public async Task<int> CreateAsync()
    {
        await PayrollIndexSchema.CreateAsync(SchemaBuilder);
        await PayrollFilingIndexSchema.CreateAsync(SchemaBuilder);
        await _seeder.SeedAsync();
        await _policySeeder.SeedAsync();

        return 8;
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

    // Pay statements and the line rows the accumulators sum.
    public async Task<int> UpdateFrom3Async()
    {
        await PayStatementIndexSchema.CreateAsync(SchemaBuilder);

        return 4;
    }

    // Pay runs, their inputs, and the approval policy runs are approved by.
    public async Task<int> UpdateFrom4Async()
    {
        await PayRunIndexSchema.CreateAsync(SchemaBuilder);
        await _policySeeder.SeedAsync();

        return 5;
    }

    // Cash tips reported and the tip credit make-up; codes already there are left alone.
    public async Task<int> UpdateFrom5Async()
    {
        await _seeder.SeedAsync();

        return 6;
    }

    // Tax deposits and filings.
    public async Task<int> UpdateFrom6Async()
    {
        await PayrollFilingIndexSchema.CreateAsync(SchemaBuilder);

        return 7;
    }

    // The flat-rate wages of tax lines, so a bonus paid on its own isn't counted as wages the tables withheld on.
    public async Task<int> UpdateFrom7Async()
    {
        await PayStatementIndexSchema.AddFlatWagesAsync(SchemaBuilder);

        return 8;
    }
}
