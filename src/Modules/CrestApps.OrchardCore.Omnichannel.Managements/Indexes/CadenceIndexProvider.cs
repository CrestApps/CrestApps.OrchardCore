using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class CadenceIndexProvider : IndexProvider<Cadence>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceIndexProvider"/> class.
    /// </summary>
    public CadenceIndexProvider()
    {
        CollectionName = OmnichannelConstants.CollectionName;
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
