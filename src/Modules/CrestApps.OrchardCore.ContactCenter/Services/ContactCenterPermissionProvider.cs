using CrestApps.OrchardCore.ContactCenter.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides the baseline permissions exposed by the Contact Center feature.
/// </summary>
internal sealed class ContactCenterPermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        ContactCenterPermissions.ManageContactCenter,
        ContactCenterPermissions.ManageInteractions,
        ContactCenterPermissions.ViewInteractions,
        ContactCenterPermissions.ManageAgents,
        ContactCenterPermissions.ManageQueues,
        ContactCenterPermissions.ManageSkills,
        ContactCenterPermissions.ManageDialer,
        ContactCenterPermissions.SignIntoQueues,
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
                Name = "Agent",
                Permissions =
                [
                    ContactCenterPermissions.ViewInteractions,
                    ContactCenterPermissions.SignIntoQueues,
                ],
            },
        ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
