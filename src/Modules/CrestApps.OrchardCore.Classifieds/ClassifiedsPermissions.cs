using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Classifieds;

public static class ClassifiedsPermissions
{
    /// <summary>
    /// Posts listings from the site and manages one's own.
    /// </summary>
    public static readonly Permission PostClassifieds = new(
        "PostClassifieds",
        LocalizationSource.Create("Post listings", typeof(ClassifiedsPermissions)));

    /// <summary>
    /// Approves and rejects posted listings.
    /// </summary>
    public static readonly Permission ModerateClassifieds = new(
        "ModerateClassifieds",
        LocalizationSource.Create("Moderate posted listings", typeof(ClassifiedsPermissions)));
}
