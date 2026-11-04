using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings.Handlers;

/// <summary>
/// Tells the booking payment handlers when money is applied to a booking balance's invoice, however it was paid: the card
/// kept from the deposit charged when it's due, the buyer paying from the pay link, or staff recording a payment. The
/// ledger applies each payment once, so each is reported once.
/// </summary>
internal sealed class BookingBalancePaymentHandler : ITransactionPaymentHandler
{
    private readonly IBookingBalanceStore _balances;
    private readonly IBookingStore _bookings;
    private readonly IEnumerable<IBookingPaymentHandler> _paymentHandlers;
    private readonly ILogger _logger;

    public BookingBalancePaymentHandler(
        IBookingBalanceStore balances,
        IBookingStore bookings,
        IEnumerable<IBookingPaymentHandler> paymentHandlers,
        ILogger<BookingBalancePaymentHandler> logger)
    {
        _balances = balances;
        _bookings = bookings;
        _paymentHandlers = paymentHandlers;
        _logger = logger;
    }

    public async Task PaymentRecordedAsync(TransactionPaymentRecordedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = context.Transaction;

        if (!string.Equals(transaction.ObligationId, BookingBalanceTenderProvider.ObligationId, StringComparison.Ordinal) ||
            !string.Equals(transaction.Source, BookingsConstants.TransactionSource, StringComparison.Ordinal) ||
            context.Payment.Amount is not > 0m)
        {
            return;
        }

        if (await _balances.FindByOrderAsync(transaction.ReferenceId, cancellationToken) is not { } balance ||
            !string.Equals(balance.TransactionId, transaction.ItemId, StringComparison.Ordinal))
        {
            return;
        }

        var bookings = (await _bookings.ListByOrderAsync(balance.OrderId, cancellationToken))
            .Where(booking => booking.ConfirmedUtc.HasValue)
            .ToList();

        if (bookings.Count == 0)
        {
            return;
        }

        var paymentContext = new BookingPaymentContext
        {
            Bookings = bookings,
            OrderId = balance.OrderId,
            OrderNumber = balance.OrderNumber,
            PaymentKind = BookingPaymentKind.Balance,
            Amount = context.Payment.Amount.Value,
            Currency = transaction.Currency ?? balance.Currency,
            Reference = transaction.ItemId,
        };

        await _paymentHandlers.InvokeAsync((handler, received) => handler.PaymentReceivedAsync(received, cancellationToken), paymentContext, _logger);
    }
}
