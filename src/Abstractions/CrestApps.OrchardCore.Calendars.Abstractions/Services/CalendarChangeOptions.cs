namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// How a change to a calendar or an event is made.
/// </summary>
public sealed class CalendarChangeOptions
{
    /// <summary>
    /// Gets or sets who makes the change: a user id, <c>workflow</c>, or a module's name.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets why the change is made, such as a cancellation reason.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets whether to skip the calendar's overlap limit, for a module that checks availability itself under its own lock.
    /// </summary>
    public bool SkipConflictCheck { get; set; }

    /// <summary>
    /// Gets or sets whether to tell the after-commit observers (workflows, invitations, synchronization) about the change.
    /// </summary>
    public bool Notify { get; set; } = true;

    /// <summary>
    /// Gets or sets whether attendees should be emailed about the change when invitations are on.
    /// </summary>
    public bool SendInvitations { get; set; } = true;
}
