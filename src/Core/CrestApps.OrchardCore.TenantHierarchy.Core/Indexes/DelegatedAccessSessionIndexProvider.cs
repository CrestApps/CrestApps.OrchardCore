using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Maps <see cref="DelegatedAccessSession"/> documents to <see cref="DelegatedAccessSessionIndex"/>.
/// </summary>
public sealed class DelegatedAccessSessionIndexProvider : IndexProvider<DelegatedAccessSession>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedAccessSessionIndexProvider"/> class.
    /// </summary>
    public DelegatedAccessSessionIndexProvider()
    {
        CollectionName = TenantHierarchyConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<DelegatedAccessSession> context)
    {
        context
            .For<DelegatedAccessSessionIndex>()
            .Map(document => new DelegatedAccessSessionIndex
            {
                SessionHash = document.SessionHash,
                ParentUserId = document.ParentUserId,
                ParentSessionId = document.ParentSessionId,
                ChildEntryId = document.ChildEntryId,
                IsOpen = document.EndedUtc == null,
                CreatedUtc = document.CreatedUtc,
            });
    }
}
