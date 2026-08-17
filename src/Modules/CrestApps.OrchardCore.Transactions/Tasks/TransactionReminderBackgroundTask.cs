using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Core;
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
/// configured in <see cref="TransactionReminderSettings"/>.
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
    /// Sweeps outstanding transactions and sends due reminders.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var siteService = serviceProvider.GetRequiredService<ISiteService>();
        var settings = await siteService.GetSettingsAsync<TransactionReminderSettings>();

        if (!settings.Enabled)
        {
            return;
        }

        var clock = serviceProvider.GetRequiredService<IClock>();
        var manager = serviceProvider.GetRequiredService<ITransactionManager>();
        var reminderService = serviceProvider.GetRequiredService<ITransactionReminderService>();
        var logger = serviceProvider.GetRequiredService<ILogger<TransactionReminderBackgroundTask>>();

        var utcNow = clock.UtcNow;
        var candidates = await manager.GetOutstandingDueAsync(utcNow, cancellationToken);

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
    }

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
