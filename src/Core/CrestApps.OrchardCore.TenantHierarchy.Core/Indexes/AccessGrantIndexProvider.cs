using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Maps <see cref="AccessGrant"/> documents to <see cref="AccessGrantIndex"/>.
/// </summary>
public sealed class AccessGrantIndexProvider : IndexProvider<AccessGrant>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccessGrantIndexProvider"/> class.
    /// </summary>
    public AccessGrantIndexProvider()
    {
        CollectionName = TenantHierarchyConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AccessGrant> context)
    {
        context
            .For<AccessGrantIndex>()
            .Map(document => new AccessGrantIndex
            {
                GrantId = document.GrantId,
                PrincipalType = document.PrincipalType.ToString(),
                PrincipalId = document.PrincipalId,
                ChildEntryId = document.ChildEntryId,
            });
    }
}
