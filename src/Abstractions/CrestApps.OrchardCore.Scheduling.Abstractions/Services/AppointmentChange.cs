using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// A committed change to an appointment.
/// </summary>
public sealed class AppointmentChange
{
    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public AppointmentChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the appointment.
    /// </summary>
    public Appointment Appointment { get; set; }

    /// <summary>
    /// Gets or sets its type.
    /// </summary>
    public AppointmentType AppointmentType { get; set; }

    /// <summary>
    /// Gets or sets when it started before a reschedule.
    /// </summary>
    public DateTime? PreviousStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the waitlist entry, for a freed time.
    /// </summary>
    public WaitlistEntry WaitlistEntry { get; set; }

    /// <summary>
    /// Gets or sets who made the change.
    /// </summary>
    public string Actor { get; set; }
}
