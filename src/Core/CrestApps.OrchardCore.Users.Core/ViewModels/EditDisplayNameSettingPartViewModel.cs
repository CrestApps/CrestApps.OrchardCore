using CrestApps.OrchardCore.Users.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Users.Core.ViewModels;

/// <summary>
/// Represents the view model for edit display name setting part.
/// </summary>
public class EditDisplayNameSettingPartViewModel
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public DisplayNameType Type { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public DisplayNamePropertyType DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public DisplayNamePropertyType FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    public DisplayNamePropertyType LastName { get; set; }

    /// <summary>
    /// Gets or sets the middle name.
    /// </summary>
    public DisplayNamePropertyType MiddleName { get; set; }

    /// <summary>
    /// Gets or sets the template.
    /// </summary>
    public string Template { get; set; }

    /// <summary>
    /// Gets or sets the types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Types { get; set; }

    /// <summary>
    /// Gets or sets the property types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> PropertyTypes { get; set; }
}
