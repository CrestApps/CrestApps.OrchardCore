using CrestApps.OrchardCore.Diagnostics;
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
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => ProcessOpenPlansAsync(serviceProvider, run, cancellationToken));

    private static async Task ProcessOpenPlansAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
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

            using var logScope = logger.BeginScope(new Dictionary<string, object> { ["InstallmentPlanId"] = plan.ItemId });

            try
            {
                var result = await planService.ProcessAsync(plan.ItemId, waitForLock: true, cancellationToken);

                run.AddItems(1, result.Succeeded ? "processed" : "not_processed");

                if (!result.Succeeded && logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Installment plan '{PlanId}' was not processed: {Errors}", plan.ItemId, string.Join(" ", result.Errors.Select(error => error.Value)));
                }
            }
            catch (Exception ex)
            {
                // One plan's problem must not stop the others from being collected.
                run.AddItems(1, CrestAppsOrchardCoreTelemetry.OutcomeError);
                logger.LogError(ex, "Failed to process installment plan '{PlanId}'.", plan.ItemId);
            }
        }
    }
}
