using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills;

internal sealed class SkillsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        SkillsPermissions.ManageSkills,
        SkillsPermissions.AssignSkills,
        SkillsPermissions.ViewSkills,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
    }
}
