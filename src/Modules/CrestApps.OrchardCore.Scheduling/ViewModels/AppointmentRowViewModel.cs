using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.ViewModels;

/// <summary>
/// An appointment in the list.
/// </summary>
public class AppointmentRowViewModel
{
    /// <summary>
    /// Gets or sets the appointment.
    /// </summary>
    public Appointment Appointment { get; set; }

    /// <summary>
    /// Gets or sets the appointment type's name.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the resources' names.
    /// </summary>
    public string ResourceNames { get; set; }

    /// <summary>
    /// Gets or sets when it starts, in the site's time zone.
    /// </summary>
    public DateTime LocalStart { get; set; }
}
