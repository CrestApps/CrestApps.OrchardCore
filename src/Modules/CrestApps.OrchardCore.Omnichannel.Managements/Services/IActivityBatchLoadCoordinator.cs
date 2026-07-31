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
}
