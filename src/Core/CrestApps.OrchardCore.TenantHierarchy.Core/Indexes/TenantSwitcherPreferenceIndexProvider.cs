using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Maps <see cref="TenantSwitcherPreference"/> documents to <see cref="TenantSwitcherPreferenceIndex"/>.
/// </summary>
public sealed class TenantSwitcherPreferenceIndexProvider : IndexProvider<TenantSwitcherPreference>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantSwitcherPreferenceIndexProvider"/> class.
    /// </summary>
    public TenantSwitcherPreferenceIndexProvider()
    {
        CollectionName = TenantHierarchyConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<TenantSwitcherPreference> context)
    {
        context
            .For<TenantSwitcherPreferenceIndex>()
            .Map(document => new TenantSwitcherPreferenceIndex
            {
                UserId = document.UserId,
            });
    }
}
