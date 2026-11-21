using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Xapi.Services;

/// <summary>
/// Declares the permissions of the Learning Record Store. A role that holds them is given to an OpenID application,
/// which is then an LRS client.
/// </summary>
internal sealed class XapiPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        LearningXapiPermissions.WriteLearningStatements,
        LearningXapiPermissions.ReadAllLearningStatements,
        LearningXapiPermissions.ReadOwnLearningStatements,
        LearningXapiPermissions.ManageLearningDocuments,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
