using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Defines a strategy that loads activities into an <see cref="OmnichannelActivityBatch"/> for a
/// specific activity batch source. Implement and register this interface to add a new source that
/// controls how leads are queried, filtered, and turned into activities.
/// </summary>
public interface IActivityBatchLoader
{
    /// <summary>
    /// Gets the activity batch source this loader is responsible for. The value must match a
    /// registered <c>ActivityBatchSourceEntry.Source</c> value.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Loads the activities for the batch described by the supplied context.
    /// The implementation owns its own filtering and activity creation, updates
    /// <see cref="OmnichannelActivityBatch.TotalLoaded"/>, and sets the terminal
    /// <see cref="OmnichannelActivityBatch.Status"/> (typically loaded, or new when it aborts).
    /// </summary>
    /// <param name="context">The context describing the batch to load and the initiating user.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task LoadAsync(ActivityBatchLoadContext context, CancellationToken cancellationToken = default);
}
