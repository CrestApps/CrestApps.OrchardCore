using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Seo.Services;

/// <summary>
/// Provides the permissions of the Advanced SEO feature.
/// </summary>
public sealed class AdvancedSeoPermissionsProvider : IPermissionProvider
{
    /// <summary>
    /// Lets a user change the advanced SEO settings.
    /// </summary>
    public static readonly Permission ManageAdvancedSeoSettings = new("ManageAdvancedSeoSettings", LocalizationSource.Create<AdvancedSeoPermissionsProvider>("Manage the advanced SEO settings"));

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageAdvancedSeoSettings,
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
