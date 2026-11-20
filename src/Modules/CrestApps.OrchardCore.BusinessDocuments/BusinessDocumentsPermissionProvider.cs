using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.BusinessDocuments;

internal sealed class BusinessDocumentsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        BusinessDocumentsPermissions.ManageBusinessDocumentTemplates,
        BusinessDocumentsPermissions.ManageNumberSeries,
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
        ];
}
