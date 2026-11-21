using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Classes.Services;

/// <summary>
/// Declares the permissions of Learning classes.
/// </summary>
internal sealed class ClassesPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        LearningClassesPermissions.ManageLearningClasses,
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
