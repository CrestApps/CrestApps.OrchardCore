using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Scores or filters routing candidates for a queued activity.
/// </summary>
public interface IActivityRoutingStrategy
{
    /// <summary>
    /// Gets the strategy order. Lower values run first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Applies the routing strategy to the current candidate set.
    /// </summary>
    /// <param name="context">The routing context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default);
}
