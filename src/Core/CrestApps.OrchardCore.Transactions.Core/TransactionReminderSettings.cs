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

    /// <summary>
    /// Gets or sets how many days before a payment falls due the owner is told it is coming due. One such
    /// reminder is sent per transaction. Defaults to 3. A value of 0 or less turns these reminders off.
    /// </summary>
    public int UpcomingReminderDays { get; set; } = 3;

    /// <summary>
    /// Gets or sets the staged reminder policy: the days, relative to the due date, a reminder is sent on, in order,
    /// such as -3, 0, 7, 14 and 30 (three days before, on the day, then a week, two weeks and a month after). A step
    /// before the due date tells the owner it is coming due; the others remind them it is owed. When it is empty, the
    /// default, the first reminder delay, interval and maximum above are used instead.
    /// </summary>
    public int[] Steps { get; set; } = [];

    /// <summary>
    /// Gets or sets the first hour of the day (0 to 23, in the site's time zone) reminders are sent. Defaults to 8.
    /// </summary>
    public int SendFromHour { get; set; } = 8;

    /// <summary>
    /// Gets or sets the hour of the day (1 to 24, in the site's time zone) reminders stop being sent. Defaults to 20.
    /// With <see cref="SendFromHour"/> 0 and this 24, reminders are sent at any time.
    /// </summary>
    public int SendUntilHour { get; set; } = 20;

    /// <summary>
    /// Gets or sets how many days after a declined charge of a saved payment method each retry is made, in order.
    /// Defaults to one, three and five days. After the last retry is declined, the owner is asked to pay.
    /// </summary>
    public int[] ChargeRetryDays { get; set; } = [1, 3, 5];

    /// <summary>
    /// Gets the delay before the retry that follows <paramref name="declinedSoFar"/> declined charges, or
    /// <see langword="null"/> when no retry is left.
    /// </summary>
    /// <param name="declinedSoFar">How many charges were declined so far.</param>
    public TimeSpan? GetChargeRetryDelay(int declinedSoFar)
    {
        var retries = ChargeRetryDays ?? [];

        if (declinedSoFar < 1 || declinedSoFar > retries.Length)
        {
            return null;
        }

        return TimeSpan.FromDays(Math.Max(1, retries[declinedSoFar - 1]));
    }

    /// <summary>
    /// Gets a value indicating whether reminders may be sent at an hour of the site's day.
    /// </summary>
    /// <param name="localHour">The hour of the day in the site's time zone, 0 to 23.</param>
    public bool IsSendingHour(int localHour)
    {
        var from = Math.Clamp(SendFromHour, 0, 23);
        var until = Math.Clamp(SendUntilHour, 1, 24);

        return from < until
            ? localHour >= from && localHour < until
            : localHour >= from || localHour < until;
    }
}
