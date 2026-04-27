using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// Defines the contract for omnichannel event handler.
/// </summary>
public interface IOmnichannelEventHandler
{
    /// <summary>
    /// Handles the specified omnichannel event.
    /// </summary>
    /// <param name="omnichannelEvent">The event to handle.</param>
    public Task HandleAsync(OmnichannelEvent omnichannelEvent);
}
