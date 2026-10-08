using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Maps <see cref="HierarchyAuditEvent"/> documents to <see cref="HierarchyAuditEventIndex"/>.
/// </summary>
public sealed class HierarchyAuditEventIndexProvider : IndexProvider<HierarchyAuditEvent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchyAuditEventIndexProvider"/> class.
    /// </summary>
    public HierarchyAuditEventIndexProvider()
    {
        CollectionName = TenantHierarchyConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<HierarchyAuditEvent> context)
    {
        context
            .For<HierarchyAuditEventIndex>()
            .Map(document => new HierarchyAuditEventIndex
            {
                Name = document.Name,
                ChildEntryId = document.ChildEntryId,
                UserId = document.UserId,
                CreatedUtc = document.CreatedUtc,
            });
    }
}
