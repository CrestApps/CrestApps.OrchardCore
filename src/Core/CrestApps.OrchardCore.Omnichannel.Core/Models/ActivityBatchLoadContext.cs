namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Provides contextual information to an <c>IActivityBatchLoader</c> while it loads a batch.
/// </summary>
public sealed class ActivityBatchLoadContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityBatchLoadContext"/> class.
    /// </summary>
    /// <param name="batch">The batch being loaded. It is already transitioned to the loading state.</param>
    /// <param name="loaderId">The identifier of the user that initiated the load.</param>
    /// <param name="loaderUserName">The username of the user that initiated the load.</param>
    public ActivityBatchLoadContext(
        OmnichannelActivityBatch batch,
        string loaderId,
        string loaderUserName)
    {
        Batch = batch;
        LoaderId = loaderId;
        LoaderUserName = loaderUserName;
    }

    /// <summary>
    /// Gets the batch being loaded.
    /// </summary>
    public OmnichannelActivityBatch Batch { get; }

    /// <summary>
    /// Gets the identifier of the user that initiated the load.
    /// </summary>
    public string LoaderId { get; }

    /// <summary>
    /// Gets the username of the user that initiated the load.
    /// </summary>
    public string LoaderUserName { get; }
}
