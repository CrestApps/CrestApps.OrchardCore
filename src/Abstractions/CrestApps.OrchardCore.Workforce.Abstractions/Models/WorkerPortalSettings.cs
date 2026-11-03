namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's portal settings, kept in the worker's properties: which channels they hear on, and the secret of their
/// calendar feed.
/// </summary>
public sealed class WorkerPortalSettings
{
    /// <summary>
    /// Gets or sets the channels the worker turned off, by name.
    /// </summary>
    public IList<string> DisabledChannels { get; set; } = [];

    /// <summary>
    /// Gets or sets the kinds of notification the worker turned off.
    /// </summary>
    public IList<WorkforceNotificationKind> DisabledKinds { get; set; } = [];

    /// <summary>
    /// Gets or sets the secret in the worker's calendar feed address; empty until they ask for one.
    /// </summary>
    public string FeedToken { get; set; }

    /// <summary>
    /// Gets or sets how many minutes before a shift the worker is reminded of it, or zero for no reminder.
    /// </summary>
    public int ReminderMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets when the worker's quiet hours start, in their time zone, or <see langword="null"/> for none.
    /// During quiet hours only the My Work inbox gets notifications.
    /// </summary>
    public TimeOnly? QuietStart { get; set; }

    /// <summary>
    /// Gets or sets when the worker's quiet hours end; before the start means the next day.
    /// </summary>
    public TimeOnly? QuietEnd { get; set; }

    /// <summary>
    /// Tells whether a local time falls in the quiet hours. Pure.
    /// </summary>
    /// <param name="local">The worker's local time.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool IsQuiet(TimeOnly local)
    {
        if (QuietStart is not { } start || QuietEnd is not { } end || start == end)
        {
            return false;
        }

        return start < end ? local >= start && local < end : local >= start || local < end;
    }
}
