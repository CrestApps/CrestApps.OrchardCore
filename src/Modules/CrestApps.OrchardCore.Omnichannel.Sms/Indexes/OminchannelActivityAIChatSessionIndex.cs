using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Sms.Indexes;

public sealed class OminchannelActivityAIChatSessionIndex : MapIndex
{
    public string SessionId { get; set; }

    public string ActivityId { get; set; }
}
