using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core;

public interface IOmnichannelProcessor
{
    /// <summary>
    /// The channel this processor is responsible for.
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// Processes the given activity.
    /// </summary>
    /// <param name="activity"></param>
    /// <returns></returns>
    Task StartAsync(OmnichannelActivity activity, CancellationToken cancellationToken);
}
