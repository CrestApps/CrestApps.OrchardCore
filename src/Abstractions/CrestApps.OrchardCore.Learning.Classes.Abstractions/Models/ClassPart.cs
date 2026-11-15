using CrestApps.OrchardCore.Scheduling.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Classes.Models;

/// <summary>
/// A class in a course outline: "attend a class". Its dates are dated sessions in Scheduling with this item as their
/// source, and a learner registers in one of them.
/// </summary>
public sealed class ClassPart : ContentPart
{
    /// <summary>
    /// Gets or sets the Scheduling appointment type the class's sessions use.
    /// </summary>
    public string AppointmentTypeId { get; set; }

    /// <summary>
    /// Gets or sets the Scheduling resources (instructors, rooms) new sessions use.
    /// </summary>
    public List<string> ResourceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how many learners a new session takes.
    /// </summary>
    public int Capacity { get; set; } = 10;

    /// <summary>
    /// Gets or sets where new sessions take place.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the meeting link new sessions use.
    /// </summary>
    public string MeetingUrl { get; set; }

    /// <summary>
    /// Gets or sets what happens when a place frees up in a full session.
    /// </summary>
    public SchedulingWaitlistMode WaitlistMode { get; set; } = SchedulingWaitlistMode.AutoPromote;

    /// <summary>
    /// Gets or sets the minutes a partial attendance needs to count as attended; empty means partial attendance never
    /// completes the class.
    /// </summary>
    public int? MinimumMinutes { get; set; }
}
