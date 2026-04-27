using CrestApps.OrchardCore.Roles.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Roles.ViewModels;

/// <summary>
/// Represents the view model for role picker.
/// </summary>
public class RolePickerViewModel
{
    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the roles.
    /// </summary>
    public string[] Roles { get; set; }

    /// <summary>
    /// Gets or sets the settings.
    /// </summary>
    [BindNever]
    public RolePickerPartSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets the available roles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AvailableRoles { get; set; }
}
