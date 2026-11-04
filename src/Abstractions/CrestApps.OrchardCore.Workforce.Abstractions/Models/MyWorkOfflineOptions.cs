namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// The My Work pages that keep working offline. The portal's service worker keeps the last copy of each, with the
/// scripts, styles, fonts and images it loaded, and shows that copy when there is no connection.
/// </summary>
public sealed class MyWorkOfflineOptions
{
    /// <summary>
    /// Gets the pages' paths under the site, such as <c>~/Admin/my-work/field</c>.
    /// </summary>
    public List<string> Pages { get; } = [];
}
