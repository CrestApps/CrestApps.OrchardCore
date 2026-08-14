using CrestApps.OrchardCore.Users.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Core.ViewModels;

/// <summary>
/// Represents the view model for user full name part.
/// </summary>
public class UserFullNamePartViewModel
{
    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// Gets or sets the middle name.
    /// </summary>
    public string MiddleName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user.
    /// </summary>
    [BindNever]
    public User User { get; set; }

    /// <summary>
    /// Gets or sets the settings.
    /// </summary>
    [BindNever]
    public DisplayNameSettings Settings { get; set; }
}
