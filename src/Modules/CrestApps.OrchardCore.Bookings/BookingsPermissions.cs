using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Bookings;

public static class BookingsPermissions
{
    /// <summary>
    /// Sees and changes bookings and their dates: confirms, completes and cancels them, and charges balances.
    /// </summary>
    public static readonly Permission ManageBookings = new("ManageBookings", "Manage bookings");
}

internal sealed class BookingsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        BookingsPermissions.ManageBookings,
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
