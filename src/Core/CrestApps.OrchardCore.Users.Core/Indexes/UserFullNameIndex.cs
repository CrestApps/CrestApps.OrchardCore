using YesSql.Indexes;

namespace CrestApps.OrchardCore.Users.Core.Indexes;

/// <summary>
/// Represents the user full name index.
/// </summary>
public sealed class UserFullNameIndex : MapIndex
{
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

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; }
}
