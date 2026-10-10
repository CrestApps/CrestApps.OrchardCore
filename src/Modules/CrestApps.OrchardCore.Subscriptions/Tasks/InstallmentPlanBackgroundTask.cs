using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Subscriptions.Tasks;

/// <summary>
/// Keeps every open installment plan moving: starts a plan whose down payment arrived without the administrator's
/// page seeing it, charges or invoices the payments that fell due, retries failed charges, and finishes plans that
/// are paid in full.
/// </summary>
[BackgroundTask(
    Title = "Installment Plans",
    Schedule = "*/15 * * * *",
    Description = "Collects the payments of installment plans as they fall due.",
    LockTimeout = 10_000,
    LockExpiration = 900_000)]
public sealed class InstallmentPlanBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var store = serviceProvider.GetRequiredService<IInstallmentPlanStore>();
        var planService = serviceProvider.GetRequiredService<IInstallmentPlanService>();
        var logger = serviceProvider.GetRequiredService<ILogger<InstallmentPlanBackgroundTask>>();

        var plans = await store.GetOpenAsync(cancellationToken);

        foreach (var plan in plans)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await planService.ProcessAsync(plan.ItemId, waitForLock: true, cancellationToken);

                if (!result.Succeeded && logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Installment plan '{PlanId}' was not processed: {Errors}", plan.ItemId, string.Join(" ", result.Errors.Select(error => error.Value)));
                }
            }
            catch (Exception ex)
            {
                // One plan's problem must not stop the others from being collected.
                logger.LogError(ex, "Failed to process installment plan '{PlanId}'.", plan.ItemId);
            }
        }
    }
}
