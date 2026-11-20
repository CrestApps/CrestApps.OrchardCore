using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Payroll.Migrations;

internal sealed class PaySetupMigrations : DataMigration
{
    private readonly PayCodeSeeder _seeder;

    public PaySetupMigrations(PayCodeSeeder seeder)
    {
        _seeder = seeder;
    }

    public async Task<int> CreateAsync()
    {
        await PaySetupIndexSchema.CreateAsync(SchemaBuilder);
        await _seeder.SeedAsync();

        return 1;
    }
}
