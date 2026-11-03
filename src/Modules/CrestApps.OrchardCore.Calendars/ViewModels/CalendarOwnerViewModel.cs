using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The Owner card of the calendar editor.
/// </summary>
public class CalendarOwnerViewModel
{
    /// <summary>
    /// Gets or sets the owner: a user id or a role name.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets who the calendar belongs to.
    /// </summary>
    [BindNever]
    public CalendarOwnerKind OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the selected user, as the user picker reads it.
    /// </summary>
    [BindNever]
    public string InitialUserJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the roles to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> RoleOptions { get; set; } = [];
}
