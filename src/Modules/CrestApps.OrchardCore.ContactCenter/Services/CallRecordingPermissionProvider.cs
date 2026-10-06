using CrestApps.OrchardCore.ContactCenter.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides the call recording permissions, which exist only while the Call Recording feature is enabled.
/// </summary>
internal sealed class CallRecordingPermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        ContactCenterPermissions.ListenToOwnCallRecordings,
        ContactCenterPermissions.ListenToAllCallRecordings,
        ContactCenterPermissions.DeleteCallRecordings,
    ];

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
            new PermissionStereotype
            {
                // An agent hears their own calls back, to review them; nobody else's.
                Name = "Agent",
                Permissions =
                [
                    ContactCenterPermissions.ListenToOwnCallRecordings,
                ],
            },
            new PermissionStereotype
            {
                Name = "Supervisor",
                Permissions =
                [
                    ContactCenterPermissions.ListenToAllCallRecordings,
                ],
            },
        ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
