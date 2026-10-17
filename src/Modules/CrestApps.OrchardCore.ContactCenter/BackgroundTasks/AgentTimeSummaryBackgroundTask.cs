using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Keeps the daily agent time summaries the time-based workforce reports read up to date. The background task lock is
/// what makes the roller single-writer across nodes.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Agent Time Summaries",
    Schedule = "*/5 * * * *",
    Description = "Summarizes each finished day of agent state changes for the workforce reports, and backfills earlier days.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class AgentTimeSummaryBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var roller = serviceProvider.GetRequiredService<AgentTimeSummaryRoller>();
        var logger = serviceProvider.GetRequiredService<ILogger<AgentTimeSummaryBackgroundTask>>();

        try
        {
            await roller.RollAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while updating the Contact Center agent time summaries.");
        }
    }
}
