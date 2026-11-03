using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.ViewModels;

/// <summary>
/// An appointment's page, for staff or for the customer's manage link.
/// </summary>
public class AppointmentDetailViewModel
{
    /// <summary>Gets or sets the appointment.</summary>
    public Appointment Appointment { get; set; }

    /// <summary>Gets or sets the appointment type.</summary>
    public AppointmentType AppointmentType { get; set; }

    /// <summary>Gets or sets the resources' names.</summary>
    public string ResourceNames { get; set; }

    /// <summary>Gets or sets the time zone the times are shown in.</summary>
    public string TimeZoneId { get; set; }

    /// <summary>Gets or sets when it starts, in that time zone.</summary>
    public DateTime LocalStart { get; set; }

    /// <summary>Gets or sets when it ends, in that time zone.</summary>
    public DateTime LocalEnd { get; set; }

    /// <summary>Gets or sets the open times to move it to, as UTC start and local label.</summary>
    public IList<KeyValuePair<string, string>> Slots { get; set; } = [];

    /// <summary>Gets or sets the day shown for moving, as yyyy-MM-dd.</summary>
    public string Date { get; set; }

    /// <summary>Gets or sets the customer's manage link token, on the public page.</summary>
    public string Token { get; set; }

    /// <summary>Gets or sets the manage link, on the staff page.</summary>
    public string ManageUrl { get; set; }

    /// <summary>Gets or sets whether the customer can still cancel it themselves, on the public page.</summary>
    public bool CustomerCanCancel { get; set; } = true;

    /// <summary>Gets or sets whether the customer can still move it themselves, on the public page.</summary>
    public bool CustomerCanReschedule { get; set; } = true;
}
