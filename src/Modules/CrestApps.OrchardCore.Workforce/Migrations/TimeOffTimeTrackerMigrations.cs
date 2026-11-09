using CrestApps.OrchardCore.Workforce.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class TimeOffTimeTrackerMigrations : DataMigration
{
    public static int Create()
    {
        // The seeder reads time off types through their store, whose table another migration of the same activation
        // creates; run it once every migration has committed, so the order Orchard runs them in does not matter.
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<TimeOffTypeStageSeeder>().SeedAsync());

        return 1;
    }
}
