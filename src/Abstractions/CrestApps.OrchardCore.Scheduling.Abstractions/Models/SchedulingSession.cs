using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// A dated session of an appointment type, such as a class: one or more meetings, its own capacity, a location or a
/// meeting link, and a registration window. It exists before anyone registers, and one registration covers all of its
/// meetings.
/// </summary>
public sealed class SchedulingSession : CatalogItem
{
    /// <summary>
    /// Gets or sets the appointment type whose rules (confirmation, cancel notice) the registrations follow.
    /// </summary>
    public string AppointmentTypeId { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the meetings, in order.
    /// </summary>
    public List<SchedulingSessionMeeting> Meetings { get; set; } = [];

    /// <summary>
    /// Gets or sets how many people may register.
    /// </summary>
    public int Capacity { get; set; } = 1;

    /// <summary>
    /// Gets or sets where it takes place.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets a meeting link pasted by the organizer.
    /// </summary>
    public string MeetingUrl { get; set; }

    /// <summary>
    /// Gets or sets the resources it uses, such as instructors and rooms. The first one's calendar holds its event.
    /// </summary>
    public List<string> ResourceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when registration opens; empty for at once.
    /// </summary>
    public DateTime? RegistrationOpensUtc { get; set; }

    /// <summary>
    /// Gets or sets when registration closes; empty for when the first meeting starts.
    /// </summary>
    public DateTime? RegistrationClosesUtc { get; set; }

    /// <summary>
    /// Gets or sets what happens when a place frees up.
    /// </summary>
    public SchedulingWaitlistMode WaitlistMode { get; set; } = SchedulingWaitlistMode.AutoPromote;

    /// <summary>
    /// Gets or sets how many hours an offered place is held before it passes to the next person.
    /// </summary>
    public int OfferHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets whether it was cancelled.
    /// </summary>
    public SchedulingSessionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets what made it, such as a course's class.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the id within <see cref="Source"/>.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the calendar event that holds the session and its attendees.
    /// </summary>
    public string CalendarEventId { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets when the first meeting starts.
    /// </summary>
    public DateTime StartUtc => Meetings.Count == 0 ? default : Meetings.Min(meeting => meeting.StartUtc);

    /// <summary>
    /// Gets when the last meeting ends.
    /// </summary>
    public DateTime EndUtc => Meetings.Count == 0 ? default : Meetings.Max(meeting => meeting.EndUtc);

    /// <summary>
    /// Gets the minutes of all its meetings.
    /// </summary>
    public int TotalMinutes => (int)Meetings.Sum(meeting => (meeting.EndUtc - meeting.StartUtc).TotalMinutes);
}

/// <summary>
/// One meeting of a session.
/// </summary>
public sealed class SchedulingSessionMeeting
{
    /// <summary>
    /// Gets or sets when it starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends, in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; }
}

/// <summary>
/// The states of a session.
/// </summary>
public enum SchedulingSessionStatus
{
    /// <summary>
    /// It takes place.
    /// </summary>
    Scheduled,

    /// <summary>
    /// It was cancelled.
    /// </summary>
    Cancelled,
}

/// <summary>
/// What a session does when a place frees up.
/// </summary>
public enum SchedulingWaitlistMode
{
    /// <summary>
    /// Nobody waits: a full session refuses.
    /// </summary>
    None,

    /// <summary>
    /// The next person is told a place is free and registers themselves.
    /// </summary>
    Notify,

    /// <summary>
    /// The next person is offered the place, held for them until the offer runs out, when it passes to the next.
    /// </summary>
    AutoPromote,
}
