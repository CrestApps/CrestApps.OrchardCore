using CrestApps.OrchardCore.Calendars.Models;

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

    /// <summary>
    /// Gets or sets whether changes made here are written back to the external calendar. Only providers that implement
    /// <see cref="IWritableCalendarProvider"/> can; with others the calendar is a read-only copy.
    /// </summary>
    public bool TwoWay { get; set; }

    /// <summary>
    /// Gets or sets which copy wins when an event changed on both sides since it was last synchronized.
    /// </summary>
    public CalendarSyncConflictPolicy ConflictPolicy { get; set; }

    /// <summary>
    /// Gets or sets the token to read the next changes from, for an <see cref="IIncrementalCalendarProvider"/>.
    /// </summary>
    public string SyncToken { get; set; }

    /// <summary>
    /// Gets or sets whether local changes wait to be written back, so the next run comes early.
    /// </summary>
    public bool PendingPush { get; set; }

    /// <summary>
    /// Gets or sets the external copies of events deleted here, still to be deleted in the external calendar.
    /// </summary>
    public IList<CalendarProviderMapping> PendingDeletes { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the external account refused access. Synchronization is paused until the subscription is saved
    /// again, usually after reconnecting the account.
    /// </summary>
    public bool NeedsReconnect { get; set; }
}
