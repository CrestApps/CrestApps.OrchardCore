using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The default <see cref="ISavedPaymentMethodChargeService"/>. Each command runs under a lock of its own on the
/// transaction; the charge is the ledger's <see cref="ITransactionCommandService.ChargeSavedPaymentMethodAsync"/>, which
/// never charges twice for a charge already handed to the provider.
/// </summary>
public sealed class DefaultSavedPaymentMethodChargeService : ISavedPaymentMethodChargeService
{
    internal const string LockPrefix = "Transactions:AutoCharge:";

    private const int ChargesPerSweep = 100;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _transientRetryDelay = TimeSpan.FromHours(1);

    private readonly ITransactionManager _transactionManager;
    private readonly ITransactionCommandService _commands;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DefaultSavedPaymentMethodChargeService(
        ITransactionManager transactionManager,
        ITransactionCommandService commands,
        IDistributedLock distributedLock,
        ISession session,
        ISiteService siteService,
        IClock clock,
        IServiceProvider serviceProvider,
        ILogger<DefaultSavedPaymentMethodChargeService> logger,
        IStringLocalizer<DefaultSavedPaymentMethodChargeService> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _commands = commands;
        _distributedLock = distributedLock;
        _session = session;
        _siteService = siteService;
        _clock = clock;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<SavedPaymentMethodChargeResult> ScheduleAsync(string transactionId, string savedPaymentMethodId, DateTime chargeOnUtc, CancellationToken cancellationToken = default)
        => WithTransactionAsync(transactionId, async (transaction, result) =>
        {
            if (!TransactionStateMachine.CanRecordPayment(transaction) || transaction.OutstandingAmount <= 0m)
            {
                result.Fail(S["This transaction has no outstanding balance to charge."]);

                return false;
            }

            if (_serviceProvider.GetService<ISavedPaymentMethodStore>() is not { } paymentMethods)
            {
                result.Fail(S["Saved cards are charged through the Checkout feature. Enable it to charge one."]);

                return false;
            }

            var saved = string.IsNullOrEmpty(transaction.OwnerId)
                ? null
                : string.IsNullOrEmpty(savedPaymentMethodId)
                    ? await paymentMethods.GetDefaultAsync(transaction.OwnerId, cancellationToken)
                    : await paymentMethods.FindAsync(transaction.OwnerId, savedPaymentMethodId, cancellationToken);

            if (saved?.PaymentMethod is null || saved.PaymentMethod.IsExpired(_clock.UtcNow))
            {
                result.Fail(S["The owner kept no saved card that can be charged."]);

                return false;
            }

            var description = saved.PaymentMethod.Describe();

            transaction.AutoCharge = new TransactionAutoCharge
            {
                SavedPaymentMethodId = saved.ItemId,
                NextAttemptUtc = chargeOnUtc,
            };
            transaction.AutoCollection = new TransactionAutoCollection
            {
                ProviderKey = saved.PaymentMethod.ProviderKey,
                PaymentMethodDescription = description,
            };

            AddNote(transaction, S["The balance will be charged to {0} when it is due.", description].Value);

            return true;
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task<SavedPaymentMethodChargeResult> ChargeAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        string abandonedReason = null;

        var result = await WithTransactionAsync(transactionId, async (transaction, result) =>
        {
            if (transaction.AutoCharge is not { } scheduled)
            {
                result.Fail(S["No charge of a saved card is scheduled on this transaction."]);

                return false;
            }

            if (!TransactionStateMachine.CanRecordPayment(transaction) || transaction.OutstandingAmount <= 0m)
            {
                // Paid or closed meanwhile: there is nothing left to charge.
                transaction.AutoCharge = null;

                return true;
            }

            var now = _clock.UtcNow;
            var charged = await _commands.ChargeSavedPaymentMethodAsync(transaction.ItemId, scheduled.SavedPaymentMethodId, cancellationToken);

            // The charge saved the transaction it changed, and a save detaches what the session read before, so the
            // transaction is read again: changing the copy read before would put back the balance the charge paid.
            transaction = await _transactionManager.FindByIdAsync(transaction.ItemId, cancellationToken) ?? transaction;
            result.Transaction = transaction;

            var charge = transaction.AutoCharge ?? scheduled;

            transaction.AutoCharge = charge;
            charge.Attempts += charged.ChargeStatus == SavedPaymentMethodChargeStatus.Declined ? 1 : 0;
            charge.LastAttemptUtc = now;
            result.ChargeStatus = charged.ChargeStatus;

            switch (charged.ChargeStatus)
            {
                case SavedPaymentMethodChargeStatus.Succeeded:
                case SavedPaymentMethodChargeStatus.Pending:
                    transaction.AutoCharge = null;

                    return true;

                case SavedPaymentMethodChargeStatus.Failed:
                    // Nothing is known about the card: tried again soon, without using up one of its retries.
                    charge.LastFailure = string.Join(" ", charged.Errors);
                    charge.NextAttemptUtc = now.Add(_transientRetryDelay);
                    result.NextAttemptUtc = charge.NextAttemptUtc;

                    return true;

                case SavedPaymentMethodChargeStatus.Declined:
                    charge.LastFailure = string.Join(" ", charged.Errors);

                    var settings = await _siteService.GetSettingsAsync<TransactionReminderSettings>();

                    if (settings.GetChargeRetryDelay(charge.Attempts) is { } delay)
                    {
                        charge.NextAttemptUtc = now.Add(delay);
                        result.NextAttemptUtc = charge.NextAttemptUtc;

                        return true;
                    }

                    break;

                default:
                    // Needs the cardholder, or was refused before the provider was asked (an expired or removed card).
                    charge.LastFailure = string.Join(" ", charged.Errors);
                    break;
            }

            abandonedReason = charge.LastFailure;
            HandOver(transaction, charge.LastFailure);
            result.HandedOver = true;

            await RecordIssueAsync(transaction, charge.LastFailure, cancellationToken);

            return true;
        }, cancellationToken);

        if (result.HandedOver && result.Transaction is { } handedOver)
        {
            foreach (var handler in _serviceProvider.GetServices<ITransactionEventHandler>())
            {
                try
                {
                    await handler.AutoChargeAbandonedAsync(handedOver, abandonedReason, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "The transaction event handler '{Handler}' failed when the charge of transaction '{TransactionId}' was abandoned.", handler.GetType().FullName, handedOver.ItemId);
                }
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<int> ChargeDueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var due = await _session.QueryIndex<TransactionIndex>(
                index => index.AutoChargeNextAttemptUtc != null && index.AutoChargeNextAttemptUtc <= utcNow,
                collection: TransactionsConstants.CollectionName)
            .OrderBy(index => index.AutoChargeNextAttemptUtc)
            .Take(ChargesPerSweep)
            .ListAsync(cancellationToken);

        var charged = 0;

        foreach (var transactionId in due.Select(index => index.ItemId).Distinct(StringComparer.Ordinal))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // Each charge in a unit of work of its own: a charge refused by the checkout must not take the others
                // with it.
                await using var scope = _serviceProvider.CreateAsyncScope();

                var result = await scope.ServiceProvider.GetRequiredService<ISavedPaymentMethodChargeService>().ChargeAsync(transactionId, cancellationToken);

                if (result.ChargeStatus is SavedPaymentMethodChargeStatus.Succeeded or SavedPaymentMethodChargeStatus.Pending)
                {
                    charged++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The scheduled charge of transaction '{TransactionId}' failed.", transactionId);
            }
        }

        return charged;
    }

    /// <inheritdoc/>
    public Task<SavedPaymentMethodChargeResult> CancelAsync(string transactionId, CancellationToken cancellationToken = default)
        => WithTransactionAsync(transactionId, (transaction, result) =>
        {
            if (transaction.AutoCharge is null)
            {
                return Task.FromResult(false);
            }

            transaction.AutoCharge = null;
            transaction.AutoCollection = null;
            AddNote(transaction, S["The scheduled charge of the saved card was canceled; the owner pays the balance themselves."].Value);

            return Task.FromResult(true);
        }, cancellationToken);

    // The owner pays it themselves from now on: no card is charged, and the overdue reminders take over.
    private void HandOver(Transaction transaction, string reason)
    {
        var card = transaction.AutoCollection?.PaymentMethodDescription;

        transaction.AutoCharge = null;
        transaction.AutoCollection = null;

        AddNote(transaction, string.IsNullOrEmpty(reason)
            ? S["{0} was not charged again; the owner is asked to pay the balance.", card ?? S["The saved card"].Value].Value
            : S["{0} was not charged again ({1}); the owner is asked to pay the balance.", card ?? S["The saved card"].Value, reason].Value);
    }

    private async Task RecordIssueAsync(Transaction transaction, string reason, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<IPaymentIssueRecorder>() is not { } recorder)
        {
            return;
        }

        try
        {
            await recorder.RecordAsync(new PaymentIssue
            {
                Kind = PaymentIssueKinds.AutoChargeFailed,
                Message = S["The saved card could not be charged for {0}, so its owner was asked to pay.", transaction.Title].Value,
                Error = reason,
                ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                ReferenceId = transaction.ItemId,
                Amount = transaction.OutstandingAmount,
                Currency = transaction.Currency,
                CreatedUtc = _clock.UtcNow,
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The payment issue for the abandoned charge of transaction '{TransactionId}' could not be recorded.", transaction.ItemId);
        }
    }

    private void AddNote(Transaction transaction, string message)
    {
        var now = _clock.UtcNow;

        transaction.Events.Add(new TransactionEvent
        {
            Id = IdGenerator.GenerateId(),
            CreatedUtc = now,
            Type = TransactionEventType.Note,
            Message = message,
        });
        transaction.UpdatedUtc = now;
    }

    // Runs a command under the transaction's charge lock and saves the transaction when the command changed it.
    private async Task<SavedPaymentMethodChargeResult> WithTransactionAsync(
        string transactionId,
        Func<Transaction, SavedPaymentMethodChargeResult, Task<bool>> command,
        CancellationToken cancellationToken)
    {
        var result = new SavedPaymentMethodChargeResult();

        if (string.IsNullOrEmpty(transactionId))
        {
            return result.Fail(S["The transaction could not be found."]);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + transactionId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return result.Fail(S["This transaction is already being charged. Try again in a moment."]);
        }

        await using var _ = locker;

        var transaction = await _transactionManager.FindByIdAsync(transactionId, cancellationToken);

        if (transaction is null)
        {
            return result.Fail(S["The transaction could not be found."]);
        }

        result.Transaction = transaction;

        if (!await command(transaction, result))
        {
            return result;
        }

        // A command that read the transaction again (after a charge saved it) hands back the copy it changed.
        transaction = result.Transaction ?? transaction;

        try
        {
            await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return result.Fail(S["This transaction was changed by someone else meanwhile. Try again."]);
        }

        return result;
    }
}
