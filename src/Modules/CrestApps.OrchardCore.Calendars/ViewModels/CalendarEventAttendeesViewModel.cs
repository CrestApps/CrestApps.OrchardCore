using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The Attendees card of the event editor.
/// </summary>
public class CalendarEventAttendeesViewModel
{
    /// <summary>
    /// Gets or sets the users invited.
    /// </summary>
    public string[] UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets outside people invited, one per line as <c>Name &lt;email&gt;</c> or an email.
    /// </summary>
    public string Guests { get; set; }

    /// <summary>
    /// Gets or sets the rooms and equipment booked.
    /// </summary>
    public string[] ResourceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the invited users, as the user picker reads them.
    /// </summary>
    [BindNever]
    public string InitialUsersJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the rooms and equipment to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ResourceOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the attendees and their answers.
    /// </summary>
    [BindNever]
    public IList<CalendarAttendee> Attendees { get; set; } = [];
}
