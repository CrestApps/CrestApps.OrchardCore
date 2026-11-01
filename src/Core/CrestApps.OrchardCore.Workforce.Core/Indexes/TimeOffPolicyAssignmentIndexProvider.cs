using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps TimeOffPolicy records to <see cref="TimeOffPolicyAssignmentIndex"/> rows.
/// </summary>
public sealed class TimeOffPolicyAssignmentIndexProvider : IndexProvider<TimeOffPolicy>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffPolicyAssignmentIndexProvider"/> class.
    /// </summary>
    public TimeOffPolicyAssignmentIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TimeOffPolicy> context)
    {
        context.For<TimeOffPolicyAssignmentIndex>()
            .Map(item => item.Assignments.Select(assignment => new TimeOffPolicyAssignmentIndex
            {
                PolicyId = item.ItemId,
                TeamId = assignment.TeamId,
                WorkerId = assignment.WorkerId,
            }));
    }
}
