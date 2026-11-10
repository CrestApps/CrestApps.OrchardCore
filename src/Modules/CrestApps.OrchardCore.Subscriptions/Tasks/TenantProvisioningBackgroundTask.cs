using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Tasks;

/// <summary>
/// Builds the sites customers have paid for, and keeps trying when an attempt fails.
/// </summary>
/// <remarks>
/// This is the half of tenant provisioning that makes it survivable. The checkout only writes down that a
/// site was bought; this sweep is what turns that record into a running site, and what keeps a slow recipe,
/// a brief database outage, or a deployment restart from leaving a paying customer with nothing.
///
/// It runs often because the customer is waiting. A job's own back-off, not the schedule, is what keeps a
/// persistently failing site from being retried in a tight loop.
/// </remarks>
[BackgroundTask(
    Title = "Tenant Provisioning",
    Schedule = "* * * * *",
    Description = "Creates the sites that have been paid for and retries the ones that failed.",
    LockTimeout = 10_000,
    LockExpiration = 900_000)]
public sealed class TenantProvisioningBackgroundTask : IBackgroundTask
{
    // One site at a time per sweep. Setup is heavy, and a burst of purchases must not saturate the host.
    private const int MaxJobsPerRun = 3;

    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningBackgroundTask"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public TenantProvisioningBackgroundTask(ILogger<TenantProvisioningBackgroundTask> logger)
        => _logger = logger;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => ProvisionDueAsync(serviceProvider, run, cancellationToken));

    private async Task ProvisionDueAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var jobStore = serviceProvider.GetRequiredService<ITenantProvisioningJobStore>();
        var provisioningService = serviceProvider.GetRequiredService<ITenantProvisioningService>();
        var clock = serviceProvider.GetRequiredService<IClock>();

        var due = await jobStore.GetDueAsync(clock.UtcNow, cancellationToken);

        if (due.Count == 0)
        {
            run.Skip();

            return;
        }

        if (due.Count > MaxJobsPerRun)
        {
            // The rest wait for the next run, a minute away.
            run.BudgetExceeded();
        }

        foreach (var job in due.Take(MaxJobsPerRun))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var status = await provisioningService.ProvisionAsync(job.ItemId, cancellationToken);

                run.AddItems(1, status switch
                {
                    TenantProvisioningStatus.Succeeded => "succeeded",
                    TenantProvisioningStatus.Abandoned => "abandoned",
                    TenantProvisioningStatus.Failed => "failed",
                    _ => "busy",
                });
            }
            catch (Exception exception)
            {
                // One bad job must not stop the sweep; its own back-off brings it round again.
                run.AddItems(1, CrestAppsOrchardCoreTelemetry.OutcomeError);
                _logger.LogError(exception, "The provisioning sweep failed on job '{JobId}'.", job.ItemId);
            }
        }
    }
}
