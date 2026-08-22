namespace CrestApps.OrchardCore.Transactions.Core;

/// <summary>
/// The site settings that control how outstanding-payment reminders are sent for the tenant. Reminders are
/// delivered through the notification system so each user receives them on their preferred channel.
/// </summary>
public sealed class TransactionReminderSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the scheduled reminder sweep is enabled. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of days to wait after a transaction becomes due before the first reminder is
    /// sent. Defaults to 0 (remind as soon as it is due).
    /// </summary>
    public int FirstReminderDelayDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to wait between reminders. Defaults to 7.
    /// </summary>
    public int ReminderIntervalDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the maximum number of reminders to send for a single transaction. Defaults to 3. A value
    /// of 0 or less means there is no limit.
    /// </summary>
    public int MaxReminders { get; set; } = 3;
}
