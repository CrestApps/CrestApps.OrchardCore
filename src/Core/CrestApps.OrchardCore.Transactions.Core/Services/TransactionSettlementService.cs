using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The default <see cref="ITransactionSettlementService"/>. Settlement is applied against the amount the provider
/// actually confirmed, never the amount that was asked for, and is idempotent per attempt on the transaction's own
/// timeline.
/// </summary>
public sealed class TransactionSettlementService : ITransactionSettlementService
{
    private readonly ITransactionManager _transactionManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionSettlementService"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="serviceProvider">The service provider the payment handlers are resolved from when they are needed.</param>
    /// <param name="clock">The clock used for settlement timestamps.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionSettlementService(
        ITransactionManager transactionManager,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<TransactionSettlementService> logger,
        IStringLocalizer<TransactionSettlementService> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(Transaction transaction, IEnumerable<PaymentAttempt> attempts, string settlementReference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var confirmed = (attempts ?? []).Where(attempt => attempt.State == PaymentAttemptState.Succeeded).ToArray();

        if (confirmed.Length == 0)
        {
            return false;
        }

        // Every confirmed attempt must be in the transaction currency. An empty attempt currency does not match a
        // typed transaction currency, so the check fails closed and no implicit conversion is ever applied.
        foreach (var attempt in confirmed)
        {
            if (!string.Equals(attempt.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Refusing to settle transaction '{TransactionId}' ({TransactionCurrency}) from payment attempt '{AttemptId}' ({AttemptCurrency}): the currencies differ and no conversion is applied.", transaction.ItemId, transaction.Currency, attempt.ItemId, attempt.Currency);

                return false;
            }
        }

        // Idempotency is per confirmed attempt, recorded on the durable timeline. An attempt already applied is never
        // counted again, even when a later partial settlement from a different session overwrote the scalar
        // settlement reference or a webhook is replayed out of order.
        var appliedAttemptIds = transaction.Events
            .Where(payment => payment.Type == TransactionEventType.PaymentRecorded && !string.IsNullOrEmpty(payment.PaymentAttemptId))
            .Select(payment => payment.PaymentAttemptId)
            .ToHashSet(StringComparer.Ordinal);

        var newAttempts = confirmed.Where(attempt => !appliedAttemptIds.Contains(attempt.ItemId)).ToArray();

        if (newAttempts.Length == 0)
        {
            return false;
        }

        var confirmedTotal = newAttempts.Sum(attempt => attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount);

        if (confirmedTotal <= 0m)
        {
            _logger.LogWarning("Payment attempts for transaction '{TransactionId}' confirmed no positive amount; the transaction is left unsettled.", transaction.ItemId);

            return false;
        }

        var now = _clock.UtcNow;

        transaction.AmountPaid = CurrencyScale.Round(transaction.AmountPaid + confirmedTotal, transaction.Currency);

        var fullyPaid = transaction.AmountPaid >= transaction.TotalAmount;

        transaction.PaymentAttemptId = newAttempts.Last().ItemId;
        transaction.Status = fullyPaid ? TransactionStatus.Paid : TransactionStatus.PartiallyPaid;
        transaction.SettlementMethod = TransactionsConstants.SettlementMethods.Online;
        transaction.SettlementReference = settlementReference;
        transaction.UpdatedUtc = now;

        if (fullyPaid)
        {
            transaction.SettledUtc = now;
        }

        var payments = new List<TransactionEvent>();

        foreach (var attempt in newAttempts)
        {
            var paid = attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount;
            var payment = TransactionPaymentHandlerExtensions.CreatePaymentEvent(
                now,
                paid,
                TransactionsConstants.SettlementMethods.Online,
                S["Recorded a payment of {0} {1} against the transaction from checkout session '{2}' (payment attempt '{3}').", CurrencyScale.Format(paid, transaction.Currency), transaction.Currency, attempt.SessionId, attempt.ItemId].Value);

            payment.PaymentAttemptId = attempt.ItemId;

            transaction.Events.Add(payment);
            payments.Add(payment);
        }

        try
        {
            await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            _logger.LogWarning("A concurrency conflict prevented settling transaction '{TransactionId}'; another writer updated it first.", transaction.ItemId);

            return false;
        }

        // Resolved here rather than injected: a handler may itself start a payment through the checkout, and the
        // checkout is what applies payments, so injecting them would make the two depend on each other.
        await _serviceProvider.GetServices<ITransactionPaymentHandler>().PaymentRecordedAsync(transaction, payments, _logger, cancellationToken);

        return true;
    }
}
