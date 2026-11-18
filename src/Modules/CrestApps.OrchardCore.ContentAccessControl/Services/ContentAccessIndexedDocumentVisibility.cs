using CrestApps.OrchardCore.Indexing;
using CrestApps.OrchardCore.Roles.Core.Indexes;
using OrchardCore;
using YesSql;

namespace CrestApps.OrchardCore.ContentAccessControl.Services;

/// <summary>
/// Tells the stores that copy search index documents, such as an AI knowledge base, which content items aren't for
/// everyone: a published item restricted to roles that leave out <c>Anonymous</c>. An item of a type with a restricting
/// role picker and no role picked has an <c>Anonymous</c> row in <see cref="ContentAccessRoleIndex"/> and is for
/// everyone; an item of a type without one has no row and is for everyone too, as far as Content Access Control goes.
/// </summary>
internal sealed class ContentAccessIndexedDocumentVisibility : IIndexedDocumentVisibility
{
    private readonly ISession _session;

    public ContentAccessIndexedDocumentVisibility(ISession session)
    {
        _session = session;
    }

    public async Task<bool> IsForEveryoneAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(documentId))
        {
            return true;
        }

        var roles = (await _session.QueryIndex<ContentAccessRoleIndex>(index => index.ContentItemId == documentId).ListAsync(cancellationToken))
            .Select(row => row.RoleName)
            .ToList();

        return IsForEveryone(roles);
    }

    /// <summary>
    /// Returns whether an item with these allowed roles is for everyone.
    /// </summary>
    /// <param name="allowedRoles">The roles of the item's rows in <see cref="ContentAccessRoleIndex"/>.</param>
    /// <returns><see langword="true"/> when there are none or one is <c>Anonymous</c>.</returns>
    internal static bool IsForEveryone(IReadOnlyCollection<string> allowedRoles)
        => allowedRoles.Count == 0
            || allowedRoles.Contains(OrchardCoreConstants.Roles.Anonymous, StringComparer.OrdinalIgnoreCase);
}
