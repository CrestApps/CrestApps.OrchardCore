using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps open shifts to <see cref="OpenShiftIndex"/> rows.
/// </summary>
public sealed class OpenShiftIndexProvider : IndexProvider<OpenShift>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenShiftIndexProvider"/> class.
    /// </summary>
    public OpenShiftIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<OpenShift> context)
    {
        context.For<OpenShiftIndex>()
            .Map(item => new OpenShiftIndex
            {
                ItemId = item.ItemId,
                TeamId = item.TeamId,
                PeriodId = item.PeriodId,
                ShiftKey = item.ShiftKey,
                Date = WorkforceDates.ToKey(item.Date),
                StartUtc = string.IsNullOrEmpty(item.TimeZoneId) ? DateTime.SpecifyKind(item.Start, DateTimeKind.Utc) : CalendarTime.ToUtc(item.Start, item.TimeZoneId),
                Status = item.Status,
                ClaimPolicy = item.ClaimPolicy,
                ClaimDeadlineUtc = item.ClaimDeadlineUtc,
            });
    }
}
