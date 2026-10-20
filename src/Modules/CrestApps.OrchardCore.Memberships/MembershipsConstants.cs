using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Memberships;

public static class MembershipsConstants
{
    public const string Feature = "CrestApps.OrchardCore.Memberships";

    /// <summary>
    /// The YesSql collection that holds memberships.
    /// </summary>
    public const string CollectionName = "Memberships";
}

public static class MembershipsPermissions
{
    /// <summary>
    /// Sees members, and ends a membership early.
    /// </summary>
    public static readonly Permission ManageMemberships = new("ManageMemberships", "Manage memberships");
}

internal sealed class MembershipsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        MembershipsPermissions.ManageMemberships,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}
