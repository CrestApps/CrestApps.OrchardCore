using CrestApps.OrchardCore.Payroll.Core.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Creates the table of garnishment orders.
/// </summary>
internal sealed class GarnishmentsMigrations : DataMigration
{
    private readonly GarnishmentPayCodeSeeder _seeder;

    public GarnishmentsMigrations(GarnishmentPayCodeSeeder seeder)
    {
        _seeder = seeder;
    }

    public async Task<int> CreateAsync()
    {
        await GarnishmentOrderIndexSchema.CreateAsync(SchemaBuilder);
        await _seeder.SeedAsync();

        return 1;
    }
}
