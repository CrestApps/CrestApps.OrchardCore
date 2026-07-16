using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Recovers Contact Center agents whose after-call work was orphaned or exceeded its deadline.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Agent Availability Recovery",
    Schedule = "* * * * *",
    Description = "Recovers agent capacity from orphaned or expired after-call work.",
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class AgentAvailabilityRecoveryBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var recoveryService = serviceProvider.GetRequiredService<IAgentAvailabilityRecoveryService>();
        var logger = serviceProvider.GetRequiredService<ILogger<AgentAvailabilityRecoveryBackgroundTask>>();

        try
        {
            var recovered = await recoveryService.RecoverAsync(cancellationToken);

            if (recovered > 0 && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Recovered {Count} Contact Center agent availability state(s).", recovered);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "An error occurred while recovering Contact Center agent availability.");
        }
    }
}
