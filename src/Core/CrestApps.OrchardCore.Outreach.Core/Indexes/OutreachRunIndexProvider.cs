using CrestApps.OrchardCore.Outreach.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Outreach.Core.Indexes;

/// <summary>
/// Maps outreach runs to the <see cref="OutreachRunIndex"/>.
/// </summary>
public sealed class OutreachRunIndexProvider : IndexProvider<OutreachRun>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachRunIndexProvider"/> class.
    /// </summary>
    public OutreachRunIndexProvider()
    {
        CollectionName = OutreachConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<OutreachRun> context)
    {
        context
            .For<OutreachRunIndex>()
            .Map(run => new OutreachRunIndex
            {
                ItemId = run.ItemId,
                OwnerType = run.OwnerType,
                OwnerId = run.OwnerId,
                Status = run.Status,
                CreatedUtc = run.CreatedUtc,
            });
    }
}
