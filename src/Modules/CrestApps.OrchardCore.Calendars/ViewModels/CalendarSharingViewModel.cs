using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The Sharing card of the calendar editor: the users and roles at each access level.
/// </summary>
public class CalendarSharingViewModel
{
    /// <summary>
    /// Gets or sets the users who see only when the calendar is busy.
    /// </summary>
    public string[] FreeBusyUsers { get; set; } = [];

    /// <summary>
    /// Gets or sets the users who see the events.
    /// </summary>
    public string[] ReadUsers { get; set; } = [];

    /// <summary>
    /// Gets or sets the users who add and change events.
    /// </summary>
    public string[] WriteUsers { get; set; } = [];

    /// <summary>
    /// Gets or sets the users who manage the calendar.
    /// </summary>
    public string[] ManageUsers { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles that see only when the calendar is busy.
    /// </summary>
    public string[] FreeBusyRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles that see the events.
    /// </summary>
    public string[] ReadRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles that add and change events.
    /// </summary>
    public string[] WriteRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles that manage the calendar.
    /// </summary>
    public string[] ManageRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected users per level, as the user pickers read them.
    /// </summary>
    [BindNever]
    public IDictionary<string, string> InitialUsersJson { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the roles to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> RoleOptions { get; set; } = [];
}
