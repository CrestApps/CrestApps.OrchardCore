using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class WorkforceMigrations : DataMigration
{
    private readonly ActivityCodeSeeder _activityCodeSeeder;

    public WorkforceMigrations(ActivityCodeSeeder activityCodeSeeder)
    {
        _activityCodeSeeder = activityCodeSeeder;
    }

    public async Task<int> CreateAsync()
    {
        await WorkforceIndexSchema.CreateAsync(SchemaBuilder);
        await _activityCodeSeeder.SeedAsync();

        return 1;
    }
}
