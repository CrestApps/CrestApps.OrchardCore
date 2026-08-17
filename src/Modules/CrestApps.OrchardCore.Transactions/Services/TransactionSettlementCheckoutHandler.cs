using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Bridges the checkout framework and the transaction ledger so an outstanding transaction can be settled
/// online. When a checkout session references a transaction, the handler contributes the outstanding
/// balance as a one-time billing item and marks the transaction paid once the checkout completes.
/// </summary>
public sealed class TransactionSettlementCheckoutHandler : CheckoutHandlerBase
{
    private readonly ITransactionManager _transactionManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionSettlementCheckoutHandler"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="clock">The clock used for settlement timestamps.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionSettlementCheckoutHandler(
        ITransactionManager transactionManager,
        IClock clock,
        ILogger<TransactionSettlementCheckoutHandler> logger,
        IStringLocalizer<TransactionSettlementCheckoutHandler> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var session = context.Session;

        if (!IsTransactionReference(session.ReferenceType) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var transaction = await _transactionManager.FindByIdAsync(session.ReferenceId);

        if (transaction is null || transaction.OutstandingAmount <= 0m)
        {
            return;
        }

        if (string.IsNullOrEmpty(session.Currency))
        {
            session.Currency = transaction.Currency;
        }

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "TransactionSettlement",
            Title = S["Outstanding payment"],
            Description = transaction.Title,
            Order = 0,
            BillingItems =
            [
                new BillingItem
                {
                    ItemId = transaction.ItemId,
                    Description = string.IsNullOrEmpty(transaction.Title)
                        ? S["Outstanding payment"].Value
                        : transaction.Title,
                    Amount = transaction.OutstandingAmount,
                    Plan = null,
                },
            ],
        });
    }

    /// <inheritdoc/>
    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (context.Flow.Session is not CheckoutSession session)
        {
            return;
        }

        if (!IsTransactionReference(session.ReferenceType) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var transaction = await _transactionManager.FindByIdAsync(session.ReferenceId);

        if (transaction is null || transaction.Status == TransactionStatus.Paid)
        {
            return;
        }

        var now = _clock.UtcNow;

        transaction.AmountPaid = transaction.TotalAmount;
        transaction.Status = TransactionStatus.Paid;
        transaction.SettledUtc = now;
        transaction.UpdatedUtc = now;
        transaction.SettlementMethod = TransactionsConstants.SettlementMethods.Online;
        transaction.SettlementReference = session.SessionId;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.PaymentRecorded,
            Message = S["The transaction was paid online through checkout session '{0}'.", session.SessionId].Value,
        });

        await _transactionManager.UpdateAsync(transaction);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Transaction '{TransactionId}' was settled online through checkout session '{SessionId}'.", transaction.ItemId, session.SessionId);
        }
    }

    private static bool IsTransactionReference(string referenceType)
        => string.Equals(referenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase);
}
