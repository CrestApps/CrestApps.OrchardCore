using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.DataConnectors.BackgroundTasks;

/// <summary>
/// Refreshes, every minute, the imported data sets whose schedule is due or that someone asked to refresh.
/// </summary>
/// <remarks>
/// The task's lock keeps one instance of the site refreshing at a time, and each refresh also locks its data set, so a
/// refresh started from the admin and a scheduled one never overlap.
/// </remarks>
[BackgroundTask(
    Title = "Connected Data Set Refresh",
    Schedule = "* * * * *",
    Description = "Refreshes the imported data sets whose schedule is due, or that someone asked to refresh.",
    LockTimeout = 1_000,
    LockExpiration = 3_600_000)]
public sealed class DataSetRefreshBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        // Absent when the feature's services aren't in this tenant's container, which isn't an error.
        var refreshService = serviceProvider.GetService<IDataSetRefreshService>();

        if (refreshService is null)
        {
            return;
        }

        var count = await refreshService.RefreshDueAsync(cancellationToken);

        if (count > 0)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<DataSetRefreshBackgroundTask>>();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Refreshed {Count} imported data set(s).", count);
            }
        }
    }
}
