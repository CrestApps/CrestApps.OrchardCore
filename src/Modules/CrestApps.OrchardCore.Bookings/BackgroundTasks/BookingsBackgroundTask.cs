using CrestApps.OrchardCore.Bookings.Services;
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

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var bookings = serviceProvider.GetRequiredService<BookingService>();
        var balances = serviceProvider.GetRequiredService<IBookingBalanceStore>();
        var charger = serviceProvider.GetRequiredService<BookingBalanceCharger>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<BookingsBackgroundTask>>();

        await bookings.ReleaseExpiredAsync(BatchSize, cancellationToken);

        foreach (var balance in await balances.ListDueAsync(clock.UtcNow, BatchSize, cancellationToken))
        {
            try
            {
                await charger.ChargeAsync(balance, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "The balance of order '{OrderNumber}' couldn't be charged.", balance.OrderNumber);
            }
        }
    }
}
