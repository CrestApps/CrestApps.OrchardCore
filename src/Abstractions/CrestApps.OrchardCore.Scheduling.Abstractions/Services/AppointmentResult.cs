using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// The outcome of a booking operation.
/// </summary>
public sealed class AppointmentResult
{
    /// <summary>
    /// Gets or sets the appointment.
    /// </summary>
    public Appointment Appointment { get; set; }

    /// <summary>
    /// Gets the errors, empty when it succeeded.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets whether it failed because the time is taken.
    /// </summary>
    public bool Conflict { get; set; }

    /// <summary>
    /// Gets whether it succeeded.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
