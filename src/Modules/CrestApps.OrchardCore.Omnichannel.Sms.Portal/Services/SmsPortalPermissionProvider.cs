using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;

/// <summary>
/// Provides the permissions exposed by the SMS Communication Portal.
/// </summary>
internal sealed class SmsPortalPermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        SmsPortalPermissions.ManageSmsNumberRoutes,
        SmsPortalPermissions.UseSmsPortal,
        SmsPortalPermissions.SendDuringQuietHours,
        SmsPortalPermissions.SendGroupSms,
        SmsPortalPermissions.ViewAllConversations,
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
                    SmsPortalPermissions.UseSmsPortal,
                ],
            },
            new PermissionStereotype
            {
                Name = "Supervisor",
                Permissions =
                [
                    SmsPortalPermissions.UseSmsPortal,
                    SmsPortalPermissions.SendGroupSms,
                    SmsPortalPermissions.ViewAllConversations,
                    SmsPortalPermissions.ManageSmsNumberRoutes,
                ],
            },
        ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
