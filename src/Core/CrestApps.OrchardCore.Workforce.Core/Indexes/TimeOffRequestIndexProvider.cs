using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps TimeOffRequest records to <see cref="TimeOffRequestIndex"/> rows.
/// </summary>
public sealed class TimeOffRequestIndexProvider : IndexProvider<TimeOffRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffRequestIndexProvider"/> class.
    /// </summary>
    public TimeOffRequestIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TimeOffRequest> context)
    {
        context.For<TimeOffRequestIndex>()
            .Map(item => new TimeOffRequestIndex
            {
                ItemId = item.ItemId,
                WorkerId = item.WorkerId,
                TeamId = item.TeamId,
                TypeId = item.TypeId,
                PolicyId = item.PolicyId,
                Status = item.Status,
                StartDate = WorkforceDates.ToKey(item.StartDate),
                EndDate = WorkforceDates.ToKey(item.EndDate),
                SubmittedUtc = item.SubmittedUtc,
            });
    }
}
