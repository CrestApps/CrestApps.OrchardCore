using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps TimeOffPolicy records to <see cref="TimeOffPolicyIndex"/> rows.
/// </summary>
public sealed class TimeOffPolicyIndexProvider : IndexProvider<TimeOffPolicy>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffPolicyIndexProvider"/> class.
    /// </summary>
    public TimeOffPolicyIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TimeOffPolicy> context)
    {
        context.For<TimeOffPolicyIndex>()
            .Map(item => new TimeOffPolicyIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                TypeId = item.TypeId,
                AccrualMethod = item.AccrualMethod,
                Enabled = item.Enabled,
            });
    }
}
