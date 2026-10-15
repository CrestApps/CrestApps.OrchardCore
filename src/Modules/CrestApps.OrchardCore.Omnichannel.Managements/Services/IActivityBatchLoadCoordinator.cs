using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Coordinates the loading of an activity batch by resolving the appropriate activity batch loader
/// for the batch source and managing the batch loading lifecycle.
/// </summary>
public interface IActivityBatchLoadCoordinator
{
    /// <summary>
    /// Loads the activities for the batch with the given identifier. The batch must be in the started
    /// state; it is transitioned to the loading state before the resolved loader runs.
    /// </summary>
    /// <param name="batchId">The identifier of the batch to load.</param>
    /// <param name="loaderId">The identifier of the user that initiated the load.</param>
    /// <param name="loaderUserName">The username of the user that initiated the load.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task LoadAsync(
        string batchId,
        string loaderId,
        string loaderUserName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Carries on with a load that stopped part-way, for example because the process running it was restarted. A load
    /// that never got past starting is run from the beginning; one that was loading carries on after the last record it
    /// committed, with the counts it had.
    /// </summary>
    /// <param name="batchId">The identifier of the batch to resume.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ResumeAsync(
        string batchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts what the batch would load if it ran now, without creating, queueing or saving anything. The batch can be
    /// one that is being edited and has not been saved.
    /// </summary>
    /// <param name="batch">The batch to count.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The counts, or why they could not be worked out.</returns>
    Task<ActivityBatchLoadPreview> PreviewAsync(
        OmnichannelActivityBatch batch,
        CancellationToken cancellationToken = default);
}
