using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

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
