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

    /// <summary>
    /// Gets or sets how many days before a payment falls due the owner is told it is coming due.
    /// </summary>
    public int UpcomingReminderDays { get; set; }

    /// <summary>
    /// Gets or sets the staged reminder steps, in days relative to the due date, separated by commas.
    /// </summary>
    public string Steps { get; set; }

    /// <summary>
    /// Gets or sets the first hour of the day reminders are sent.
    /// </summary>
    public int SendFromHour { get; set; }

    /// <summary>
    /// Gets or sets the hour of the day reminders stop being sent.
    /// </summary>
    public int SendUntilHour { get; set; }

    /// <summary>
    /// Gets or sets the days after a declined charge of a saved card each retry is made, separated by commas.
    /// </summary>
    public string ChargeRetryDays { get; set; }
}
