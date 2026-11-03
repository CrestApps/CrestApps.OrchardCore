using YesSql.Indexes;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// Finds a user's reminders that are still shown.
/// </summary>
public sealed class CalendarUserReminderIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the reminder's id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the user to remind.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets whether the user dismissed it.
    /// </summary>
    public bool Dismissed { get; set; }

    /// <summary>
    /// Gets or sets when the reminder was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
