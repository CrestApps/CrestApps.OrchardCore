using CrestApps.OrchardCore.Payroll.Core.Services;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Creates the table of garnishment orders.
/// </summary>
internal sealed class GarnishmentsMigrations : DataMigration
{
    private readonly GarnishmentPayCodeSeeder _seeder;
    private readonly IStore _store;

    public GarnishmentsMigrations(GarnishmentPayCodeSeeder seeder, IStore store)
    {
        _seeder = seeder;
        _store = store;
    }

    public async Task<int> CreateAsync()
    {
        await GarnishmentOrderIndexSchema.CreateAsync(SchemaBuilder);
        await _seeder.SeedAsync();

        return 2;
    }

    // The index table gets a module-specific name (Payroll_GarnishmentOrderIndex becomes
    // Payroll_PayrollGarnishmentOrderIndex), so it never collides with an application module's index of the same name.
    public async Task<int> UpdateFrom1Async()
    {
        if (await PayrollIndexTableMove.MoveAsync(SchemaBuilder, _store, "Payroll_GarnishmentOrderIndex", typeof(PayrollGarnishmentOrderIndex), GarnishmentOrderIndexSchema.CreateAsync))
        {
            await SchemaBuilder.DropTableAsync("Payroll_GarnishmentOrderIndex");
        }

        return 2;
    }
}
