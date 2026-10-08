using CrestApps.Core.AI.FileSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.AI.FileSources.BackgroundTasks;

/// <summary>
/// Hourly Orchard background task that runs every enabled file source whose interval has elapsed.
/// </summary>
/// <remarks>
/// The framework ships a hosted service that does this, but a hosted service registered in a tenant's
/// container is never started, so the schedule is driven from here instead.
/// <para>
/// Which sources are due, and running them, is entirely <see cref="IFileSourceScheduler"/>'s -- the same
/// service the framework's own hosted service calls. This class is the Orchard trigger and nothing else, so
/// a rule about what is due cannot drift between the two hosts.
/// </para>
/// <para>
/// The scheduler creates no scope of its own, which is why this hands it the tenant scope Orchard already
/// opened: everything it reads and writes belongs to this tenant.
/// </para>
/// </remarks>
[BackgroundTask(
    Title = "File Source Ingestion",
    Schedule = "0 * * * *",
    Description = "Hourly evaluation of file sources; reads and ingests each source that is due.",
    LockTimeout = 5_000,
    LockExpiration = 600_000)]
public sealed class FileSourceRunBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// Runs the file sources that are due.
    /// </summary>
    /// <param name="serviceProvider">The tenant service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        // Absent when the feature's services are not in this tenant's container, which is not an error.
        var scheduler = serviceProvider.GetService<IFileSourceScheduler>();

        if (scheduler is null)
        {
            return;
        }

        var result = await scheduler.RunDueAsync(cancellationToken);

        if (result.Ran == 0 && result.Failed == 0)
        {
            return;
        }

        var logger = serviceProvider.GetRequiredService<ILogger<FileSourceRunBackgroundTask>>();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Considered {Considered} file source(s): ran {Ran}, failed {Failed}.",
                result.Considered,
                result.Ran,
                result.Failed);
        }
    }
}
