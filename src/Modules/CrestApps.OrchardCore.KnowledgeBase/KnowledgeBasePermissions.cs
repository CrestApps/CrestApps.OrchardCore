using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.KnowledgeBase;

/// <summary>
/// The Knowledge Base's permissions. Writing articles uses the content permissions of the Knowledge Article type, and who
/// may read an article is Content Access Control's.
/// </summary>
public static class KnowledgeBasePermissions
{
    /// <summary>
    /// Reads readers' answers to "Was this helpful?": the feedback report and its data set.
    /// </summary>
    public static readonly Permission ViewFeedback = new("ViewKnowledgeBaseFeedback", LocalizationSource.Create("View knowledge base feedback", typeof(KnowledgeBasePermissions)));
}

/// <summary>
/// Declares the Knowledge Base's permissions.
/// </summary>
internal sealed class KnowledgeBasePermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _permissions = [KnowledgeBasePermissions.ViewFeedback];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_permissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype { Name = OrchardCoreConstants.Roles.Administrator, Permissions = _permissions },
            new PermissionStereotype { Name = OrchardCoreConstants.Roles.Editor, Permissions = _permissions },
        ];
}
