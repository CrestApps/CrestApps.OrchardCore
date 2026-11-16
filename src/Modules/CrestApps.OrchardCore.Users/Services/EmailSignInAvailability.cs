using OrchardCore.Settings;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Whether the site offers signing in with an e-mailed code at all. A code is a local sign-in, so a site that turned
/// those off (Orchard Core's <see cref="LoginSettings.DisableLocalLogin"/>), to sign in only through an external
/// provider, has none: the code pages answer 404, and the login form shows no link to them. The pages and the link both
/// ask here, so the link never leads to a page that is not there.
/// </summary>
public static class EmailSignInAvailability
{
    /// <summary>
    /// Whether the code sign-in pages are there, and the login form links to them.
    /// </summary>
    /// <param name="siteService">The site's settings.</param>
    public static async Task<bool> IsAvailableAsync(ISiteService siteService)
    {
        ArgumentNullException.ThrowIfNull(siteService);

        return !(await siteService.GetSettingsAsync<LoginSettings>()).DisableLocalLogin;
    }
}
