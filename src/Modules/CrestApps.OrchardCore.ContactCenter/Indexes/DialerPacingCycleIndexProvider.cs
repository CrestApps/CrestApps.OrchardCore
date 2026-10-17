using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="DialerPacingCycle"/> documents to the <see cref="DialerPacingCycleIndex"/>.
/// </summary>
public sealed class DialerPacingCycleIndexProvider : IndexProvider<DialerPacingCycle>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingCycleIndexProvider"/> class.
    /// </summary>
    public DialerPacingCycleIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<DialerPacingCycle> context)
    {
        context
            .For<DialerPacingCycleIndex>()
            .Map(cycle => new DialerPacingCycleIndex
            {
                ItemId = cycle.ItemId,
                QueueId = cycle.QueueId,
                DialerProfileId = cycle.DialerProfileId,
                Outcome = cycle.Outcome,
                CompletedUtc = cycle.CompletedUtc,
            });
    }
}
