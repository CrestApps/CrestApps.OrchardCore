using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.Tasks;

/// <summary>
/// Background task that sweeps outstanding transactions and sends payment reminders on the cadence
/// configured in <see cref="TransactionReminderSettings"/>. It also raises
/// <see cref="ITransactionEventHandler.BecameOverdueAsync"/>, once per transaction, for each one it finds unpaid after
/// the day it was due.
/// </summary>
[BackgroundTask(
    Title = "Transaction Payment Reminders",
    Schedule = "0 * * * *",
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
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => SweepAsync(serviceProvider, run, cancellationToken));

    private static async Task SweepAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var siteService = serviceProvider.GetRequiredService<ISiteService>();
        var settings = await siteService.GetSettingsAsync<TransactionReminderSettings>();

        var clock = serviceProvider.GetRequiredService<IClock>();
        var manager = serviceProvider.GetRequiredService<ITransactionManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<TransactionReminderBackgroundTask>>();

        // Entries a stopped process left waiting for their customer credit change are completed first, so nothing stays
        // half done; each completes under its transaction's lock and records a payment issue when it can't.
        run.AddItems(await ReconcileCreditAsync(serviceProvider, logger, cancellationToken), "credit_reconciled");

        var utcNow = clock.UtcNow;
        var candidates = await manager.GetOutstandingDueAsync(utcNow, cancellationToken);

        // Overdue is reported even when scheduled reminders are turned off: a module that keeps its own state in step
        // with a transaction needs to know, whether or not the owner is chased for it.
        run.AddItems(await RaiseOverdueAsync(serviceProvider, manager, candidates, utcNow, logger, cancellationToken), "became_overdue");

        if (!settings.Enabled)
        {
            return;
        }

        // Reminders go out in the site's daytime only; the sweep runs every hour, so the next one in the hours sends them.
        if (serviceProvider.GetService<ILocalClock>() is { } localClock &&
            !settings.IsSendingHour((await localClock.GetLocalNowAsync()).Hour))
        {
            return;
        }

        var reminderService = serviceProvider.GetRequiredService<ITransactionReminderService>();

        if (TransactionReminderSchedule.IsStaged(settings))
        {
            await SendStagedAsync(serviceProvider, manager, reminderService, candidates, settings, utcNow, run, logger, cancellationToken);

            return;
        }

        foreach (var transaction in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // A transaction charged to a saved card is not chased while the charge and its retries are pending.
            if (transaction.RemindersPaused || transaction.AutoCharge is not null || !IsReminderDue(transaction, settings, utcNow))
            {
                continue;
            }

            try
            {
                if (await IsStillOwedAsync(serviceProvider, transaction, cancellationToken) &&
                    await reminderService.SendReminderAsync(transaction, cancellationToken))
                {
                    await manager.UpdateAsync(transaction, data: null, cancellationToken);
                    run.AddItems(1, "reminded");
                }
            }
            catch (Exception ex)
            {
                run.AddItems(1, CrestAppsOrchardCoreTelemetry.OutcomeError);
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

            if (transaction.RemindersPaused || !IsUpcomingReminderDue(transaction, settings, utcNow))
            {
                continue;
            }

            try
            {
                if (await IsStillOwedAsync(serviceProvider, transaction, cancellationToken) &&
                    await reminderService.SendUpcomingReminderAsync(transaction, cancellationToken))
                {
                    await manager.UpdateAsync(transaction, data: null, cancellationToken);
                    run.AddItems(1, "reminded_upcoming");
                }
            }
            catch (Exception ex)
            {
                run.AddItems(1, CrestAppsOrchardCoreTelemetry.OutcomeError);
                logger.LogError(ex, "Failed to send an upcoming payment reminder for transaction '{TransactionId}'.", transaction.ItemId);
            }
        }
    }

    // The staged policy: each transaction gets the latest step whose day has come, once. A step before the due date says
    // it is coming due (and names the card it will be charged to); the others say it is owed, except while a charge of a
    // saved card is pending.
    private static async Task SendStagedAsync(
        IServiceProvider serviceProvider,
        ITransactionManager manager,
        ITransactionReminderService reminderService,
        IReadOnlyList<Transaction> overdue,
        TransactionReminderSettings settings,
        DateTime utcNow,
        MeasuredTaskRun run,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var steps = TransactionReminderSchedule.GetSteps(settings);
        var lead = TransactionReminderSchedule.GetLeadDays(settings);
        var candidates = overdue.ToList();

        if (lead > 0)
        {
            var seen = candidates.Select(transaction => transaction.ItemId).ToHashSet(StringComparer.Ordinal);

            candidates.AddRange((await manager.GetComingDueAsync(utcNow, utcNow.Date.AddDays(lead + 1), cancellationToken))
                .Where(transaction => seen.Add(transaction.ItemId)));
        }

        foreach (var transaction in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (TransactionReminderSchedule.GetDueStep(transaction, settings, utcNow) is not int step)
            {
                continue;
            }

            var before = steps[step] < 0;

            if (!before && transaction.AutoCharge is not null)
            {
                continue;
            }

            try
            {
                if (!await IsStillOwedAsync(serviceProvider, transaction, cancellationToken))
                {
                    continue;
                }

                var sent = before
                    ? await reminderService.SendUpcomingReminderAsync(transaction, cancellationToken)
                    : await reminderService.SendReminderAsync(transaction, cancellationToken);

                if (sent)
                {
                    transaction.ReminderStepsSent = step + 1;
                    await manager.UpdateAsync(transaction, data: null, cancellationToken);
                    run.AddItems(1, before ? "reminded_upcoming" : "reminded");
                }
            }
            catch (Exception ex)
            {
                run.AddItems(1, CrestAppsOrchardCoreTelemetry.OutcomeError);
                logger.LogError(ex, "Failed to send a staged payment reminder for transaction '{TransactionId}'.", transaction.ItemId);
            }
        }
    }

    // Reads the transaction again, in a unit of work of its own, right before a reminder is sent: one paid, paused or
    // closed since the sweep listed it, or with a payment being taken right now, gets no reminder.
    internal static async Task<bool> IsStillOwedAsync(IServiceProvider serviceProvider, Transaction transaction, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var current = await scope.ServiceProvider.GetRequiredService<ITransactionManager>().FindByIdAsync(transaction.ItemId, cancellationToken) ?? transaction;

        if (current.OutstandingAmount <= 0m || current.RemindersPaused || !TransactionStateMachine.IsCollectable(current.Status))
        {
            return false;
        }

        if (scope.ServiceProvider.GetService<ICheckoutSessionStore>() is { } sessions)
        {
            var checkout = await sessions.GetByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, transaction.ItemId, cancellationToken: cancellationToken);

            if (checkout?.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending)
            {
                return false;
            }
        }

        return true;
    }

    // Completes the customer credit changes left waiting, a page of transactions per sweep.
    internal static async Task<int> ReconcileCreditAsync(IServiceProvider serviceProvider, ILogger logger, CancellationToken cancellationToken)
    {
        if (serviceProvider.GetService<ISession>() is not { } session)
        {
            return 0;
        }

        var commands = serviceProvider.GetRequiredService<ITransactionCommandService>();
        var count = 0;

        foreach (var transactionId in await TransactionCreditSettlement.GetPendingAsync(session, 100, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var result = await commands.ReconcileCreditAsync(transactionId, cancellationToken);

            foreach (var warning in result.Warnings)
            {
                logger.LogWarning("Transaction '{TransactionId}': {Warning}", transactionId, warning);
            }

            count++;
        }

        return count;
    }

    // Marks each transaction that has just become overdue, saves that, and only then raises the event, so it is raised
    // once per transaction: a save that fails leaves the mark off and the next sweep tries again. Returns how many
    // became overdue.
    internal static async Task<int> RaiseOverdueAsync(
        IServiceProvider serviceProvider,
        ITransactionManager manager,
        IEnumerable<Transaction> candidates,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        IEnumerable<ITransactionEventHandler> eventHandlers = null;
        var overdue = 0;

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
            overdue++;
        }

        return overdue;
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
