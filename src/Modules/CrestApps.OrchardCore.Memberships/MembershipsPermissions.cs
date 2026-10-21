using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Memberships;

public static class MembershipsPermissions
{
    /// <summary>
    /// Sees members, and ends a membership early.
    /// </summary>
    public static readonly Permission ManageMemberships = new(
        "ManageMemberships",
        LocalizationSource.Create("Manage memberships", typeof(MembershipsPermissions)));
}
