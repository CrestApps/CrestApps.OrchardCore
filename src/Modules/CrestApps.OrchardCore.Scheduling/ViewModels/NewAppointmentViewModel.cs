using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Scheduling.ViewModels;

/// <summary>
/// The page that books an appointment, used by staff and on the public booking page.
/// </summary>
public class NewAppointmentViewModel
{
    /// <summary>Gets or sets the appointment type.</summary>
    public string AppointmentTypeId { get; set; }

    /// <summary>Gets or sets the day, as yyyy-MM-dd.</summary>
    public string Date { get; set; }

    /// <summary>Gets or sets the chosen start, as an ISO 8601 UTC time.</summary>
    public string Start { get; set; }

    /// <summary>Gets or sets the customer's name.</summary>
    public string CustomerName { get; set; }

    /// <summary>Gets or sets the customer's email.</summary>
    public string CustomerEmail { get; set; }

    /// <summary>Gets or sets the customer's phone number.</summary>
    public string CustomerPhone { get; set; }

    /// <summary>Gets or sets notes.</summary>
    public string Notes { get; set; }

    /// <summary>Gets or sets the appointment type.</summary>
    [BindNever]
    public AppointmentType AppointmentType { get; set; }

    /// <summary>Gets or sets the time zone the times are shown in.</summary>
    [BindNever]
    public string TimeZoneId { get; set; }

    /// <summary>Gets or sets the open times on the day, as UTC start and local label.</summary>
    [BindNever]
    public IList<KeyValuePair<string, string>> Slots { get; set; } = [];

    /// <summary>Gets or sets the appointment types to choose from.</summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];

    /// <summary>Gets or sets the errors.</summary>
    [BindNever]
    public IList<string> Errors { get; set; } = [];
}
