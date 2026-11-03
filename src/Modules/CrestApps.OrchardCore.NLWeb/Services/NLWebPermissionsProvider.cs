using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Provides the permissions of the NLWeb module.
/// </summary>
public sealed class NLWebPermissionsProvider : IPermissionProvider
{
    /// <summary>
    /// Lets a caller ask the site questions through the NLWeb endpoints and the ask tool. Answers only include the
    /// content items the caller may view.
    /// </summary>
    public static readonly Permission QueryNLWeb = new("QueryNLWeb", LocalizationSource.Create<NLWebPermissionsProvider>("Ask the site questions with NLWeb"));

    /// <summary>
    /// Lets a user change the NLWeb settings.
    /// </summary>
    public static readonly Permission ManageNLWebSettings = new("ManageNLWebSettings", LocalizationSource.Create<NLWebPermissionsProvider>("Manage the NLWeb settings"));

    private readonly IEnumerable<Permission> _allPermissions =
    [
        QueryNLWeb,
        ManageNLWebSettings,
    ];

    /// <summary>
    /// Gets the permissions of the module.
    /// </summary>
    /// <returns>The permissions.</returns>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <summary>
    /// Gets the roles that hold the permissions by default. Anyone can ask questions, as anyone can use the site's
    /// search.
    /// </summary>
    /// <returns>The default stereotypes.</returns>
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
            Permissions = [QueryNLWeb],
        },
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Authenticated,
            Permissions = [QueryNLWeb],
        },
    ];
}
