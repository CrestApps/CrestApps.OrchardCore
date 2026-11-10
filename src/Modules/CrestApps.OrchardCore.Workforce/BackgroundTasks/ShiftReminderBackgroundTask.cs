using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Workforce.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Workforce.BackgroundTasks;

/// <summary>
/// Reminds workers of their shifts as long before they start as each chose.
/// </summary>
[BackgroundTask(
    Title = "Shift reminders",
    Schedule = "*/5 * * * *",
    Description = "Reminds each worker of a shift as long before it starts as they chose in My Work, once per shift.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class ShiftReminderBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<ShiftReminderService>().SendDueAsync(cancellationToken), "sent"));
    }
}
