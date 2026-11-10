using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.BackgroundTasks;

/// <summary>
/// Deletes expired one-time codes and removes the child tenants whose removal grace period ended.
/// </summary>
[BackgroundTask(
    Title = "Tenant Hierarchy Maintenance",
    Schedule = "*/5 * * * *",
    Description = "Deletes expired delegated access codes and removes child tenants whose grace period ended.",
    LockTimeout = 5_000,
    LockExpiration = 900_000)]
public sealed class ChildTenantMaintenanceBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            if (!serviceProvider.GetRequiredService<ShellSettings>().IsParentTenant())
            {
                run.Skip();

                return;
            }

            var logger = serviceProvider.GetRequiredService<ILogger<ChildTenantMaintenanceBackgroundTask>>();

            try
            {
                // Expired delegated sign-in codes are removed by the platform retention task (DelegatedAccessCodeRetentionPolicy).
                var removed = await serviceProvider.GetRequiredService<ChildTenantManager>().RemoveDueAsync();

                run.AddItems(removed, "removed");

                if (removed > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Removed {Count} child tenants whose removal grace period ended.", removed);
                }
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                run.ReportError(ex);
                logger.LogError(ex, "The tenant hierarchy maintenance failed.");
            }
        });
    }
}
