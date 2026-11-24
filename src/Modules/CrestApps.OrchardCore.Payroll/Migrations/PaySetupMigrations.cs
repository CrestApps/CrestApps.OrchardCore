using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Payroll.Migrations;

internal sealed class PaySetupMigrations : DataMigration
{
    private readonly PayCodeSeeder _seeder;
    private readonly IStore _store;

    public PaySetupMigrations(PayCodeSeeder seeder, IStore store)
    {
        _seeder = seeder;
        _store = store;
    }

    public async Task<int> CreateAsync()
    {
        await PaySetupIndexSchema.CreateAsync(SchemaBuilder);
        await _seeder.SeedAsync();

        return 2;
    }

    // The index tables get module-specific names (Payroll_PayCodeIndex becomes Payroll_PayrollPayCodeIndex), so an
    // application module with an index of the same generic name in a Payroll collection of its own no longer shares them.
    public async Task<int> UpdateFrom1Async()
    {
        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_PayScheduleIndex", typeof(PayrollPayScheduleIndex), PaySetupIndexSchema.CreateAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_PayScheduleIndex");
        }

        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_PayCodeIndex", typeof(PayrollPayCodeIndex), PaySetupIndexSchema.CreateAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_PayCodeIndex");
        }

        return 2;
    }
}
