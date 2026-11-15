using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.KnowledgeBase;

/// <summary>
/// The Knowledge Base's permissions. Writing articles uses the content permissions of the Knowledge Article type.
/// </summary>
public static class KnowledgeBasePermissions
{
    /// <summary>
    /// Reads the articles meant for staff only.
    /// </summary>
    public static readonly Permission ViewStaffArticles = new("ViewStaffKnowledgeArticles", LocalizationSource.Create("View staff-only knowledge articles", typeof(KnowledgeBasePermissions)));
}

/// <summary>
/// Declares the Knowledge Base's permissions.
/// </summary>
internal sealed class KnowledgeBasePermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _permissions = [KnowledgeBasePermissions.ViewStaffArticles];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_permissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype { Name = OrchardCoreConstants.Roles.Administrator, Permissions = _permissions },
            new PermissionStereotype { Name = OrchardCoreConstants.Roles.Editor, Permissions = _permissions },
        ];
}
