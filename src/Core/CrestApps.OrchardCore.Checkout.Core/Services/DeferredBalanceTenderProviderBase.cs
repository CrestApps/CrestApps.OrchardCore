using CrestApps.OrchardCore.Checkout.Core.Models;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// A checkout tender that pays part of a checkout with money owed later: the part goes on an outstanding invoice on the
/// transaction ledger, recorded under the checkout's session and <see cref="ObligationId"/> so completing the checkout
/// twice owes once, and a refund of it is waived off what is still owed. Booking balances and account terms are built on
/// it.
/// </summary>
public abstract class DeferredBalanceTenderProviderBase : ICheckoutTenderProvider
{
    private readonly ITransactionManager _transactions;
    private readonly ITransactionCommandService _commands;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IStringLocalizer _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeferredBalanceTenderProviderBase"/> class.
    /// </summary>
    /// <param name="transactions">The transaction ledger the invoice goes on.</param>
    /// <param name="commands">The transaction commands a refund is waived through.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="localizer">The localizer of the derived tender, for the default refund reason.</param>
    protected DeferredBalanceTenderProviderBase(
        ITransactionManager transactions,
        ITransactionCommandService commands,
        IClock clock,
        ILogger logger,
        IStringLocalizer localizer)
    {
        _transactions = transactions;
        _commands = commands;
        _clock = clock;
        _logger = logger;
        _localizer = localizer;
    }

    /// <inheritdoc/>
    public abstract string Key { get; }

    /// <inheritdoc/>
    public virtual bool PaysWithSavedPaymentMethod
        => false;

    /// <summary>
    /// Gets the obligation the invoice is recorded under in the checkout's session.
    /// </summary>
    protected abstract string ObligationId { get; }

    /// <inheritdoc/>
    public abstract Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public async Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        if (flow?.Session is not CheckoutSession session ||
            !string.Equals(session.SessionId, GetSessionId(reference), StringComparison.Ordinal) ||
            await PrepareDebitAsync(reference, session, amount, currency, cancellationToken) is not { } debit)
        {
            throw new InvalidOperationException($"The {Key} tender '{reference}' doesn't belong to this checkout.");
        }

        if (await _transactions.GetByObligationAsync(session.SessionId, ObligationId, cancellationToken) is not null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var transaction = await _transactions.NewAsync(cancellationToken: cancellationToken);

        transaction.Title = debit.Title;
        transaction.Source = debit.Source;
        transaction.OwnerId = session.OwnerId;
        transaction.OwnerKind = CustomerOwnerKind.Authenticated;
        transaction.ReferenceType = session.ReferenceType;
        transaction.ReferenceId = session.ReferenceId;
        transaction.ReferenceVersionId = session.ReferenceVersionId;

        if (!string.IsNullOrEmpty(debit.ParentReferenceType))
        {
            transaction.ParentReferenceType = debit.ParentReferenceType;
            transaction.ParentReferenceId = debit.ParentReferenceId;
        }

        transaction.CheckoutSessionId = session.SessionId;
        transaction.ObligationId = ObligationId;
        transaction.Currency = currency;
        transaction.Amount = amount;
        transaction.TaxAmount = 0m;
        transaction.TotalAmount = amount;
        transaction.AmountPaid = 0m;
        transaction.Status = TransactionStatus.Outstanding;
        transaction.CreatedUtc = now;
        transaction.UpdatedUtc = now;
        transaction.DueUtc = debit.DueUtc;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Created,
            Message = debit.Message,
        });

        await _transactions.CreateAsync(transaction, cancellationToken);
        await DebitedAsync(transaction, debit, session, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default)
    {
        var sessionId = GetSessionId(reference);

        if (amount <= 0m || string.IsNullOrEmpty(sessionId) ||
            await _transactions.GetByObligationAsync(sessionId, ObligationId, cancellationToken) is not { } transaction)
        {
            return;
        }

        // What's refunded comes off what's still owed; what was already paid on the invoice is refunded by the store.
        var adjustment = Math.Min(amount, transaction.OutstandingAmount);

        if (adjustment <= 0m)
        {
            _logger.LogWarning("A refund of {Amount} went to the '{Tender}' balance of checkout '{SessionId}', whose invoice was already paid; refund it directly.", amount, Key, sessionId);

            return;
        }

        var result = await _commands.AdjustAsync(transaction.ItemId, adjustment, TransactionAdjustmentKind.Waiver, string.IsNullOrWhiteSpace(reason) ? _localizer["Refunded"].Value : reason, cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("The '{Tender}' invoice '{TransactionId}' couldn't be lowered by a refund: {Errors}", Key, transaction.ItemId, string.Join(" ", result.Errors));

            return;
        }

        await CreditedAsync(reference, transaction, adjustment, cancellationToken);
    }

    /// <summary>
    /// Returns the checkout session a tender reference was made for.
    /// </summary>
    /// <param name="reference">The tender reference.</param>
    protected abstract string GetSessionId(string reference);

    /// <summary>
    /// Reads what the reference is for, such as the order or the account, and describes the invoice to open. Returns
    /// <see langword="null"/> when the reference names nothing this checkout may owe, and the debit is refused.
    /// </summary>
    /// <param name="reference">The tender reference.</param>
    /// <param name="session">The checkout session, already known to be the reference's.</param>
    /// <param name="amount">The amount owed later.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task<DeferredBalanceDebit> PrepareDebitAsync(string reference, CheckoutSession session, decimal amount, string currency, CancellationToken cancellationToken);

    /// <summary>
    /// Runs after the invoice is made, such as to schedule its charge. Does nothing by default.
    /// </summary>
    /// <param name="transaction">The invoice.</param>
    /// <param name="debit">What <see cref="PrepareDebitAsync"/> returned.</param>
    /// <param name="session">The checkout session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected virtual Task DebitedAsync(Transaction transaction, DeferredBalanceDebit debit, CheckoutSession session, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Runs after a refund was waived off the invoice, such as to cancel a scheduled charge once nothing is owed. Does
    /// nothing by default.
    /// </summary>
    /// <param name="reference">The tender reference.</param>
    /// <param name="transaction">The invoice, as it was before the refund.</param>
    /// <param name="adjustment">How much was waived.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected virtual Task CreditedAsync(string reference, Transaction transaction, decimal adjustment, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
