using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.ViewModels;

/// <summary>
/// A booking held until the customer confirms it from the link e-mailed to them.
/// </summary>
public class BookingConfirmationViewModel
{
    /// <summary>Gets or sets the appointment type.</summary>
    public AppointmentType AppointmentType { get; set; }

    /// <summary>Gets or sets the held appointment.</summary>
    public Appointment Appointment { get; set; }

    /// <summary>Gets or sets the signed token from the link.</summary>
    public string Token { get; set; }
}
