using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Users.Core.Models;

/// <summary>
/// Represents the user full name part.
/// </summary>
public sealed class UserFullNamePart : ContentPart
{
    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the middle name.
    /// </summary>
    public string MiddleName { get; set; }
}
