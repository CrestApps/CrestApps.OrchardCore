using CrestApps.OrchardCore.Telephony.Sms.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Sms.Indexes;

/// <summary>
/// Maps <see cref="SmsBroadcast"/> documents to the <see cref="SmsBroadcastIndex"/>.
/// </summary>
public sealed class SmsBroadcastIndexProvider : IndexProvider<SmsBroadcast>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsBroadcastIndexProvider"/> class.
    /// </summary>
    public SmsBroadcastIndexProvider()
    {
        CollectionName = TelephonySmsStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<SmsBroadcast> context)
    {
        context
            .For<SmsBroadcastIndex>()
            .Map(broadcast => new SmsBroadcastIndex
            {
                ItemId = broadcast.ItemId,
                Name = broadcast.Name,
                Status = broadcast.Status.ToString(),
            });
    }
}
