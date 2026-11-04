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
    /// Gets or sets whether the caller already holds the lock (<see cref="CalendarLockKeys.Calendar"/>) of every calendar
    /// whose overlap limit the event counts toward, so the overlap check runs under the caller's locks instead of taking
    /// them again. A module that books a calendar from its own availability, such as Scheduling, takes the calendar locks
    /// with its own and sets this, so its bookings and the calendar's own events can't overlap.
    /// </summary>
    public bool CalendarLocksHeld { get; set; }

    /// <summary>
    /// Gets or sets whether to tell the after-commit observers (workflows, invitations, synchronization) about the change.
    /// </summary>
    public bool Notify { get; set; } = true;

    /// <summary>
    /// Gets or sets whether attendees should be emailed about the change when invitations are on.
    /// </summary>
    public bool SendInvitations { get; set; } = true;
}
