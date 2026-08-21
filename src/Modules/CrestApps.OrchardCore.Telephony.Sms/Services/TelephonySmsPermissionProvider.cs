using CrestApps.OrchardCore.Telephony.Sms.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Telephony.Sms.Services;

/// <summary>
/// Provides the permissions exposed by the SMS Communication Portal.
/// </summary>
internal sealed class TelephonySmsPermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        TelephonySmsPermissions.ManageSmsNumberRoutes,
        TelephonySmsPermissions.UseSmsPortal,
        TelephonySmsPermissions.SendGroupSms,
        TelephonySmsPermissions.ViewAllConversations,
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
                    TelephonySmsPermissions.UseSmsPortal,
                ],
            },
            new PermissionStereotype
            {
                Name = "Supervisor",
                Permissions =
                [
                    TelephonySmsPermissions.UseSmsPortal,
                    TelephonySmsPermissions.SendGroupSms,
                    TelephonySmsPermissions.ViewAllConversations,
                    TelephonySmsPermissions.ManageSmsNumberRoutes,
                ],
            },
        ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
