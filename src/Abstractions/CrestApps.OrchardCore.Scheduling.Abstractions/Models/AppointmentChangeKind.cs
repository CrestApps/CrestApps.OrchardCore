namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// What happened to an appointment.
/// </summary>
public enum AppointmentChangeKind
{
    /// <summary>
    /// The time was held.
    /// </summary>
    Held,

    /// <summary>
    /// The appointment was requested and waits for confirmation.
    /// </summary>
    Requested,

    /// <summary>
    /// The appointment was confirmed.
    /// </summary>
    Confirmed,

    /// <summary>
    /// The appointment moved to another time.
    /// </summary>
    Rescheduled,

    /// <summary>
    /// The appointment was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The appointment took place.
    /// </summary>
    Completed,

    /// <summary>
    /// The customer didn't come.
    /// </summary>
    NoShow,

    /// <summary>
    /// A hold ran out.
    /// </summary>
    HoldExpired,

    /// <summary>
    /// A cancellation freed a time a waitlisted customer wanted.
    /// </summary>
    WaitlistSlotAvailable,
}
