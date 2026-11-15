using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Charges an issued invoice marked <see cref="Invoice.AutoCharge"/> to the payer's default saved card: at once when it
/// is due on issue, else on its due date, retried by <see cref="ISavedPaymentMethodChargeService"/> when declined. When
/// the payer kept no card that can be charged, or the retries run out, the invoice is sent to them to pay instead.
/// </summary>
public sealed class InvoiceAutoCharge : IInvoiceObserver, ITransactionEventHandler
{
    private readonly ITransactionManager _transactions;
    private readonly IInvoiceStore _invoices;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public InvoiceAutoCharge(
        ITransactionManager transactions,
        IInvoiceStore invoices,
        IClock clock,
        ILocalClock localClock,
        IServiceProvider serviceProvider,
        ILogger<InvoiceAutoCharge> logger)
    {
        _transactions = transactions;
        _invoices = invoices;
        _clock = clock;
        _localClock = localClock;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task IssuedAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        // A payment schedule is charged payment by payment by its installment plan.
        if (invoice?.Schedule is { Count: > 0 })
        {
            return;
        }

        if (invoice is not { AutoCharge: true } ||
            string.IsNullOrEmpty(invoice.TransactionId) ||
            _serviceProvider.GetService<ISavedPaymentMethodChargeService>() is not { } charges)
        {
            return;
        }

        var transaction = await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken);

        if (transaction is null || transaction.OutstandingAmount <= 0m)
        {
            return;
        }

        // Charged on the morning of its due day in the site's time zone, or now when it is due on issue (or already).
        var now = _clock.UtcNow;
        var chargeOn = now;

        if (invoice.DueDate is { } dueDate && invoice.IssueDate is { } issueDate && dueDate > issueDate)
        {
            var dueStart = await _localClock.ToUtcLenientAsync(dueDate.ToDateTime(TimeOnly.MinValue));

            chargeOn = dueStart > now ? dueStart : now;
        }
        var scheduled = await charges.ScheduleAsync(transaction.ItemId, savedPaymentMethodId: null, chargeOn, cancellationToken);

        if (!scheduled.Succeeded)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Invoice '{InvoiceId}' was not charged to a saved card ({Reason}); it is sent to be paid instead.", invoice.ItemId, string.Join(" ", scheduled.Errors));
            }

            await SendAsync(invoice.ItemId, cancellationToken);

            return;
        }

        if (chargeOn <= now)
        {
            // Due on issue: charged now. A charge that is declined is retried on schedule; one that can't be retried
            // hands the invoice to the payer, which sends it (AutoChargeAbandonedAsync).
            await charges.ChargeAsync(transaction.ItemId, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task AutoChargeAbandonedAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
    {
        // A part of a payment schedule is the installment plan's to follow up: it tells the customer about that part.
        if (InvoiceTransactionLink.IsScheduledPayment(transaction))
        {
            return;
        }

        if (await InvoiceTransactionLink.FindInvoiceAsync(_invoices, transaction, cancellationToken) is not { Status: InvoiceStatus.Open } invoice)
        {
            return;
        }

        await SendAsync(invoice.ItemId, cancellationToken);
    }

    private async Task SendAsync(string invoiceId, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<IInvoiceSender>() is not { } sender)
        {
            return;
        }

        var sent = await sender.SendAsync(new InvoiceSendRequest { InvoiceId = invoiceId }, cancellationToken);

        if (!sent.Succeeded && _logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Invoice '{InvoiceId}' could not be sent to be paid: {Reason}", invoiceId, string.Join(" ", sent.Errors));
        }
    }

    Task ITransactionEventHandler.CreatedAsync(Transaction transaction, CancellationToken cancellationToken)
        => Task.CompletedTask;

    Task ITransactionEventHandler.StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken)
        => Task.CompletedTask;

    Task ITransactionEventHandler.CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken)
        => Task.CompletedTask;

    Task ITransactionEventHandler.BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
