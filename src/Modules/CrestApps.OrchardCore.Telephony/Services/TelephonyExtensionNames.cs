namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// The name the phone shows for the person an extension rings.
/// </summary>
internal static class TelephonyExtensionNames
{
    /// <summary>
    /// Chooses the name to show for an extension.
    /// </summary>
    /// <remarks>
    /// A name typed on the extension itself wins: it was set for that extension on purpose ("Front desk"). The
    /// extension editor fills the field with the username when it is left empty, so a name equal to the username is not
    /// one. Then the user's display name, as the site shows users; then the username; then the bare extension.
    /// </remarks>
    /// <param name="extensionDisplayName">The display name stored on the extension, if any.</param>
    /// <param name="userName">The username of the user the extension rings.</param>
    /// <param name="userDisplayName">The user's display name, as the site's display name provider gives it.</param>
    /// <param name="number">The extension number.</param>
    /// <returns>The name to show; the extension number when nothing better is known.</returns>
    public static string Choose(string extensionDisplayName, string userName, string userDisplayName, string number)
    {
        var assigned = extensionDisplayName?.Trim();

        if (!string.IsNullOrEmpty(assigned) && !string.Equals(assigned, userName?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return assigned;
        }

        if (!string.IsNullOrWhiteSpace(userDisplayName))
        {
            return userDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName.Trim();
        }

        return number?.Trim() ?? string.Empty;
    }
}
