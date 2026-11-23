using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.MailingLists;

/// <summary>
/// Provides the mailing lists permissions; administrators hold them all.
/// </summary>
internal sealed class MailingListsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        MailingListsPermissions.ManageMailingLists,
        MailingListsPermissions.ManageMailingListMembers,
        MailingListsPermissions.ViewMailingListMembers,
        MailingListsPermissions.ManageMailingListSettings,
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
                Name = global::OrchardCore.OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}
