using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Core.TaxForms;
using CrestApps.OrchardCore.Payroll.Services;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Creates the Payroll feature's tables, adds the pay codes payroll needs and makes the key tax ids are hashed with.
/// </summary>
internal sealed class PayrollMigrations : DataMigration
{
    private readonly PayrollPayCodeSeeder _seeder;
    private readonly PayRunApprovalPolicySeeder _policySeeder;
    private readonly PayrollTaxIdHasher _taxIdHasher;
    private readonly IStore _store;

    public PayrollMigrations(
        PayrollPayCodeSeeder seeder,
        PayRunApprovalPolicySeeder policySeeder,
        PayrollTaxIdHasher taxIdHasher,
        IStore store)
    {
        _seeder = seeder;
        _policySeeder = policySeeder;
        _taxIdHasher = taxIdHasher;
        _store = store;
    }

    public async Task<int> CreateAsync()
    {
        await PayrollIndexSchema.CreateAsync(SchemaBuilder);
        await PayrollFilingIndexSchema.CreateAsync(SchemaBuilder);
        await PayrollTaxFormIndexSchema.CreateAsync(SchemaBuilder);
        await _seeder.SeedAsync();
        await _policySeeder.SeedAsync();
        await _taxIdHasher.EnsureKeyAsync();

        return 12;
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

    // The pay run approval policy serves pay runs only, so a request of another type that names no policy never
    // lands on it (policies saved before approval types served every type).
    public async Task<int> UpdateFrom8Async()
    {
        await _policySeeder.ServePayRunsOnlyAsync();

        return 9;
    }

    // The seeded policy's stage is titled after its rule, "Payroll approver", while it still has the seeded rule.
    public async Task<int> UpdateFrom9Async()
    {
        await _policySeeder.TitleSeededStageAsync();

        return 10;
    }

    // The index tables get module-specific names (Payroll_PayRunIndex becomes Payroll_PayrollPayRunIndex), so an
    // application module with an index of the same generic name in a Payroll collection of its own no longer shares
    // them. The rows are copied and the old tables dropped. The key tax ids are hashed with is made here too, in the
    // migration's own unit of work, so a recipe or an import that hashes a tax id finds it.
    public async Task<int> UpdateFrom10Async()
    {
        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_EmployerIndex", typeof(PayrollEmployerIndex), PayrollIndexSchema.CreateEmployerTableAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_EmployerIndex");
        }

        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_PayProfileIndex", typeof(PayrollPayProfileIndex), PayrollIndexSchema.CreateProfileTablesAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_PayProfileIndex");
        }

        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_PayItemIndex", typeof(PayrollPayItemIndex), PayrollIndexSchema.CreateProfileTablesAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_PayItemIndex");
        }

        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_PayStatementIndex", typeof(PayrollPayStatementIndex), PayStatementIndexSchema.CreateAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_PayStatementIndex");
        }

        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_PayStatementLineIndex", typeof(PayrollPayStatementLineIndex), PayStatementIndexSchema.CreateAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_PayStatementLineIndex");
        }

        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_PayRunIndex", typeof(PayrollPayRunIndex), PayRunIndexSchema.CreateAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_PayRunIndex");
        }

        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_PayRunInputIndex", typeof(PayrollPayRunInputIndex), PayRunIndexSchema.CreateAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_PayRunInputIndex");
        }

        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_TaxDepositIndex", typeof(PayrollTaxDepositIndex), PayrollFilingIndexSchema.CreateTaxDepositTableAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_TaxDepositIndex");
        }

        await _taxIdHasher.EnsureKeyAsync();

        return 11;
    }

    // Tax forms are records now, made on the Tax Forms page or by the country recipes, instead of classes.
    public async Task<int> UpdateFrom11Async()
    {
        await PayrollTaxFormIndexSchema.CreateAsync(SchemaBuilder);

        return 12;
    }
}
