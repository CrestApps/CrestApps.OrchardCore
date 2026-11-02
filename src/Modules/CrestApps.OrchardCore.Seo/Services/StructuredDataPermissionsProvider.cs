using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Seo.Services;

/// <summary>
/// Provides the permissions of the structured data feature.
/// </summary>
public sealed class StructuredDataPermissionsProvider : IPermissionProvider
{
    /// <summary>
    /// Lets a user change the structured data settings.
    /// </summary>
    public static readonly Permission ManageStructuredDataSettings = new("ManageStructuredDataSettings", LocalizationSource.Create<StructuredDataPermissionsProvider>("Manage the structured data settings"));

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageStructuredDataSettings,
    ];

    /// <summary>
    /// Gets the permissions of the feature.
    /// </summary>
    /// <returns>The permissions.</returns>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <summary>
    /// Gets the roles that hold the permissions by default.
    /// </summary>
    /// <returns>The default stereotypes.</returns>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
