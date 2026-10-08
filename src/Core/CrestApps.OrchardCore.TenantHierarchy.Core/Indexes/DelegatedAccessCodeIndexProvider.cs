using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Maps <see cref="DelegatedAccessCode"/> documents to <see cref="DelegatedAccessCodeIndex"/>.
/// </summary>
public sealed class DelegatedAccessCodeIndexProvider : IndexProvider<DelegatedAccessCode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedAccessCodeIndexProvider"/> class.
    /// </summary>
    public DelegatedAccessCodeIndexProvider()
    {
        CollectionName = TenantHierarchyConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<DelegatedAccessCode> context)
    {
        context
            .For<DelegatedAccessCodeIndex>()
            .Map(document => new DelegatedAccessCodeIndex
            {
                CodeHash = document.CodeHash,
                ExpiresUtc = document.ExpiresUtc,
            });
    }
}
