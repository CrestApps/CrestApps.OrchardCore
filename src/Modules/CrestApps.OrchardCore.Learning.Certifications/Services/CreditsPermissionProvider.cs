using CrestApps.OrchardCore.Learning.Certifications.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Declares the permissions of education credits.
/// </summary>
internal sealed class CreditsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        LearningCertificationsPermissions.ManageEducationCredits,
        LearningCertificationsPermissions.ReviewExternalLearning,
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
