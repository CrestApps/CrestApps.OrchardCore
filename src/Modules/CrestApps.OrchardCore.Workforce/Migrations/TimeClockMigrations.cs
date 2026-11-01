using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

/// <summary>
/// Creates the time clock's tables and the usual pay codes.
/// </summary>
internal sealed class TimeClockMigrations : DataMigration
{
    private readonly PayCodeSeeder _payCodeSeeder;

    public TimeClockMigrations(PayCodeSeeder payCodeSeeder)
    {
        _payCodeSeeder = payCodeSeeder;
    }

    public async Task<int> CreateAsync()
    {
        await TimeClockIndexSchema.CreateAsync(SchemaBuilder);
        await _payCodeSeeder.SeedAsync();

        return 1;
    }
}
