namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// The state of an appointment.
/// </summary>
public enum AppointmentStatus
{
    /// <summary>
    /// The time is held for a short while, for example while the customer finishes booking.
    /// </summary>
    Held,

    /// <summary>
    /// The customer asked for the time; someone has to confirm it.
    /// </summary>
    Requested,

    /// <summary>
    /// The appointment is booked.
    /// </summary>
    Confirmed,

    /// <summary>
    /// The appointment took place.
    /// </summary>
    Completed,

    /// <summary>
    /// The customer didn't come.
    /// </summary>
    NoShow,

    /// <summary>
    /// The appointment was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The hold ran out before the booking was finished.
    /// </summary>
    Expired,
}
