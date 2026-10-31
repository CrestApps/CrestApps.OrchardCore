namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// A calendar's connection to an external calendar, kept in the calendar's properties.
/// </summary>
public sealed class CalendarSubscription
{
    /// <summary>
    /// Gets or sets the name of the provider, such as <c>ics</c>.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the address of the external calendar.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets how often to synchronize, in minutes.
    /// </summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether synchronization is on.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when the calendar was last synchronized successfully.
    /// </summary>
    public DateTime? LastSyncedUtc { get; set; }

    /// <summary>
    /// Gets or sets when synchronization was last tried.
    /// </summary>
    public DateTime? LastAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets why the last synchronization failed, or <see langword="null"/> when it succeeded.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets how many synchronizations failed in a row, used to wait longer between retries.
    /// </summary>
    public int Failures { get; set; }

    /// <summary>
    /// Gets or sets what the last synchronization did, for display.
    /// </summary>
    public string LastResult { get; set; }
}
