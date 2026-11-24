using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Declares the permission to manage the messaging routing rules.
/// </summary>
internal sealed class MessagingRoutingRulesPermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        MessagingPermissions.ManageRoutingRules,
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
                // A supervisor decides who works which conversations.
                Name = "Supervisor",
                Permissions = _allPermissions,
            },
        ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
