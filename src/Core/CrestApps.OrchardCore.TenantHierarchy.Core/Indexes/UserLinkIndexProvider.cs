using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Maps <see cref="UserLink"/> documents to <see cref="UserLinkIndex"/>.
/// </summary>
public sealed class UserLinkIndexProvider : IndexProvider<UserLink>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserLinkIndexProvider"/> class.
    /// </summary>
    public UserLinkIndexProvider()
    {
        CollectionName = TenantHierarchyConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<UserLink> context)
    {
        context
            .For<UserLinkIndex>()
            .Map(document => new UserLinkIndex
            {
                ChildUserId = document.ChildUserId,
                ParentTenantId = document.ParentTenantId,
                ParentUserId = document.ParentUserId,
                IsCurrent = document.IsCurrent,
            });
    }
}
