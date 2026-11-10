using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings.BackgroundTasks;

/// <summary>
/// Every five minutes: gives back the dates of unpaid orders whose hold ran out, and charges the booking balances that
/// are due to the cards their deposits were paid with.
/// </summary>
[BackgroundTask(
    Title = "Booking holds and balances",
    Schedule = "*/5 * * * *",
    Description = "Gives back the dates of unpaid bookings whose hold ended, and charges booking balances that are due.",
    LockTimeout = 3_000,
    LockExpiration = 300_000)]
public sealed class BookingsBackgroundTask : IBackgroundTask
{
    private const int BatchSize = 50;

    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => ReleaseAndChargeAsync(serviceProvider, run, cancellationToken));

    private static async Task ReleaseAndChargeAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var bookings = serviceProvider.GetRequiredService<BookingService>();
        var balances = serviceProvider.GetRequiredService<IBookingBalanceStore>();
        var charger = serviceProvider.GetRequiredService<BookingBalanceCharger>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<BookingsBackgroundTask>>();

        run.AddItems(await bookings.ReleaseExpiredAsync(BatchSize, cancellationToken), "released");

        foreach (var balance in await balances.ListDueAsync(clock.UtcNow, BatchSize, cancellationToken))
        {
            try
            {
                var failure = await charger.ChargeAsync(balance, cancellationToken);

                run.AddItems(1, failure is null ? "charged" : "charge_failed");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                run.AddItems(1, CrestAppsOrchardCoreTelemetry.OutcomeError);
                logger.LogError(ex, "The balance of order '{OrderNumber}' couldn't be charged.", balance.OrderNumber);
            }
        }
    }
}
