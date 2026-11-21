using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.AI;

/// <summary>
/// Declares the permissions of the authoring assistant and the scoring help.
/// </summary>
internal sealed class LearningAIPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        LearningAIPermissions.UseAuthoringAssistant,
        LearningAIPermissions.UseScoringHelp,
    ];

    /// <inheritdoc />
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc />
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
