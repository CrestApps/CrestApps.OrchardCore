using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ServiceLevels;

/// <summary>
/// The permissions of the Service Levels building block.
/// </summary>
public static class ServiceLevelsPermissions
{
    /// <summary>
    /// Lets a user create, edit and delete service level policies.
    /// </summary>
    public static readonly Permission ManageServiceLevelPolicies = new(
        "ManageServiceLevelPolicies",
        LocalizationSource.Create("Manage service level policies", typeof(ServiceLevelsPermissions)));
}
