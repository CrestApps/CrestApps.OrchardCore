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

        return 3;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await PortalIndexSchema.CreateShiftReminderLogAsync(SchemaBuilder);

        return 2;
    }

    // Feed secrets are stored hashed and protected: the workers whose secret is from before get both. The plain secret
    // stays one release, for a rollback.
    public static int UpdateFrom2Async()
    {
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<WorkerFeedTokenBackfill>().RunAsync());

        return 3;
    }
}
