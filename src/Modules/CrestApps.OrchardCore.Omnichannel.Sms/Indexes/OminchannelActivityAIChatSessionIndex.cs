using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Indexes;

public sealed class OminchannelActivityAIChatSessionIndex : MapIndex
{
    public string SessionId { get; set; }

    public string ActivityId { get; set; }
}
