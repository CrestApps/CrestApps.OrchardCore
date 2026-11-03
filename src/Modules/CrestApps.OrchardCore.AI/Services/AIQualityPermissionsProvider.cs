using CrestApps.OrchardCore.AI.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Declares the AI quality permission, granted to administrators.
/// </summary>
internal sealed class AIQualityPermissionsProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ManageAIQuality,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
