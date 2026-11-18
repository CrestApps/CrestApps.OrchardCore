using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Bookings.Handlers;

/// <summary>
/// Keeps a booking balance in step with the saved card charge service charging its invoice: a declined charge is
/// recorded with its retry, and one that is no longer retried leaves the balance to the buyer.
/// </summary>
internal sealed class BookingBalanceChargeHandler : TransactionEventHandlerBase
{
    private readonly BookingBalanceCharger _charger;

    public BookingBalanceChargeHandler(BookingBalanceCharger charger)
    {
        _charger = charger;
    }

    public override Task AutoChargeDeclinedAsync(Transaction transaction, string reason, DateTime nextAttemptUtc, CancellationToken cancellationToken = default)
        => _charger.RecordDeclinedAsync(transaction, reason, nextAttemptUtc, cancellationToken);

    public override Task AutoChargeAbandonedAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
        => _charger.RecordAbandonedAsync(transaction, reason, cancellationToken);
}
