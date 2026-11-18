using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Decides which step of the staged reminder policy (<see cref="TransactionReminderSettings.Steps"/>) a transaction is
/// due, if any. Days are compared as the reminder sweep compares the due date: by the calendar day of the UTC times.
/// </summary>
public static class TransactionReminderSchedule
{
    /// <summary>
    /// Gets the steps of the policy, earliest first, without repeats.
    /// </summary>
    /// <param name="settings">The reminder settings.</param>
    public static int[] GetSteps(TransactionReminderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return [.. (settings.Steps ?? []).Distinct().Order()];
    }

    /// <summary>
    /// Gets a value indicating whether the staged policy is used instead of the first delay, interval and maximum.
    /// </summary>
    /// <param name="settings">The reminder settings.</param>
    public static bool IsStaged(TransactionReminderSettings settings)
        => settings?.Steps is { Length: > 0 };

    /// <summary>
    /// Gets the index of the step the transaction is due now: the latest step whose day has come and that was not sent.
    /// Earlier steps missed meanwhile are passed over, so an owner never gets several reminders at once. Returns
    /// <see langword="null"/> when no step is due, the transaction owes nothing, has no due date, or its reminders are
    /// paused.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="settings">The reminder settings.</param>
    /// <param name="utcNow">The current time.</param>
    public static int? GetDueStep(Transaction transaction, TransactionReminderSettings settings, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!IsStaged(settings) ||
            transaction.RemindersPaused ||
            transaction.OutstandingAmount <= 0m ||
            !TransactionStateMachine.IsCollectable(transaction.Status) ||
            transaction.DueUtc is not DateTime dueUtc)
        {
            return null;
        }

        var steps = GetSteps(settings);

        for (var index = steps.Length - 1; index >= transaction.ReminderStepsSent && index >= 0; index--)
        {
            if (dueUtc.Date.AddDays(steps[index]) <= utcNow.Date)
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets how many days before the due date the earliest step is, or zero when no step comes before it, so the sweep
    /// knows how far ahead to look for transactions coming due.
    /// </summary>
    /// <param name="settings">The reminder settings.</param>
    public static int GetLeadDays(TransactionReminderSettings settings)
    {
        var steps = GetSteps(settings);

        return steps.Length == 0 ? 0 : Math.Max(0, -steps[0]);
    }
}
