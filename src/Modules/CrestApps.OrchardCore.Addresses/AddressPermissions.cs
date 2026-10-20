using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Addresses;

public static class AddressPermissions
{
    /// <summary>
    /// Sets up the service address suggestions come from, such as its API key.
    /// </summary>
    public static readonly Permission ManageAddressSuggestions = new("ManageAddressSuggestions", "Manage address suggestions");
}

internal sealed class AddressPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        AddressPermissions.ManageAddressSuggestions,
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
