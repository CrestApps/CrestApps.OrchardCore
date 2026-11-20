using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.ExchangeRates.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ExchangeRates.Services;

/// <summary>
/// Fetches the day's rates from the provider the settings name, on the site's date. Does nothing without a provider.
/// </summary>
[BackgroundTask(
    Title = "Exchange Rate Update",
    Schedule = "20 * * * *",
    Description = "Fetches the day's exchange rates from the provider chosen in the exchange rate settings.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class ExchangeRateUpdateBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run =>
        {
            var localClock = serviceProvider.GetRequiredService<ILocalClock>();
            var today = DateOnly.FromDateTime((await localClock.GetLocalNowAsync()).DateTime);

            run.AddItems(await serviceProvider.GetRequiredService<ExchangeRateUpdater>().UpdateAsync(today, cancellationToken), "rates");
        });
}
