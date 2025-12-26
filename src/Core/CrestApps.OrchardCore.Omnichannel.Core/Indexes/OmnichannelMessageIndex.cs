using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

public sealed class OmnichannelMessageIndex : MapIndex
{
    public string Channel { get; set; }

    public string CustomerAddress { get; set; }

    public string ServiceAddress { get; set; }

    public DateTime CreatedUtc { get; set; }

    public bool IsInbound { get; set; }
}
