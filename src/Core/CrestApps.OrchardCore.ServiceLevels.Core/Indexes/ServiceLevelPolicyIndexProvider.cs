using CrestApps.OrchardCore.ServiceLevels.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Indexes;

/// <summary>
/// Maps service level policies to their index rows.
/// </summary>
public sealed class ServiceLevelPolicyIndexProvider : IndexProvider<ServiceLevelPolicy>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelPolicyIndexProvider"/> class.
    /// </summary>
    public ServiceLevelPolicyIndexProvider()
    {
        CollectionName = ServiceLevelsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<ServiceLevelPolicy> context)
    {
        context.For<ServiceLevelPolicyIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(policy => new ServiceLevelPolicyIndex
            {
                ItemId = policy.ItemId,
                Name = policy.Name,
                Scope = policy.Scope,
            });
    }
}
