using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Permission provider for the Phone Number Verifications module.
/// </summary>
internal sealed class PhoneNumberVerificationsPermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        PhoneNumberVerificationsPermissions.ManagePhoneNumberVerificationSettings,
        PhoneNumberVerificationsPermissions.VerifyPhoneNumbers,
        PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

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
                // A supervisor reads every report, this one included.
                Name = "Supervisor",
                Permissions =
                [
                    PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport,
                ],
            },
        ];
}
