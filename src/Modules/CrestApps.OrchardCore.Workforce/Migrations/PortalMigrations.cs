using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class PortalMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await PortalIndexSchema.CreateAsync(SchemaBuilder);
        await PortalIndexSchema.CreateStaffFeedIndexAsync(SchemaBuilder);

        return 4;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await PortalIndexSchema.CreateShiftReminderLogAsync(SchemaBuilder);

        return 2;
    }

    // Workers became staff members, kept in the Staff collection: their feed secrets are indexed there.
    public async Task<int> UpdateFrom2Async()
    {
        await PortalIndexSchema.CreateStaffFeedIndexAsync(SchemaBuilder);

        return 3;
    }

    // Feed secrets are stored hashed and protected: the workers whose secret is from before get both. The plain secret
    // stays one release, for a rollback. On an upgrade from before Staff this runs on the start that turns Staff on,
    // before the workers move, and finds none: the move upgrades each worker it moves, and showing a worker's feed
    // address upgrades any secret both missed.
    public static int UpdateFrom3Async()
    {
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<WorkerFeedTokenBackfill>().RunAsync());

        return 4;
    }
}
