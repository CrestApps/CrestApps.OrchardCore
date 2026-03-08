using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core;

public interface IOmnichannelEventHandler
{
    public Task HandleAsync(OmnichannelEvent omnichannelEvent);
}
