using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Fulfillment.BackgroundTasks;

/// <summary>
/// Sends the paid orders waiting for fulfillment partners, and asks a person about any send that was interrupted.
/// </summary>
[BackgroundTask(
    Title = "Send orders to fulfillment partners",
    Schedule = "* * * * *",
    Description = "Sends the paid orders waiting for fulfillment partners, and flags any send that was interrupted.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class FulfillmentBackgroundTask : IBackgroundTask
{
    private const int BatchSize = 10;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run =>
        {
            var sender = serviceProvider.GetRequiredService<FulfillmentSender>();

            await sender.FlagStuckAsync(cancellationToken);

            var processed = await sender.ProcessDueAsync(BatchSize, cancellationToken);

            run.AddItems(processed);

            if (processed >= BatchSize)
            {
                run.BudgetExceeded();
            }
        });
}
