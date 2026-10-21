using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Commerce;

public static class CommercePermissions
{
    /// <summary>
    /// Opens the store's setup guide and runs its steps.
    /// </summary>
    public static readonly Permission SetUpStore = new(
        "SetUpStore",
        LocalizationSource.Create("Set up the store", typeof(CommercePermissions)),
        isSecurityCritical: true);
}
