using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.BackgroundTasks;

/// <summary>
/// Saves each agent's adherence for their previous day once it is over in their time zone.
/// </summary>
[BackgroundTask(
    Title = "Workforce Adherence History",
    Schedule = "15 * * * *",
    Description = "Saves each agent's adherence and conformance for their previous day.",
    LockTimeout = 3_000,
    LockExpiration = 900_000)]
public sealed class AdherenceHistoryBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<AdherenceHistoryBackgroundTask>>();

            try
            {
                var saved = await serviceProvider.GetRequiredService<AdherenceService>().SavePreviousDaysAsync(cancellationToken);
                await serviceProvider.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

                run.AddItems(saved, "saved");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                run.ReportError(ex);
                logger.LogError(ex, "An error occurred while saving the adherence history.");
            }
        });
    }
}
