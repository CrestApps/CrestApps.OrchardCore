using CrestApps.OrchardCore.Staff.Core.Indexes;
using CrestApps.OrchardCore.Staff.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Staff.Migrations;

/// <summary>
/// Creates the Staff index tables and the Staff role.
/// </summary>
internal sealed class StaffMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await StaffIndexSchema.CreateAsync(SchemaBuilder);

        // The role is added after the migration commits, through the role manager. Migrations that run while a tenant
        // starts run no deferred tasks: then the hourly staff upkeep, or the first staff save, adds it.
        ShellScope.AddDeferredTask(async scope =>
        {
            try
            {
                await scope.ServiceProvider.GetRequiredService<StaffRoles>().EnsureRoleAsync();
            }
            catch (Exception ex)
            {
                scope.ServiceProvider.GetRequiredService<ILogger<StaffMigrations>>().LogError(ex, "The Staff role could not be added. It is added the next time a staff member is saved.");
            }
        });

        return 1;
    }
}
