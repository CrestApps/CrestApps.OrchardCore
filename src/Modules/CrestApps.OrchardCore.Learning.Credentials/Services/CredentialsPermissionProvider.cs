using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// The permissions of Digital Credentials.
/// </summary>
internal sealed class CredentialsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        LearningCredentialsPermissions.ManageCredentialDefinitions,
        LearningCredentialsPermissions.AwardCredentials,
        LearningCredentialsPermissions.ViewCredentialAwards,
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
