using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Transactions.Tasks;

/// <summary>
/// Background task that sweeps outstanding transactions and sends payment reminders on the cadence
/// configured in <see cref="TransactionReminderSettings"/>. It also raises
/// <see cref="ITransactionEventHandler.BecameOverdueAsync"/>, once per transaction, for each one it finds unpaid after
/// the day it was due.
/// </summary>
[BackgroundTask(
    Title = "Transaction Payment Reminders",
    Schedule = "0 */6 * * *",
    Description = "Sends reminders for outstanding transactions on the configured cadence.",
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class TransactionReminderBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// Sweeps outstanding transactions, reports the ones that just became overdue, and sends due reminders.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var siteService = serviceProvider.GetRequiredService<ISiteService>();
        var settings = await siteService.GetSettingsAsync<TransactionReminderSettings>();

        var clock = serviceProvider.GetRequiredService<IClock>();
        var manager = serviceProvider.GetRequiredService<ITransactionManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<TransactionReminderBackgroundTask>>();

        var utcNow = clock.UtcNow;
        var candidates = await manager.GetOutstandingDueAsync(utcNow, cancellationToken);

        // Overdue is reported even when scheduled reminders are turned off: a module that keeps its own state in step
        // with a transaction needs to know, whether or not the owner is chased for it.
        await RaiseOverdueAsync(serviceProvider, manager, candidates, utcNow, logger, cancellationToken);

        if (!settings.Enabled)
        {
            return;
        }

        var reminderService = serviceProvider.GetRequiredService<ITransactionReminderService>();

        foreach (var transaction in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!IsReminderDue(transaction, settings, utcNow))
            {
                continue;
            }

            try
            {
                if (await reminderService.SendReminderAsync(transaction, cancellationToken))
                {
                    await manager.UpdateAsync(transaction, data: null, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send a payment reminder for transaction '{TransactionId}'.", transaction.ItemId);
            }
        }

        if (settings.UpcomingReminderDays <= 0)
        {
            return;
        }

        var comingDue = await manager.GetComingDueAsync(utcNow, utcNow.AddDays(settings.UpcomingReminderDays), cancellationToken);

        foreach (var transaction in comingDue)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!IsUpcomingReminderDue(transaction, settings, utcNow))
            {
                continue;
            }

            try
            {
                if (await reminderService.SendUpcomingReminderAsync(transaction, cancellationToken))
                {
                    await manager.UpdateAsync(transaction, data: null, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send an upcoming payment reminder for transaction '{TransactionId}'.", transaction.ItemId);
            }
        }
    }

    // Marks each transaction that has just become overdue, saves that, and only then raises the event, so it is raised
    // once per transaction: a save that fails leaves the mark off and the next sweep tries again.
    internal static async Task RaiseOverdueAsync(
        IServiceProvider serviceProvider,
        ITransactionManager manager,
        IEnumerable<Transaction> candidates,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        IEnumerable<ITransactionEventHandler> eventHandlers = null;

        foreach (var transaction in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!IsNewlyOverdue(transaction, utcNow))
            {
                continue;
            }

            transaction.BecameOverdueUtc = utcNow;

            try
            {
                await manager.UpdateAsync(transaction, data: null, cancellationToken);
            }
            catch (Exception ex)
            {
                transaction.BecameOverdueUtc = null;
                logger.LogError(ex, "Failed to record that transaction '{TransactionId}' became overdue.", transaction.ItemId);

                continue;
            }

            eventHandlers ??= serviceProvider.GetServices<ITransactionEventHandler>();

            await eventHandlers.BecameOverdueAsync(transaction, logger, cancellationToken);
        }
    }

    // Overdue once the day it was due has passed with money still owed, the same way a payment due today is not
    // behind yet. A transaction without a due date never becomes overdue.
    internal static bool IsNewlyOverdue(Transaction transaction, DateTime utcNow)
        => transaction.BecameOverdueUtc is null &&
            TransactionStateMachine.IsCollectable(transaction.Status) &&
            transaction.OutstandingAmount > 0m &&
            transaction.DueUtc is DateTime dueUtc &&
            dueUtc.Date < utcNow.Date;

    // One notice per payment, inside the window before it falls due. A payment created already inside the
    // window (a plan whose next payment is two days away) still gets it, because the window is measured from now.
    internal static bool IsUpcomingReminderDue(Transaction transaction, TransactionReminderSettings settings, DateTime utcNow)
        => settings.UpcomingReminderDays > 0 &&
            transaction.UpcomingReminderSentUtc is null &&
            transaction.OutstandingAmount > 0m &&
            transaction.DueUtc is DateTime dueUtc &&
            dueUtc > utcNow &&
            dueUtc <= utcNow.AddDays(settings.UpcomingReminderDays);

    internal static bool IsReminderDue(Transaction transaction, TransactionReminderSettings settings, DateTime utcNow)
    {
        if (transaction.OutstandingAmount <= 0m)
        {
            return false;
        }

        if (settings.MaxReminders > 0 && transaction.ReminderCount >= settings.MaxReminders)
        {
            return false;
        }

        DateTime nextEligibleUtc;

        if (transaction.ReminderCount == 0)
        {
            var baseline = transaction.DueUtc ?? transaction.CreatedUtc;
            nextEligibleUtc = baseline.AddDays(Math.Max(0, settings.FirstReminderDelayDays));
        }
        else
        {
            var baseline = transaction.LastReminderSentUtc ?? transaction.CreatedUtc;
            nextEligibleUtc = baseline.AddDays(Math.Max(1, settings.ReminderIntervalDays));
        }

        return utcNow >= nextEligibleUtc;
    }
}
