using CrestApps.OrchardCore.Learning.Programs.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Declares the permissions of required learning.
/// </summary>
internal sealed class ProgramsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        LearningProgramsPermissions.ManageRequiredLearning,
        LearningProgramsPermissions.ExemptFromRequiredLearning,
        LearningProgramsPermissions.ViewTeamLearning,
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
