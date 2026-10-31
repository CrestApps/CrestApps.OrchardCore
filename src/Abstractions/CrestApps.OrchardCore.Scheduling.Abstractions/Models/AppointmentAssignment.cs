namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// How an appointment type picks its resources.
/// </summary>
public enum AppointmentAssignment
{
    /// <summary>
    /// Any one free resource, the first in the list.
    /// </summary>
    AnyAvailable,

    /// <summary>
    /// One free resource, the one with the fewest recent appointments, so work is shared out.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Every resource of the type at once, such as a doctor and a room.
    /// </summary>
    AllTogether,
}
