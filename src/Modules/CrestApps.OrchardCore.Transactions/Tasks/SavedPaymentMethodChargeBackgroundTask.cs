using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.Tasks;

/// <summary>
/// Charges the transactions whose scheduled charge of a saved payment method is due, and the retries of the ones
/// declined before (<see cref="ISavedPaymentMethodChargeService"/>).
/// </summary>
[BackgroundTask(
    Title = "Saved Card Charges",
    Schedule = "30 * * * *",
    Description = "Charges the transactions scheduled to be charged to a saved card, and retries the declined ones on the retry schedule.",
    LockTimeout = 5_000,
    LockExpiration = 600_000)]
public sealed class SavedPaymentMethodChargeBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run =>
        {
            var clock = serviceProvider.GetRequiredService<IClock>();

            run.AddItems(await serviceProvider.GetRequiredService<ISavedPaymentMethodChargeService>().ChargeDueAsync(clock.UtcNow, cancellationToken), "charged");
        });
}
