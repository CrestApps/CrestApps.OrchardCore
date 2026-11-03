using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// The editor of an appointment workflow event.
/// </summary>
public class AppointmentEventActivityViewModel
{
    /// <summary>
    /// Gets or sets the appointment types that start the workflow.
    /// </summary>
    public string[] AppointmentTypeIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how many minutes before the start, for the Starting Soon event.
    /// </summary>
    public int MinutesBefore { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether the event has a minutes-before setting.
    /// </summary>
    [BindNever]
    public bool HasMinutesBefore { get; set; }

    /// <summary>
    /// Gets or sets the appointment types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];
}
