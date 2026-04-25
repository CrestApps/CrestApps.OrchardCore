using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core;

public interface IOmnichannelProcessor
{
    /// <summary>
    /// The channel this processor is responsible for.
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// Starts processing the specified omnichannel activity.
    /// </summary>
    /// <param name="activity">The activity to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task StartAsync(OmnichannelActivity activity, CancellationToken cancellationToken);
}
