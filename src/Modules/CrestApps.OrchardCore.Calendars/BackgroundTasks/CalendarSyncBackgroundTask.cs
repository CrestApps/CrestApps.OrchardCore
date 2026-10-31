using CrestApps.OrchardCore.Calendars.Core.External;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Calendars.BackgroundTasks;

/// <summary>
/// Synchronizes subscribed calendars with their external calendars when they are due.
/// </summary>
[BackgroundTask(
    Title = "External calendar synchronization",
    Schedule = "*/5 * * * *",
    Description = "Keeps subscribed calendars in step with their external calendars.",
    LockTimeout = 1_000,
    LockExpiration = 600_000)]
public sealed class CalendarSyncBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return serviceProvider.GetRequiredService<CalendarSyncService>().SyncDueAsync(cancellationToken);
    }
}
