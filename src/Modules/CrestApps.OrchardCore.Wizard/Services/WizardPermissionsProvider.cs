using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Wizard.Services;

/// <summary>
/// Provides the permissions and default role stereotypes for the wizard module.
/// </summary>
public sealed class WizardPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WizardPermissions.StartWizard,
        WizardPermissions.ContinueWizard,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Anonymous,
            Permissions = _allPermissions,
        },
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Authenticated,
            Permissions = _allPermissions,
        },
    ];
}
