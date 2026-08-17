using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Handlers;

/// <summary>
/// Turns a completed Pay Later checkout into outstanding <see cref="Transaction"/> ledger entries. Because
/// Pay Later never moves money at a gateway, each succeeded Pay Later payment attempt leaves a balance the
/// customer still owes; this handler records that balance so it can be reported, reminded about, and
/// settled through the provider-agnostic Transactions module. Creation is idempotent per obligation so a
/// checkout that completes more than once never duplicates the debt.
/// </summary>
public sealed class PayLaterTransactionCheckoutHandler : CheckoutHandlerBase
{
    private readonly IPaymentAttemptStore _paymentAttemptStore;
    private readonly ITransactionManager _transactionManager;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayLaterTransactionCheckoutHandler"/> class.
    /// </summary>
    /// <param name="paymentAttemptStore">The payment attempt store used to find Pay Later commitments.</param>
    /// <param name="transactionManager">The transaction manager used to record outstanding balances.</param>
    /// <param name="siteService">The site service used to read the Pay Later settings.</param>
    /// <param name="clock">The clock used to timestamp the created transactions.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayLaterTransactionCheckoutHandler(
        IPaymentAttemptStore paymentAttemptStore,
        ITransactionManager transactionManager,
        ISiteService siteService,
        IClock clock,
        ILogger<PayLaterTransactionCheckoutHandler> logger,
        IStringLocalizer<PayLaterTransactionCheckoutHandler> stringLocalizer)
    {
        _paymentAttemptStore = paymentAttemptStore;
        _transactionManager = transactionManager;
        _siteService = siteService;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (context.Flow.Session is not CheckoutSession session)
        {
            return;
        }

        // A settlement checkout for an existing transaction is handled by the Transactions module. Never
        // create a new transaction from one, otherwise settling a debt would create another debt.
        if (string.Equals(session.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var attempts = await _paymentAttemptStore.GetBySessionAsync(session.SessionId);

        var payLaterAttempts = attempts
            .Where(attempt =>
                string.Equals(attempt.ProviderKey, PayLaterCheckoutPaymentProvider.ProcessorKey, StringComparison.OrdinalIgnoreCase) &&
                attempt.State == PaymentAttemptState.Succeeded)
            .ToArray();

        if (payLaterAttempts.Length == 0)
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<PayLaterSettings>();
        var now = _clock.UtcNow;

        DateTime? dueUtc = settings.NetTermDays > 0
            ? now.AddDays(settings.NetTermDays)
            : null;

        foreach (var attempt in payLaterAttempts)
        {
            var existing = await _transactionManager.GetByObligationAsync(session.SessionId, attempt.ObligationId);

            if (existing is not null)
            {
                continue;
            }

            var title = ResolveTitle(session, attempt);

            var transaction = await _transactionManager.NewAsync();

            transaction.Title = title;
            transaction.Source = PayLaterCheckoutPaymentProvider.ProcessorKey;
            transaction.OwnerId = session.OwnerId;
            transaction.ReferenceType = session.ReferenceType;
            transaction.ReferenceId = session.ReferenceId;
            transaction.ReferenceVersionId = session.ReferenceVersionId;
            transaction.CheckoutSessionId = session.SessionId;
            transaction.ObligationId = attempt.ObligationId;
            transaction.Currency = attempt.Currency ?? session.Currency;
            transaction.Amount = attempt.ExpectedAmount;
            transaction.TaxAmount = attempt.ExpectedTaxAmount;
            transaction.TotalAmount = attempt.ExpectedAmount + attempt.ExpectedTaxAmount;
            transaction.AmountPaid = 0m;
            transaction.Status = TransactionStatus.Outstanding;
            transaction.CreatedUtc = now;
            transaction.UpdatedUtc = now;
            transaction.DueUtc = dueUtc;
            transaction.Events.Add(new TransactionEvent
            {
                CreatedUtc = now,
                Type = TransactionEventType.Created,
                Message = S["An outstanding Pay Later balance was recorded from checkout session '{0}'.", session.SessionId].Value,
            });

            await _transactionManager.CreateAsync(transaction);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Created an outstanding Pay Later transaction '{TransactionId}' for obligation '{ObligationId}' of checkout session '{SessionId}'.", transaction.ItemId, attempt.ObligationId, session.SessionId);
            }
        }
    }

    private string ResolveTitle(CheckoutSession session, PaymentAttempt attempt)
    {
        var billingItem = session.Steps?
            .SelectMany(step => step.BillingItems ?? [])
            .FirstOrDefault(item => string.Equals(item.ItemId, attempt.ObligationId, StringComparison.Ordinal));

        if (billingItem is not null && !string.IsNullOrEmpty(billingItem.Description))
        {
            return billingItem.Description;
        }

        var firstDescription = session.Steps?
            .SelectMany(step => step.BillingItems ?? [])
            .Select(item => item.Description)
            .FirstOrDefault(description => !string.IsNullOrEmpty(description));

        return firstDescription ?? S["Pay Later balance"].Value;
    }
}
