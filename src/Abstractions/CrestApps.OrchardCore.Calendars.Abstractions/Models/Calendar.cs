using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// A calendar: a container of events with an owner, a type, a time zone and the people it is shared with.
/// Type-specific settings live in <see cref="ExtensibleEntity.Properties"/>.
/// </summary>
public sealed class Calendar : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the calendar's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the calendar is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the name of the calendar type, which says what the calendar holds and how its events behave.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the color the calendar's events are drawn in, as a CSS color such as <c>#1971c2</c>.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the IANA time zone new events use, or <see langword="null"/> for the site's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets who the calendar belongs to, copied from its type when it is created.
    /// </summary>
    public CalendarOwnerKind OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the owner: a user id for a user, a role name for a team, and empty otherwise.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets who can see the calendar besides its owner and the people it is shared with.
    /// </summary>
    public CalendarVisibility Visibility { get; set; }

    /// <summary>
    /// Gets or sets the users and roles the calendar is shared with, and what each can do.
    /// </summary>
    public IList<CalendarShare> Shares { get; set; } = [];

    /// <summary>
    /// Gets or sets how the calendar treats events that overlap.
    /// </summary>
    public CalendarConflictPolicy ConflictPolicy { get; set; }

    /// <summary>
    /// Gets or sets how many busy events may overlap when <see cref="ConflictPolicy"/> is
    /// <see cref="CalendarConflictPolicy.LimitConcurrent"/>.
    /// </summary>
    public int Capacity { get; set; } = 1;

    /// <summary>
    /// Gets or sets a counter raised whenever an event that counts toward the calendar's overlap limit changes. The calendar is
    /// saved with a concurrency check at the same time, so two requests can never both take the last place.
    /// </summary>
    public long BookingVersion { get; set; }

    /// <summary>
    /// Gets or sets whether the calendar is in use. A disabled calendar keeps its events but takes no new ones.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the module that manages the calendar, such as <c>Bookings</c>, or <see langword="null"/> when people manage it.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the record in <see cref="Source"/> the calendar belongs to.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the secret part of the calendar's private iCalendar feed address, or <see langword="null"/> when it has no feed.
    /// </summary>
    public string FeedToken { get; set; }

    /// <summary>
    /// Gets or sets when the calendar was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the calendar was last changed.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who created the calendar.
    /// </summary>
    public string CreatedBy { get; set; }
}
