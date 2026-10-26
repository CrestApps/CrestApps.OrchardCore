using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Commerce;

internal sealed class CommercePermissionProvider : IPermissionProvider
{
    // The content and media permissions of Orchard Core's Contents and Media features, by name: this module references
    // neither package, and a stereotype grants a permission by its name.
    private static readonly Permission _listContent = new("ListContent");
    private static readonly Permission _viewContent = new("ViewContent");
    private static readonly Permission _editContent = new("EditContent");
    private static readonly Permission _publishContent = new("PublishContent");
    private static readonly Permission _deleteContent = new("DeleteContent");
    private static readonly Permission _manageMedia = new("ManageMediaContent");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        CommercePermissions.SetUpStore,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

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
                Name = CommerceConstants.StaffRoles.StoreManager,
                Permissions =
                [
                    AdminPermissions.AccessAdminPanel,
                    _listContent,
                    _viewContent,
                    _editContent,
                    _publishContent,
                    _deleteContent,
                    _manageMedia,
                ],
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.Warehouse,
                Permissions = [AdminPermissions.AccessAdminPanel],
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.CustomerSupport,
                Permissions = [AdminPermissions.AccessAdminPanel],
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.Marketing,
                Permissions =
                [
                    AdminPermissions.AccessAdminPanel,
                    _listContent,
                    _viewContent,
                    _editContent,
                    _publishContent,
                    _manageMedia,
                ],
            },
        ];
}
