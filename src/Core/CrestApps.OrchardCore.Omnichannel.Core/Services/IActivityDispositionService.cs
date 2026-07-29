using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Defines the centralized service for applying dispositions to omnichannel activities.
/// Components that produce outcomes must use this service instead of modifying activities directly.
/// </summary>
public interface IActivityDispositionService
{
    /// <summary>
    /// Applies the specified disposition to an activity, validates it against the activity subject,
    /// records audit information, triggers the configured subject flow, and publishes domain events.
    /// </summary>
    /// <param name="request">The disposition request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The disposition result.</returns>
    Task<ActivityDispositionResult> ApplyAsync(ActivityDispositionRequest request, CancellationToken cancellationToken = default);
}
