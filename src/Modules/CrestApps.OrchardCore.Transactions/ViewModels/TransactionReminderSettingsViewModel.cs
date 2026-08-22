namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The editor view model for the transaction reminder settings.
/// </summary>
public class TransactionReminderSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the scheduled reminder sweep is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the number of days to wait after a transaction becomes due before the first reminder.
    /// </summary>
    public int FirstReminderDelayDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to wait between reminders.
    /// </summary>
    public int ReminderIntervalDays { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of reminders to send for a single transaction.
    /// </summary>
    public int MaxReminders { get; set; }
}
