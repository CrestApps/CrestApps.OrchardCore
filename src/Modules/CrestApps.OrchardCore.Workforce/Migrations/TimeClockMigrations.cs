using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

/// <summary>
/// Creates the time clock's tables, the usual pay codes, and a Time Tracker stage for every activity code.
/// </summary>
internal sealed class TimeClockMigrations : DataMigration
{
    private readonly PayCodeSeeder _payCodeSeeder;
    private readonly TimeClockStageSeeder _stageSeeder;

    public TimeClockMigrations(PayCodeSeeder payCodeSeeder, TimeClockStageSeeder stageSeeder)
    {
        _payCodeSeeder = payCodeSeeder;
        _stageSeeder = stageSeeder;
    }

    public async Task<int> CreateAsync()
    {
        await TimeClockIndexSchema.CreateAsync(SchemaBuilder);
        await _payCodeSeeder.SeedAsync();
        await _stageSeeder.SeedAsync();

        return 2;
    }

    // The time clock now keeps people's time in Time Tracker; the backfill task brings earlier punches in.
    public async Task<int> UpdateFrom1Async()
    {
        await _stageSeeder.SeedAsync();

        return 2;
    }
}
