using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Services;

/// <summary>
/// Creates the next billing cycle for recurring Pay Later commitments once the period they cover has ended.
/// </summary>
/// <remarks>
/// A gateway keeps the schedule for a card subscription and simply charges again. Pay Later has no gateway,
/// so if nothing advances the schedule a recurring commitment bills exactly once and then quietly stops —
/// the customer keeps what they subscribed to and the site owner never invoices for it again. This sweep is
/// that missing clock.
///
/// It is deliberately conservative: it never touches money, only records the next period's debt, and it
/// marks the cycle it came from as spawned before committing, so a retry or a second node cannot invoice the
/// same period twice.
/// </remarks>
[BackgroundTask(
    Title = "Pay Later Renewals",
    Schedule = "*/30 * * * *",
    Description = "Creates the next outstanding balance for recurring Pay Later commitments whose billing period has ended.",
    LockTimeout = 10_000,
    LockExpiration = 60_000)]
public sealed class PayLaterRenewalBackgroundTask : IBackgroundTask
{
    // A cycle is only created once the period has genuinely ended. Without a small grace the sweep and the
    // clock can disagree by milliseconds at the boundary and invoice a period a moment early.
    private static readonly TimeSpan _grace = TimeSpan.FromMinutes(1);

    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayLaterRenewalBackgroundTask"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PayLaterRenewalBackgroundTask(ILogger<PayLaterRenewalBackgroundTask> logger)
        => _logger = logger;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
        var siteService = serviceProvider.GetRequiredService<ISiteService>();
        var distributedLock = serviceProvider.GetRequiredService<IDistributedLock>();
        var localizer = serviceProvider.GetRequiredService<IStringLocalizer<PayLaterRenewalBackgroundTask>>();
        var clock = serviceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var due = await transactionManager.GetDueForRenewalAsync(now - _grace, cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        var settings = await siteService.GetSettingsAsync<PayLaterSettings>();

        foreach (var transaction in due)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!string.Equals(transaction.Source, PayLaterCheckoutPaymentProvider.ProcessorKey, StringComparison.OrdinalIgnoreCase))
            {
                // Another provider owns this agreement's schedule; only its own renewal path may advance it.
                continue;
            }

            try
            {
                await RenewAsync(transactionManager, distributedLock, localizer, settings, transaction, now, cancellationToken);
            }
            catch (Exception exception)
            {
                // One bad agreement must not stop the rest of the sweep. The cycle stays unspawned, so the
                // next run tries it again.
                _logger.LogError(exception, "Failed to create the next Pay Later billing cycle for transaction '{TransactionId}'.", transaction.ItemId);
            }
        }
    }

    private static async Task RenewAsync(
        ITransactionManager transactionManager,
        IDistributedLock distributedLock,
        IStringLocalizer localizer,
        PayLaterSettings settings,
        Transaction transaction,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var recurrence = transaction.Recurrence;

        if (recurrence?.GetNextCycleUtc() is null)
        {
            return;
        }

        // Two nodes running the sweep at the same moment would otherwise both see an unspawned cycle and
        // each create the next period's debt.
        var (locker, locked) = await distributedLock.TryAcquireLockAsync(
            "PAY_LATER_RENEWAL_" + transaction.ItemId,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(2));

        if (!locked)
        {
            return;
        }

        await using var _ = locker;

        // Re-read inside the lock: the holder of the lock may have just spawned this very cycle.
        var current = await transactionManager.FindByIdAsync(transaction.ItemId, cancellationToken);

        if (current?.Recurrence?.GetNextCycleUtc() is null)
        {
            return;
        }

        var next = await BuildNextCycleAsync(transactionManager, localizer, settings, current, now);

        // The source is marked before the new cycle is created so a crash between the two leaves the
        // schedule stopped rather than invoicing the same period on every later sweep.
        current.Recurrence.NextCycleCreated = true;

        current.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Note,
            Message = localizer["The next billing cycle was invoiced as transaction '{0}'.", next.ItemId].Value,
        });

        await transactionManager.UpdateAsync(current, cancellationToken: cancellationToken);
        await transactionManager.CreateAsync(next, cancellationToken: cancellationToken);
    }

    private static async Task<Transaction> BuildNextCycleAsync(
        ITransactionManager transactionManager,
        IStringLocalizer localizer,
        PayLaterSettings settings,
        Transaction source,
        DateTime now)
    {
        var recurrence = source.Recurrence;
        var periodStart = recurrence.PeriodEndUtc;

        var next = await transactionManager.NewAsync();

        next.Title = source.Title;
        next.Source = source.Source;
        next.OwnerId = source.OwnerId;
        next.OwnerKind = source.OwnerKind;
        next.GuestContactName = source.GuestContactName;
        next.GuestContactEmail = source.GuestContactEmail;
        next.ReferenceType = source.ReferenceType;
        next.ReferenceId = source.ReferenceId;
        next.ReferenceVersionId = source.ReferenceVersionId;
        next.CheckoutSessionId = source.CheckoutSessionId;

        // Each cycle needs its own obligation id: the checkout's per-obligation uniqueness check would
        // otherwise treat the new period as a duplicate of the first one and skip it.
        next.ObligationId = $"{source.ObligationId}:cycle-{recurrence.CycleNumber + 1}";
        next.Currency = source.Currency;

        // The next period bills what the agreement agreed to, which is the same amount as the cycle it
        // follows. Tax is carried forward rather than recomputed: re-rating a settled agreement with
        // today's rules would silently change what the customer signed up for.
        // Priced from the agreement's cycle amount rather than the previous debt: the first cycle may have
        // carried a one-off discount, and copying it forward would grant that discount on every cycle.
        next.Amount = recurrence.CycleAmount > 0m ? recurrence.CycleAmount : source.Amount;
        next.TaxAmount = recurrence.CycleAmount > 0m ? recurrence.CycleTaxAmount : source.TaxAmount;
        next.TotalAmount = next.Amount + next.TaxAmount;
        next.AmountPaid = 0m;
        next.Status = TransactionStatus.Outstanding;
        next.CreatedUtc = now;
        next.UpdatedUtc = now;
        next.DueUtc = settings.NetTermDays > 0 ? periodStart.AddDays(settings.NetTermDays) : null;

        next.Recurrence = new TransactionRecurrence
        {
            BillingDuration = recurrence.BillingDuration,
            DurationType = recurrence.DurationType,
            PeriodStartUtc = periodStart,
            PeriodEndUtc = recurrence.Advance(periodStart),
            CycleNumber = recurrence.CycleNumber + 1,
            CycleLimit = recurrence.CycleLimit,
            CycleAmount = recurrence.CycleAmount,
            CycleTaxAmount = recurrence.CycleTaxAmount,
        };

        next.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Created,
            Message = localizer["An outstanding Pay Later balance was recorded for the billing period starting {0:d}.", periodStart].Value,
        });

        return next;
    }
}
