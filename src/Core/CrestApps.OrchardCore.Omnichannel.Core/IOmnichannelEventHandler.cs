using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core;

public interface IOmnichannelEventHandler
{
    /// <summary>
    /// Handles the specified omnichannel event.
    /// </summary>
    /// <param name="omnichannelEvent">The event to handle.</param>
    public Task HandleAsync(OmnichannelEvent omnichannelEvent);
}
