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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (!serviceProvider.GetRequiredService<ShellSettings>().IsParentTenant())
        {
            return;
        }

        var logger = serviceProvider.GetRequiredService<ILogger<ChildTenantMaintenanceBackgroundTask>>();

        try
        {
            var clock = serviceProvider.GetRequiredService<IClock>();
            var codes = serviceProvider.GetRequiredService<DelegatedAccessCodeStore>();
            await codes.DeleteExpiredAsync(clock.UtcNow.AddMinutes(-5));

            var removed = await serviceProvider.GetRequiredService<ChildTenantManager>().RemoveDueAsync();

            if (removed > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Removed {Count} child tenants whose removal grace period ended.", removed);
            }
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            logger.LogError(ex, "The tenant hierarchy maintenance failed.");
        }
    }
}
