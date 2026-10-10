using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Maps <see cref="ChildTenantEntry"/> documents to <see cref="ChildTenantEntryIndex"/>.
/// </summary>
public sealed class ChildTenantEntryIndexProvider : IndexProvider<ChildTenantEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChildTenantEntryIndexProvider"/> class.
    /// </summary>
    public ChildTenantEntryIndexProvider()
    {
        CollectionName = TenantHierarchyConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ChildTenantEntry> context)
    {
        context
            .For<ChildTenantEntryIndex>()
            .Map(entry => new ChildTenantEntryIndex
            {
                EntryId = entry.EntryId,
                TenantId = entry.TenantId,
                TenantName = entry.TenantName,
                Slug = entry.Slug,
                DisplayName = entry.DisplayName,
                Status = entry.Status.ToString(),
                CreatedUtc = entry.CreatedUtc,
                RetainUntilUtc = entry.RetainUntilUtc,
            });
    }
}
