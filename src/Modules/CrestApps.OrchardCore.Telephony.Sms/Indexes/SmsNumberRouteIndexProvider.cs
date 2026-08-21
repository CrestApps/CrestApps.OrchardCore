using CrestApps.OrchardCore.Telephony.Sms.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Sms.Indexes;

/// <summary>
/// Maps <see cref="SmsNumberRoute"/> documents to the <see cref="SmsNumberRouteIndex"/>.
/// </summary>
public sealed class SmsNumberRouteIndexProvider : IndexProvider<SmsNumberRoute>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNumberRouteIndexProvider"/> class.
    /// </summary>
    public SmsNumberRouteIndexProvider()
    {
        CollectionName = TelephonySmsStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<SmsNumberRoute> context)
    {
        context
            .For<SmsNumberRouteIndex>()
            .Map(route => new SmsNumberRouteIndex
            {
                ItemId = route.ItemId,
                EndpointId = route.EndpointId,
                DialedNumber = route.DialedNumber,
                TargetType = route.TargetType.ToString(),
                TargetId = route.TargetId,
                Enabled = route.Enabled,
            });
    }
}
