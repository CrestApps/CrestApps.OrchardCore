using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Recovers Contact Center agents whose after-call work was orphaned or exceeded its deadline.
/// <para>
/// The recovery pass is bounded by a hard wall-clock budget enforced through a linked
/// <see cref="System.Threading.CancellationTokenSource.CancelAfter(int)"/> that cancels in-flight work, and that
/// budget is kept safely below the distributed-lock expiration so a slow pass can never outlive its lock and let a
/// second node begin an overlapping recovery pass while agent-state transitions are still being written. Work that
/// does not fit in the budget is recovered on the following tick.
/// </para>
/// </summary>
[BackgroundTask(
    Title = "Contact Center Agent Availability Recovery",
    Schedule = "* * * * *",
    Description = "Recovers agent capacity from orphaned or expired after-call work.",
    LockTimeout = 5_000,
    LockExpiration = LockExpirationMilliseconds)]
public sealed class AgentAvailabilityRecoveryBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Set to twice the one-minute schedule so the lock is not
    /// released while a run is still in progress.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The maximum wall-clock duration of a single run, in milliseconds. Kept safely below
    /// <see cref="LockExpirationMilliseconds"/> so the run always finishes before the lock can expire, which
    /// guarantees the next scheduled tick cannot start an overlapping recovery pass on another node.
    /// </summary>
    private const int MaxRunDurationMilliseconds = 90_000;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var recoveryService = serviceProvider.GetRequiredService<IAgentAvailabilityRecoveryService>();
        var logger = serviceProvider.GetRequiredService<ILogger<AgentAvailabilityRecoveryBackgroundTask>>();

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(MaxRunDurationMilliseconds);
        var runToken = runCts.Token;

        try
        {
            var recovered = await recoveryService.RecoverAsync(runToken);

            if (recovered > 0 && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Recovered {Count} Contact Center agent availability state(s).", recovered);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "The agent availability recovery run reached its {BudgetMilliseconds} ms time budget; deferring the remaining work to the next scheduled tick.",
                    MaxRunDurationMilliseconds);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "An error occurred while recovering Contact Center agent availability.");
        }
    }
}
