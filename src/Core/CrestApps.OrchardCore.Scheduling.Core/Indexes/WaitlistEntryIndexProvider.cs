using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// Maps <see cref="WaitlistEntry"/> documents to <see cref="WaitlistEntryIndex"/> rows.
/// </summary>
public sealed class WaitlistEntryIndexProvider : IndexProvider<WaitlistEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WaitlistEntryIndexProvider"/> class.
    /// </summary>
    public WaitlistEntryIndexProvider()
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<WaitlistEntry> context)
    {
        // A dated session keeps its own waitlist (SessionWaitlistIndex); it never takes a place an ordinary appointment frees.
        context.For<WaitlistEntryIndex>()
            .When(entry => string.IsNullOrEmpty(entry.SessionId))
            .Map(entry => new WaitlistEntryIndex
            {
                ItemId = entry.ItemId,
                AppointmentTypeId = entry.AppointmentTypeId,
                FromUtc = entry.FromUtc,
                ToUtc = entry.ToUtc,
                Status = entry.Status,
                CreatedUtc = entry.CreatedUtc,
            });
    }
}
