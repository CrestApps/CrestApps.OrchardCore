using CrestApps.OrchardCore.Users.Core.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// The required-field rules of a user's first, middle, last and display name, from the User Display Name settings. The
/// user editor and the AI tool apply the same rules; each words its own messages.
/// </summary>
internal static class UserFullNameRules
{
    /// <summary>
    /// Returns the name parts the settings require and that are missing, as property names of
    /// <see cref="UserFullNamePart"/>, in the order the editor shows the errors.
    /// </summary>
    /// <param name="settings">The User Display Name settings.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="firstName">The first name.</param>
    /// <param name="middleName">The middle name.</param>
    /// <param name="lastName">The last name.</param>
    /// <returns>The missing parts; empty when the names are valid.</returns>
    public static IReadOnlyList<string> GetMissingRequired(
        DisplayNameSettings settings,
        string displayName,
        string firstName,
        string middleName,
        string lastName)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var missing = new List<string>();

        if (settings.DisplayName == DisplayNamePropertyType.Required && string.IsNullOrWhiteSpace(displayName))
        {
            missing.Add(nameof(UserFullNamePart.DisplayName));
        }

        if (settings.FirstName == DisplayNamePropertyType.Required && string.IsNullOrWhiteSpace(firstName))
        {
            missing.Add(nameof(UserFullNamePart.FirstName));
        }

        if (settings.LastName == DisplayNamePropertyType.Required && string.IsNullOrWhiteSpace(lastName))
        {
            missing.Add(nameof(UserFullNamePart.LastName));
        }

        if (settings.MiddleName == DisplayNamePropertyType.Required && string.IsNullOrWhiteSpace(middleName))
        {
            missing.Add(nameof(UserFullNamePart.MiddleName));
        }

        return missing;
    }
}
