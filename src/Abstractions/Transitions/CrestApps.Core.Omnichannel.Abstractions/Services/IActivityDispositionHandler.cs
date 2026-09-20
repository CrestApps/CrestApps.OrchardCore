using CrestApps.Core.Omnichannel.Models;

namespace CrestApps.Core.Omnichannel.Services;

/// <summary>
/// Handles successful omnichannel activity dispositions.
/// </summary>
public interface IActivityDispositionHandler
{
    /// <summary>
    /// Handles a successfully dispositioned activity.
    /// </summary>
    /// <param name="request">The disposition request that completed the activity.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DispositionedAsync(ActivityDispositionRequest request, CancellationToken cancellationToken = default);
}
