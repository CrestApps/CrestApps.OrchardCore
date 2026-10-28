using OrchardCore;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContentTransfer;

public sealed class PermissionsProvider : IPermissionProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ContentTransferPermissions.ListContentTransferEntries,
        ContentTransferPermissions.DeleteContentTransferEntries,
        ContentTransferPermissions.ImportContentFromFile,
        ContentTransferPermissions.ExportContentFromFile,
    ];

    public PermissionsProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_allPermissions);

        // The cached definitions: every breadcrumb resolves permissions through here, and the Load variant read the
        // content definition document from the database on each one.
        foreach (var contentTypeDefinition in await _contentDefinitionManager.ListTypeDefinitionsAsync())
        {
            permissions.Add(ContentTransferPermissions.CreateDynamicPermission(ContentTransferPermissions.ImportContentFromFileOfType, contentTypeDefinition.Name));
            permissions.Add(ContentTransferPermissions.CreateDynamicPermission(ContentTransferPermissions.ExportContentFromFileOfType, contentTypeDefinition.Name));
        }

        return permissions;
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return new[]
        {
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        };
    }
}
