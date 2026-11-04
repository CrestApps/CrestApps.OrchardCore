using CrestApps.OrchardCore.Workforce.Core.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

/// <summary>
/// Gives every time-off type a Time Tracker stage the first time Time Off runs with Time Tracker.
/// </summary>
internal sealed class TimeOffTimeTrackerMigrations : DataMigration
{
    private readonly TimeOffTypeStageSeeder _seeder;

    public TimeOffTimeTrackerMigrations(TimeOffTypeStageSeeder seeder)
    {
        _seeder = seeder;
    }

    public async Task<int> CreateAsync()
    {
        await _seeder.SeedAsync();

        return 1;
    }
}
