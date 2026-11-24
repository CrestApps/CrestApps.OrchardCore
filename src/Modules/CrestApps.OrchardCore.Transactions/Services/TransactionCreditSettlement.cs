using CrestApps.Core.Support;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Completes the customer credit changes the timeline entries of a transaction wait for. An entry is saved first, as the
/// durable intent, with its <see cref="TransactionEvent.CreditChange"/> marked <see cref="TransactionCreditState.Pending"/>;
/// the credit is then changed with the entry's id as the reference, so it is changed once however often this runs, and
/// the entry is marked settled. A spend that finds too little credit takes its payment back with a reversing entry, and
/// a change that cannot be made marks the entry failed and records a payment issue for someone to handle.
/// </summary>
internal sealed class TransactionCreditSettlement
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly ITransactionManager _transactionManager;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    public TransactionCreditSettlement(
        ITransactionManager transactionManager,
        IDistributedLock distributedLock,
        ISession session,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger logger,
        IStringLocalizer stringLocalizer)
    {
        _transactionManager = transactionManager;
        _distributedLock = distributedLock;
        _session = session;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// The reference a credit change made for a timeline entry carries.
    /// </summary>
    public static string GetReference(string transactionId, string eventId)
        => $"Transaction:{transactionId}:{eventId}";

    /// <summary>
    /// Returns up to <paramref name="maxCount"/> transactions with an entry waiting for its credit change.
    /// </summary>
    public static async Task<IReadOnlyList<string>> GetPendingAsync(ISession session, int maxCount, CancellationToken cancellationToken)
    {
        var rows = await session
            .QueryIndex<TransactionIndex>(index => index.PendingCredit == true, collection: TransactionsConstants.CollectionName)
            .Take(maxCount)
            .ListAsync(cancellationToken);

        return [.. rows.Select(row => row.ItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Completes the waiting credit changes of a transaction, under the transaction's lock. Returns what became of them.
    /// </summary>
    public async Task<CreditSettlement> SettleAsync(string transactionId, CancellationToken cancellationToken)
    {
        var settlement = new CreditSettlement();

        if (string.IsNullOrEmpty(transactionId))
        {
            return settlement;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(DefaultTransactionCommandService.LockPrefix + transactionId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            // Another command holds the transaction; it completes the waiting changes itself.
            return settlement;
        }

        await using var _ = locker;

        var transaction = await _transactionManager.FindByIdAsync(transactionId, cancellationToken);
        var pending = transaction?.Events.Where(entry => entry.CreditState == TransactionCreditState.Pending && entry.CreditChange is not null and not 0m && !string.IsNullOrEmpty(entry.Id)).ToList();

        settlement.Transaction = transaction;

        if (pending is null || pending.Count == 0)
        {
            // Read under this lock, which is released now: the command that runs next reads the transaction again under
            // its own lock instead of using this copy.
            if (transaction is not null)
            {
                _session.Detach(transaction, TransactionsConstants.CollectionName);
            }

            return settlement;
        }

        var credit = _serviceProvider.GetService<ICustomerCreditService>();
        var payer = transaction.GetPayer();
        var currency = transaction.Currency;
        var title = transaction.Title;
        var outcomes = new List<(string EntryId, CreditOutcome Outcome, decimal Change, string Detail)>();

        foreach (var entry in pending)
        {
            var change = entry.CreditChange.Value;

            if (credit is null || !credit.CanHold(payer))
            {
                outcomes.Add((entry.Id, CreditOutcome.Failed, change, credit is null
                    ? S["Customer credit is not available any more (the Store Credit feature is off)."].Value
                    : S["The payer can't hold customer credit."].Value));

                continue;
            }

            var request = new CustomerCreditChange
            {
                Party = payer,
                Currency = currency,
                Amount = Math.Abs(change),
                Reason = change > 0m
                    ? S["Kept from {0}", title].Value
                    : S["Spent on {0}", title].Value,
                Reference = GetReference(transactionId, entry.Id),
                Actor = entry.ActorName,
            };

            var result = change > 0m
                ? await credit.AddAsync(request, cancellationToken)
                : await credit.SpendAsync(request, cancellationToken);

            if (result.Succeeded)
            {
                outcomes.Add((entry.Id, CreditOutcome.Settled, change, null));
            }
            else if (result.Error == CustomerCreditError.Busy)
            {
                outcomes.Add((entry.Id, CreditOutcome.Pending, change, null));
            }
            else if (result.Error == CustomerCreditError.InsufficientBalance && change < 0m &&
                entry.Type == TransactionEventType.PaymentRecorded &&
                string.Equals(entry.Method, TransactionsConstants.SettlementMethods.CustomerCredit, StringComparison.Ordinal))
            {
                outcomes.Add((entry.Id, CreditOutcome.TakenBack, change, S["The customer has only {0} {1} of credit.", CurrencyScale.Format(result.Balance, currency), currency].Value));
            }
            else
            {
                outcomes.Add((entry.Id, CreditOutcome.Failed, change, result.Error == CustomerCreditError.InsufficientBalance
                    ? S["The customer has only {0} {1} of credit, so it could not be taken back.", CurrencyScale.Format(result.Balance, currency), currency].Value
                    : S["The customer's credit could not be changed ({0}).", result.Error].Value));
            }
        }

        // Changing the credit commits the session, which forgets the transaction read before it: saving that copy would
        // write it again as a new document. Read it again, now, and mark the entries on the copy the session tracks.
        transaction = await _transactionManager.FindByIdAsync(transactionId, cancellationToken);

        if (transaction is null)
        {
            return settlement;
        }

        var now = _clock.UtcNow;
        var issues = new List<PaymentIssue>();
        var reversals = new List<(TransactionEvent Reversal, TransactionEvent Entry)>();

        foreach (var (entryId, outcome, change, detail) in outcomes)
        {
            var entry = transaction.Events.FirstOrDefault(candidate => string.Equals(candidate.Id, entryId, StringComparison.Ordinal));

            if (entry is null || entry.CreditState != TransactionCreditState.Pending)
            {
                continue;
            }

            switch (outcome)
            {
                case CreditOutcome.Settled:
                    entry.CreditState = TransactionCreditState.Settled;
                    settlement.Settled.Add(entry);
                    break;

                case CreditOutcome.TakenBack:
                    entry.CreditState = TransactionCreditState.Failed;
                    var reversal = TransactionReversal.Apply(
                        transaction,
                        entry,
                        detail,
                        S["The payment with customer credit was taken back, so it is owed again: {0}", detail].Value,
                        owed => S["The transaction owes {0} again, so it is no longer settled.", owed].Value,
                        now);
                    reversals.Add((reversal, entry));
                    settlement.TakenBack.Add((entry, detail));
                    break;

                case CreditOutcome.Failed:
                    entry.CreditState = TransactionCreditState.Failed;
                    settlement.Failed.Add((entry, detail));
                    issues.Add(new PaymentIssue
                    {
                        Kind = PaymentIssueKinds.CreditNotReconciled,
                        Message = change > 0m
                            ? S["{0} {1} of customer credit for {2} could not be kept: {3}", CurrencyScale.Format(change, currency), currency, title, detail].Value
                            : S["{0} {1} of customer credit for {2} could not be spent or taken back: {3}", CurrencyScale.Format(-change, currency), currency, title, detail].Value,
                        ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                        ReferenceId = transactionId,
                        Amount = Math.Abs(change),
                        Currency = currency,
                    });
                    break;

                default:
                    settlement.StillPending.Add(entry);
                    break;
            }
        }

        transaction.UpdatedUtc = now;

        await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);

        if (issues.Count > 0)
        {
            var recorder = _serviceProvider.GetService<IPaymentIssueRecorder>();

            foreach (var issue in issues)
            {
                if (recorder is null || !await recorder.RecordAsync(issue, cancellationToken))
                {
                    _logger.LogError("A customer credit change on transaction '{TransactionId}' could not be reconciled and no payment issue was recorded: {Message}", transactionId.SanitizeLogValue(), issue.Message.SanitizeLogValue());
                }
            }
        }

        await _session.SaveChangesAsync(cancellationToken);

        settlement.Transaction = transaction;

        foreach (var (reversal, entry) in reversals)
        {
            await _serviceProvider.GetServices<ITransactionEventHandler>().ReversedAsync(transaction, reversal, entry, _logger, cancellationToken);
        }

        return settlement;
    }

    private enum CreditOutcome
    {
        Settled,
        Pending,
        TakenBack,
        Failed,
    }
}

/// <summary>
/// What became of the waiting credit changes of a transaction.
/// </summary>
internal sealed class CreditSettlement
{
    public Transaction Transaction { get; set; }

    public List<TransactionEvent> Settled { get; } = [];

    public List<TransactionEvent> StillPending { get; } = [];

    public List<(TransactionEvent Entry, string Reason)> TakenBack { get; } = [];

    public List<(TransactionEvent Entry, string Reason)> Failed { get; } = [];
}
