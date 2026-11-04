using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps TimeOffLedgerEntry records to <see cref="TimeOffLedgerEntryIndex"/> rows.
/// </summary>
public sealed class TimeOffLedgerEntryIndexProvider : IndexProvider<TimeOffLedgerEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffLedgerEntryIndexProvider"/> class.
    /// </summary>
    public TimeOffLedgerEntryIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TimeOffLedgerEntry> context)
    {
        context.For<TimeOffLedgerEntryIndex>()
            .Map(item => new TimeOffLedgerEntryIndex
            {
                ItemId = item.ItemId,
                WorkerId = item.WorkerId,
                PolicyId = item.PolicyId,
                Kind = item.Kind,
                EffectiveDate = WorkforceDates.ToKey(item.EffectiveDate),
                IdempotencyKey = item.IdempotencyKey,
                CreatedUtc = item.CreatedUtc,
            });
    }
}
