using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class WorkforceMigrations : DataMigration
{
    private readonly ActivityCodeSeeder _activityCodeSeeder;
    private readonly IStore _store;
    private readonly ISession _session;

    public WorkforceMigrations(
        ActivityCodeSeeder activityCodeSeeder,
        IStore store,
        ISession session)
    {
        _activityCodeSeeder = activityCodeSeeder;
        _store = store;
        _session = session;
    }

    public async Task<int> CreateAsync()
    {
        await WorkforceIndexSchema.CreateAsync(SchemaBuilder);
        await _activityCodeSeeder.SeedAsync();

        // Teams live in the Teams module from the start, so there is nothing to move.
        return 2;
    }

    // Teams were kept in the Workforce collection before they had their own module.
    public async Task<int> UpdateFrom1Async()
    {
        await WorkforceTeamsMove.MoveAsync(SchemaBuilder, _store, _session);

        return 2;
    }
}
