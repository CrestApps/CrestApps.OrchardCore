using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.BackgroundTasks;

/// <summary>
/// Raises an alert for every agent out of adherence long enough, once per episode.
/// </summary>
[BackgroundTask(
    Title = "Workforce Adherence Alerts",
    Schedule = "* * * * *",
    Description = "Raises the Agent Out Of Adherence event for agents out of adherence longer than their rule allows.",
    LockTimeout = 3_000,
    LockExpiration = 60_000)]
public sealed class AdherenceAlertsBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<AdherenceAlertsBackgroundTask>>();

            try
            {
                var raised = await serviceProvider.GetRequiredService<AdherenceService>().RaiseAlertsAsync(cancellationToken);
                await serviceProvider.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

                run.AddItems(raised, "raised");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                run.ReportError(ex);
                logger.LogError(ex, "An error occurred while raising the adherence alerts.");
            }
        });
    }
}
