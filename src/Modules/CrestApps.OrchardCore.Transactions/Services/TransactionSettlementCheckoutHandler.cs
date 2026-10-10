using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Bridges the checkout framework and the transaction ledger so an outstanding transaction can be settled
/// online. When a checkout session references a transaction, the handler contributes the outstanding
/// balance as a one-time billing item and, once the checkout completes, settles the transaction against
/// the amount the payment provider actually confirmed rather than the amount the checkout requested.
/// </summary>
public sealed class TransactionSettlementCheckoutHandler : CheckoutHandlerBase
{
    private const string StepKey = "TransactionSettlement";

    private readonly ITransactionManager _transactionManager;
    private readonly IPaymentAttemptStore _paymentAttemptStore;
    private readonly ITransactionSettlementService _settlementService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionSettlementCheckoutHandler"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="paymentAttemptStore">The durable payment-attempt ledger used to read confirmed amounts.</param>
    /// <param name="settlementService">The service that applies confirmed attempts to the transaction.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionSettlementCheckoutHandler(
        ITransactionManager transactionManager,
        IPaymentAttemptStore paymentAttemptStore,
        ITransactionSettlementService settlementService,
        ILogger<TransactionSettlementCheckoutHandler> logger,
        IStringLocalizer<TransactionSettlementCheckoutHandler> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _paymentAttemptStore = paymentAttemptStore;
        _settlementService = settlementService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(CheckoutFlowInitializingContext context)
    {
        // Concealment is decided per request rather than stored, so the balance step is hidden again every time
        // the session is loaded.
        foreach (var step in context.Flow.Session.Steps)
        {
            if (string.Equals(step.Key, StepKey, StringComparison.Ordinal))
            {
                step.Conceal = true;
            }
        }

        return Task.CompletedTask;
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
            Key = StepKey,
            Title = S["Outstanding payment"],
            Description = transaction.Title,
            Order = 0,

            // The balance is what the customer came to pay, so the step asks them nothing; it exists only because
            // billing items belong to steps. Concealed, the checkout opens straight on the payment.
            CollectData = false,
            Conceal = true,
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

                    // The transaction already carries its own tax, decided when it was raised. Settling it is
                    // paying that total, not a new sale, so it must not be taxed a second time.
                    ExcludeFromTax = true,
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

        var attempts = await _paymentAttemptStore.GetBySessionAsync(session.SessionId);

        if (!attempts.Any(attempt => attempt.State == PaymentAttemptState.Succeeded))
        {
            _logger.LogWarning("Checkout session '{SessionId}' completed for transaction '{TransactionId}' but no confirmed payment attempt was found; the transaction is left unsettled.", session.SessionId, transaction.ItemId);

            return;
        }

        if (await _settlementService.ApplyAsync(transaction, attempts, session.SessionId) && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Transaction '{TransactionId}' was settled online through checkout session '{SessionId}'.", transaction.ItemId, session.SessionId);
        }
    }

    private static bool IsTransactionReference(string referenceType)
        => string.Equals(referenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase);
}
