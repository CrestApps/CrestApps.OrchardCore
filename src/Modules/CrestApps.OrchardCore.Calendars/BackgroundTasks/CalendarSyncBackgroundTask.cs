using CrestApps.OrchardCore.Calendars.Core.External;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Calendars.BackgroundTasks;

/// <summary>
/// Synchronizes subscribed calendars with their external calendars when they are due. Each calendar runs in its own scope, so a
/// provider that fails or a save that conflicts affects only that calendar.
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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var calendarIds = await serviceProvider.GetRequiredService<CalendarSyncService>().ListDueAsync(cancellationToken);
        var logger = serviceProvider.GetRequiredService<ILogger<CalendarSyncBackgroundTask>>();

        foreach (var calendarId in calendarIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ShellScope.UsingChildScopeAsync(scope => scope.ServiceProvider.GetRequiredService<CalendarSyncService>().SyncAsync(calendarId, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Synchronizing the calendar '{CalendarId}' with its external calendar failed.", calendarId);
            }
        }
    }
}
