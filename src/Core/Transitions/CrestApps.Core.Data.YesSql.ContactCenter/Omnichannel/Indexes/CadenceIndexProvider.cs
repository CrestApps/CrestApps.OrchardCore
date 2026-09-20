using CrestApps.Core.Omnichannel;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Omnichannel.Indexes;

public sealed class CadenceIndexProvider : IndexProvider<Cadence>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceIndexProvider"/> class.
    /// </summary>
    public CadenceIndexProvider()
    {
        CollectionName = OmnichannelCollections.Name;
    }

    public override void Describe(DescribeContext<Cadence> context)
    {
        context
            .For<CadenceIndex>()
            .Map(schedule => new CadenceIndex
            {
                ItemId = schedule.ItemId,
                DisplayText = !string.IsNullOrEmpty(schedule.DisplayText)
                    ? schedule.DisplayText.Substring(0, Math.Min(255, schedule.DisplayText.Length))
                    : null,
                Enabled = schedule.Enabled,
                CreatedUtc = schedule.CreatedUtc,
            });
    }
}
